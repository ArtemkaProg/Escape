"""Recover private laser pixels, native reactor trails, light ownership and door curves.
No original content is embedded. Requires the owner's local Unity4 recovery data.
"""
import argparse, json, struct, sys, math, hashlib
from pathlib import Path

def read_clip(shared, clipid, Reader):
    clip=next(o for o in shared.objects if o['id']==clipid and o['class_id']==74)
    clipraw=shared.data[clip['offset']:clip['offset']+clip['size']]
    r = Reader(clipraw, '<')
    name=r.aligned_string(); assert (r.i32(),r.u8(),r.u8()) == (2,0,1)
    r.align(); assert [r.i32() for _ in range(6)] == [0] * 6
    assert r.f32() == 60 and r.i32() == 0
    assert not any(r.f32() for _ in range(6))
    muscle_bytes = r.u32()
    def xform(): r.read(48)
    def array():
        n = r.i32(); assert 0 <= n < 500000; return [r.f32() for _ in range(n)]
    xform(); r.read(32)
    for _ in range(r.i32()): xform(); r.read(8)
    for _ in range(2): xform(); array(); r.read(16)
    array()
    for _ in range(5): xform()
    r.read(16)
    nw = r.i32(); words = r.read(nw * 4); ns = r.u32()
    assert (r.i32(), r.u32(), r.f32(), r.f32()) == (1, 0, 60., 0.)
    assert not array() and not array() and r.i32() == 0
    start, duration, *extra = [r.f32() for _ in range(6)]
    assert start == 0 and not any(extra)
    r.read(r.i32() * 4); r.read(r.i32() * 8)
    flags = [r.u8() for _ in range(9)]; r.align()
    assert flags == [0,0,0,0,0,0,0,1,0]
    bindings = []
    for _ in range(r.i32()):
        b = dict(pathHash=r.u32(), attribute=r.u32(), scriptFileId=r.i32(), scriptId=r.i32(),
                 classId=r.u16(), custom=r.u8(), pptr=r.u8())
        r.align(); bindings.append(b)
    assert r.i32() == r.i32() == 0 and r.p == len(clipraw)
    streams = [[] for _ in range(ns)]; p = 0
    while p < len(words):
        time, n = struct.unpack_from('<fi', words, p); p += 8
        assert 0 <= n <= ns
        for _ in range(n):
            index, a, b, c, d = struct.unpack_from('<iffff', words, p); p += 20
            assert 0 <= index < ns and all(math.isfinite(v) for v in (a,b,c,d))
            if 0 <= time <= duration: streams[index].append(dict(time=time, a=a, b=b, c=c, d=d))
    assert p == len(words)  and all(s and s[0]['time'] == 0 for s in streams)

    assert len(bindings)==1 and len(streams)==1
    return dict(name=name,duration=duration,bindings=bindings,channel=dict(keys=streams[0]))

