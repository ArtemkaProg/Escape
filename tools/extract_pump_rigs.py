"""Recover private pump proxy constraints, per-instance clocks and safe contacts.

Reads the original Unity4 serialized components and verifies their field layout
against DLL metadata. The output is local APK-derived data, never public content.
"""
import argparse,collections,json,struct,sys
from pathlib import Path

def extract(project,output):
    src=next((project/s for s in ('RecoverySource','RecoveredEscapeUnity/RecoverySource')
              if (project/s/'Tools/unity4_reader.py').is_file()),None)
    if src is None:raise ValueError('Private original recovery archive required')
    sys.path.insert(0,str(src/'Tools'))
    from unity4_reader import UnitySerialized,Reader
    from dotnet_inspect import Metadata
    a=UnitySerialized(src/'OriginalUnityData/level0');objects={o['id']:o for o in a.objects}
    assert a.unity_version=='4.6.5f1'
    def raw(id):
        o=objects[id];return a.data[o['offset']:o['offset']+o['size']]
    h=json.loads((project/'Assets/StreamingAssets/escape_hierarchy.json').read_text('utf8'))
    cs=json.loads((src/'SceneData/components.json').read_text('utf8'))
    ts={t['id']:t for t in h['transforms']}
    bygo={t['goId']:t for t in h['transforms']}
    gos={g['id']:g for g in h['gameObjects']}
    children=collections.defaultdict(list)
    for t in ts.values():children[t['parentId']].append(t['goId'])
    selected=set();queue=[1010,14014,1060,1325]
    while queue:
        g=queue.pop();selected.add(g);queue.extend(children[bygo[g]['id']])
    m=Metadata(src/'OriginalUnityData/Managed/Assembly-CSharp.dll');names=m.type_names();rows=m.tables[2]
    def fields(name):
        i=next(i for i,n in names.items() if n==name);t=rows[i-1]
        return [m.text(m.tables[4][j-1][1]) for j in range(t[4],rows[i][4]) if m.tables[4][j-1][0]&7==6]
    assert fields('AnimationTracker.AnimationConstraint_PositionRotation')==['master']
    assert fields('AnimationTracker.AnimatableBehaviour+AnimationSpeed')==['value','speedScale','mode','parentAnimator']
    assert fields('AnimationTracker.AnimatorController+AnimationTime')==['startTime','overrideStartTimeByState','parentAnimatorController','statesStartTimes']
    constraints=[];nulls=[]
    for c in cs:
        if c['go_id'] not in selected or c['script_class']!='AnimationConstraint_PositionRotation':continue
        b=raw(c['id']);assert len(b)==32
        file,master=struct.unpack_from('<ii',b,24);assert file==0
        if not master:nulls.append(c['id']);continue
        assert master in ts and ts[master]['goId'] in selected
        constraints.append(dict(sourceId=c['id'],slaveGo=c['go_id'],masterGo=ts[master]['goId']))
    pumps=[]
    for c in cs:
        if c['go_id'] not in (1010,14014,1060,1325) or c['script_class']!='AnimatorController':continue
        b=raw(c['id']);assert len(b)==124
        r=Reader(b,'<');r.at(24)
        play,rewind=r.u8(),None;r.align();rewind=r.u8();r.align()
        assert play==0 and rewind==0 and r.i32()==0 # no nested controller list
        value,scale,mode,parentfile,parentanim=r.f32(),r.f32(),r.i32(),r.i32(),r.i32()
        animate=bool(r.u8());r.align();animated=r.f32()
        animatorfile,animator=r.i32(),r.i32();legacyfile,legacy=r.i32(),r.i32()
        phase=r.f32();override=r.u8();r.align();pf,pc=r.i32(),r.i32();count=r.i32()
        assert (mode,parentfile,parentanim,animatorfile,legacyfile,legacy,override,pf,pc,count)==(0,0,83105,0,0,0,0,0,0,0)
        assert r.p==100 and not any(b[100:])
        animatorobj=objects[animator];assert animatorobj['class_id']==95
        animgo=struct.unpack_from('<i',raw(animator),4)[0]
        assert animgo in selected
        pumps.append(dict(goId=c['go_id'],name=gos[c['go_id']]['name'],animatorGo=animgo,
                          speedValue=value,speedScale=scale,animateSpeed=animate,animatedSpeed=animated,
                          speedPerZoneSecond=(animated if animate else value)*scale,
                          startNormalized=phase,clipDuration=2.0))
    assert len(pumps)==4 and len(constraints)+len(nulls)==16
    # Preserve native custom tag encoding: user tags are 20000 + TagManager index.
    main=UnitySerialized(src/'OriginalUnityData/mainData');tagobj=next(o for o in main.objects if o['class_id']==78)
    r=Reader(main.data[tagobj['offset']:tagobj['offset']+tagobj['size']],'<')
    tags=[r.aligned_string() for _ in range(r.i32())]
    safeTag=20000+tags.index('DontHitRocket')
    assert safeTag==20003
    parent={t['goId']:ts[t['parentId']]['goId'] for t in ts.values() if t['parentId'] in ts}
    rb={struct.unpack_from('<i',raw(o['id']),4)[0] for o in objects.values() if o['class_id']==54}
    def tag(g):
        r=Reader(raw(g),'<');r.read(r.i32()*12);r.i32();r.aligned_string();return r.u16()
    def safe(g):
        owner=g;p=g
        while p in gos:
            if p in rb:owner=p;break
            p=parent.get(p,0)
        return tag(owner)==safeTag
    prior=json.loads((project/'Assets/RecoveredPlayablePass12Local/zone003.json').read_text('utf8'))
    identities=[dict(sourceId=c['id'],safe=safe(c['goId'])) for c in prior['colliders']]
    result=dict(format='ESCPUMPRIG13',pumps=pumps,constraints=constraints,nullConstraints=nulls,
                identities=identities,nodes=prior['nodes'])
    output.parent.mkdir(parents=True,exist_ok=True)
    output.write_text(json.dumps(result,separators=(',',':')),encoding='utf8')
    print('Constraint proxies:',len(constraints),'null master (original no-op):',len(nulls))
    for p in pumps:print(p['name'],'phase',p['startNormalized'],'speed factor',p['speedPerZoneSecond'],
                         'real active speed',p['speedPerZoneSecond']*1.25)
    print('Safe collider identities:',sum(c['safe'] for c in identities))
    print('Private output:',output)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    args=p.parse_args();extract(args.project.resolve(),args.output.resolve())