def recover(project):
    source=next((project/p for p in ('RecoverySource','RecoveredEscapeUnity/RecoverySource') if (project/p/'Tools/unity4_reader.py').is_file()),None)
    if source is None:raise ValueError('Private recovery archive required')
    sys.path.insert(0,str(source/'Tools'))
    from unity4_reader import UnitySerialized,Reader
    from dotnet_inspect import Metadata
    from PIL import Image,ImageOps
    import texture2ddecoder
    level=UnitySerialized(source/'OriginalUnityData/level0');shared=UnitySerialized(source/'OriginalUnityData/sharedassets1.assets')
    assert level.unity_version==shared.unity_version=='4.6.5f1'
    obs={o['id']:o for o in level.objects}
    def raw(id):
        o=obs[id];return level.data[o['offset']:o['offset']+o['size']]
    md=Metadata(source/'OriginalUnityData/Managed/Assembly-CSharp.dll');names=md.type_names();rows=md.tables[2]
    def fields(name):
        i=next(i for i,n in names.items() if n==name);t=rows[i-1]
        return [md.text(md.tables[4][j-1][1]) for j in range(t[4],rows[i][4]) if md.tables[4][j-1][0]&7==6]
    assert fields('RocketChips.RocketLight')==['emissionBase','emissionPercent','animatedEmissionPercent','lightSpriteRenderer']
    assert fields('RocketChips.RocketDoor')==['animatedOpenPercent','rocketDoorLight','doorScale']
    assert fields('RocketChips.RocketReactor+TrailFx')==['trailRenderer','clampEmission','maxEmission','emissionScale','clampWidthScale','widthScaleMax']
    cs=json.loads((source/'SceneData/components.json').read_text('utf8'))
    hierarchy=json.loads((project/'Assets/StreamingAssets/escape_hierarchy.json').read_text('utf8'))
    ts={t['id']:t for t in hierarchy['transforms']};gos={g['id']:g for g in hierarchy['gameObjects']}
    prior=json.loads((project/'Assets/RecoveredPlayablePass12Local/zone003.json').read_text('utf8'));selected={n['id'] for n in prior['nodes']}
    out=project/'Assets/RecoveredEffectsPass14Local';out.mkdir(parents=True,exist_ok=True)
    def save_png(name,img):
        import io
        buf=io.BytesIO();img.save(buf,format='PNG');target=out/name
        if target.exists() and target.read_bytes()!=buf.getvalue():raise ValueError('Different private derivative exists: '+str(target))
        if not target.exists():target.write_bytes(buf.getvalue())
        return 'Assets/RecoveredEffectsPass14Local/'+name
    textures={};hashes={}
    for id in (67,73,54):
        o=next(o for o in shared.objects if o['id']==id and o['class_id']==28);r=Reader(shared.data,'<');r.at(o['offset'])
        name=r.aligned_string();w,h,size,fmt=struct.unpack_from('<4I',shared.data,r.p);length=struct.unpack_from('<I',shared.data,r.p+52)[0]
        b=shared.data[r.p+56:r.p+56+length];assert r.p+56+length==o['offset']+o['size']
        if id==67:
            assert (w,h,fmt)==(256,256,54)
            pixels=texture2ddecoder.decode_astc(b[:((w+3)//4)*((h+3)//4)*16],w,h,4,4)
            img=Image.frombytes('RGBA',(w,h),pixels,'raw','BGRA')
        else:
            assert (w,h,fmt)==(32,32,4);img=Image.frombytes('RGBA',(w,h),b[:w*h*4])
        textures[id]=save_png('texture_'+str(id)+'.png',ImageOps.flip(img));hashes[str(id)]=hashlib.sha256(b).hexdigest()
    sprite=json.loads((project/'Assets/StreamingAssets/escape_sprite_manifest.json').read_text('utf8'))
    geometry=json.loads((project/'Assets/StreamingAssets/escape_sprite_geometry.json').read_text('utf8'))
    laser=next(s for s in sprite['sprites'] if s['textureId']==67)
    assert laser['rect']==laser['textureRect']==[0,0,256,256]
    laser=dict(id=laser['id'],png=textures[67],ppu=laser['pixelsPerUnit'],pivot=next(s['pivot'] for s in geometry['sprites'] if s['id']==laser['id']))
    lights=[]
    for c in cs:
        if c['script_class']=='RocketLight' and c['go_id'] in selected:
            b=raw(c['id']);assert len(b)==44
            base,percent,animated=struct.unpack_from('<3f',b,24);sr=struct.unpack_from('<i',b,40)[0]
            srgo=struct.unpack_from('<i',raw(sr),4)[0]
            lights.append(dict(goId=c['go_id'],spriteGo=srgo,baseEmission=base,percent=percent,animated=animated))
    door=raw(93518);assert len(door)==44
    scaleid=struct.unpack_from('<i',door,40)[0];doorLight=struct.unpack_from('<i',door,32)[0]
    probeid=struct.unpack_from('<i',raw(95327),28)[0];probe=raw(probeid);assert probe[24]==1
    sphereid=struct.unpack_from('<i',probe,32)[0];sphere=raw(sphereid);assert obs[sphereid]['class_id']==135
    doorInfo=dict(scaleGo=ts[scaleid]['goId'],lightGo=struct.unpack_from('<i',raw(doorLight),4)[0],probeGo=struct.unpack_from('<i',sphere,4)[0],probeCollider=sphereid,clip=read_clip(shared,404,Reader))
    assert doorInfo['clip']['bindings'][0]['scriptId']==678
    reactor=raw(101921);r=Reader(reactor,'<');r.at(24);base,scale,animated=r.f32(),r.f32(),r.f32();trails=[]
    for _ in range(r.i32()):
        assert r.i32()==0;id=r.i32();clamp=bool(r.u8());r.align();maxem,emscale=r.f32(),r.f32();clampw=bool(r.u8());r.align();maxw=r.f32()
        b=raw(id);assert len(b)==109 and obs[id]['class_id']==96
        go=struct.unpack_from('<i',b,4)[0];material=struct.unpack_from('<i',b,32)[0];time,width,end=struct.unpack_from('<3f',b,72)
        colors=[[v/255 for v in b[i:i+4]] for i in range(84,104,4)]
        trails.append(dict(goId=go,material=material,texture=textures[73 if material==30 else 54],time=time,width=width,endWidth=end,gradient=[dict(zip(('r','g','b','a'),c)) for c in colors],minVertex=struct.unpack_from('<f',b,104)[0],clampEmission=clamp,maxEmission=maxem,emissionScale=emscale,clampWidth=clampw,widthMax=maxw))
    assert r.p==len(reactor) and len(trails)==2
    rc=raw(95730);assert len(rc)==56
    emissionMin,emissionMax,rise,fall=struct.unpack_from('<4f',rc,40)
    survivors=[dict(goId=c['go_id'],survivorGo=struct.unpack_from('<i',raw(struct.unpack_from('<i',raw(c['id']),28)[0]),4)[0]) for c in cs if c['script_class']=='SurvivorCollider' and c['go_id'] in selected]
    bars=[]
    for c in cs:
        if c['script_class']=='FuelBar' and c['go_id'] in selected:
            b=raw(c['id']);light=struct.unpack_from('<i',b,32)[0];bars.append(dict(sourceId=c['id'],goId=c['go_id'],lightGo=struct.unpack_from('<i',raw(light),4)[0]))
    fuel=raw(94015);count=struct.unpack_from('<i',fuel,24)[0]
    order=[struct.unpack_from('<i',fuel,32+i*8)[0] for i in range(count)]
    assert count==4 and set(order)=={b['sourceId'] for b in bars}
    bars.sort(key=lambda b:order.index(b['sourceId']))
    fuelClip=read_clip(shared,377,Reader);assert fuelClip['bindings'][0]['scriptId']==473
    # FuelEvent.trigger is authored by other zone timelines. Zone003 has none.
    assert not any(c['script_class']=='FuelEvent' and c['go_id'] in selected for c in cs)
    result=dict(format='ESCFX14',nodes=prior['nodes'],laser=laser,lights=lights,door=doorInfo,trails=trails,survivors=survivors,bars=bars,fuelClip=fuelClip,emissionBase=base,emissionInitial=scale,emissionAnimated=animated,emissionMin=emissionMin,emissionMax=emissionMax,rise=rise,fall=fall,hashes=hashes)
    (out/'effects.json').write_text(json.dumps(result,separators=(',',':')),encoding='utf8')
    print('Recovered laser ASTC; trails:',len(trails),'lights:',len(lights),'survivor triggers:',len(survivors),'fuel bars:',len(bars))
    print('Door duration',doorInfo['clip']['duration'],'reactor range',emissionMin,emissionMax,'rise/fall',rise,fall)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--project',type=Path,required=True);recover(p.parse_args().project.resolve())
