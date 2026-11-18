"""Read owner's Unity4 legacy particles; emit only local private derivatives.

Unity4 32-bit PPtrs and renderer header are verified independently of Unity5
reference schema. Unity5 field order is a cross-check, not an offset template.
"""
import argparse,json,struct,sys,math,hashlib
from pathlib import Path

def recover(project):
    src=project/'RecoveredEscapeUnity/RecoverySource';sys.path.insert(0,str(src/'Tools'))
    from unity4_reader import UnitySerialized,Reader
    from PIL import Image,ImageOps
    import texture2ddecoder
    a=UnitySerialized(src/'OriginalUnityData/level0');obs={o['id']:o for o in a.objects}
    shared=[UnitySerialized(src/'OriginalUnityData'/n) for n in ('sharedassets1.assets','sharedassets0.assets')]
    def raw(id):
        o=obs[id];return a.data[o['offset']:o['offset']+o['size']]
    def f(b,off,n=1):
        v=struct.unpack_from('<'+'f'*n,b,off);assert all(math.isfinite(x) for x in v);return v[0] if n==1 else list(v)
    def vec(b,off):return dict(zip(('x','y','z'),f(b,off,3)))
    def color(b):return dict(zip(('r','g','b','a'),(x/255 for x in b)))
    hierarchy=json.loads((project/'Assets/StreamingAssets/escape_hierarchy.json').read_text('utf8'))
    gos={g['id']:g for g in hierarchy['gameObjects']}
    prior=json.loads((project/'Assets/RecoveredVisualsPass15Local/visuals.json').read_text('utf8'));selected={n['id'] for n in prior['nodes']}
    manifest=json.loads((project/'Assets/StreamingAssets/escape_material_manifest.json').read_text('utf8'))['materials']
    out=project/'Assets/RecoveredParticlesPass16Local';out.mkdir(parents=True,exist_ok=True)
    particles=[];materials={}
    for o in a.objects:
        if o['class_id']!=15:continue
        b=raw(o['id']);go=struct.unpack_from('<i',b,4)[0]
        if go not in selected or go in (12345,20405):continue # takeoff state machine is a later pass
        assert len(b)==116 and b[8:12]==b'\1\1\0\0'
        assert b[96:100]==b'\1\1\0\0' # random rotation, world space, continuous
        g=gos[go];anim=raw(next(c['path_id'] for c in g['components'] if c['class_id']==12))
        rend=raw(next(c['path_id'] for c in g['components'] if c['class_id']==26))
        assert len(anim)==90 and len(rend)==104
        assert struct.unpack_from('<i',rend,28)[0]==1
        matfile,matid=struct.unpack_from('<ii',rend,32);assert (matfile,matid) in [(1,5),(2,2),(1,24)]
        assert not any(f(b,76,3)) and f(b,112)==0 # no tangent velocity or hollow ellipsoid
        assert not any(f(anim,32,6)) and f(anim,84)==1 and anim[88:90]==b'\0\0'
        assert struct.unpack_from('<i',rend,76)[0]==0 # billboard
        m=next(m for m in manifest if m['fileId']==matfile and m['id']==matid)
        mode={(1,5):0,(2,2):1,(1,24):2}[(matfile,matid)]
        key=f'{matfile}_{matid}'
        if key not in materials:
            sf=matfile if m['textureFileId']==0 else (2 if m['textureFileId']==2 else 1)
            tex=shared[sf-1];to=next(x for x in tex.objects if x['id']==m['textureId'] and x['class_id']==28)
            r=Reader(tex.data,'<');r.at(to['offset']);name=r.aligned_string();w,h,size,fmt=struct.unpack_from('<4I',tex.data,r.p)
            n=struct.unpack_from('<I',tex.data,r.p+52)[0];data=tex.data[r.p+56:r.p+56+n]
            assert r.p+56+n==to['offset']+to['size']
            if fmt==54:im=Image.frombytes('RGBA',(w,h),texture2ddecoder.decode_astc(data[:((w+3)//4)*((h+3)//4)*16],w,h,4,4),'raw','BGRA')
            elif fmt==4:im=Image.frombytes('RGBA',(w,h),data[:w*h*4])
            elif fmt==13: # Original TextureFormat.RGBA4444; little-endian packed words
                values=struct.unpack_from('<'+'H'*(w*h),data)
                pixels=bytes(vv for v in values for vv in (((v>>12)&15)*17,((v>>8)&15)*17,((v>>4)&15)*17,(v&15)*17))
                im=Image.frombytes('RGBA',(w,h),pixels)
            else:raise ValueError(('Unverified texture format',fmt))
            png=out/f'texture_{sf}_{m["textureId"]}.png';ImageOps.flip(im).save(png)
            materials[key]=dict(key=key,mode=mode,name=m['name'],texture=str(png.relative_to(project)).replace('\\','/'),cutoff=m['cutoff'],width=w,height=h,format=fmt,sha256=hashlib.sha256(data).hexdigest())
        particles.append(dict(goId=go,sourceEmitter=o['id'],name=g['name'],material=key,
            minSize=f(b,12),maxSize=f(b,16),minEnergy=f(b,20),maxEnergy=f(b,24),minEmission=f(b,28),maxEmission=f(b,32),
            worldVelocity=vec(b,36),localVelocity=vec(b,48),randomVelocity=vec(b,60),inheritVelocity=f(b,72),
            angularVelocity=f(b,88),randomAngularVelocity=f(b,92),ellipsoid=vec(b,100),
            animateColor=bool(anim[8]),colors=[color(anim[i:i+4]) for i in range(12,32,4)],
            sizeGrow=f(anim,56),randomForce=vec(anim,60),force=vec(anim,72),
            sortingLayer=struct.unpack_from('<i',rend,64)[0],sortingOrder=struct.unpack_from('<h',rend,68)[0],
            cameraVelocity=f(rend,72),lengthScale=f(rend,80),velocityScale=f(rend,84),maxParticleSize=f(rend,88),
            tilesX=struct.unpack_from('<i',rend,92)[0],tilesY=struct.unpack_from('<i',rend,96)[0],cycles=f(rend,100)))
    assert len(particles)==5 and sum(x['material']=='1_24' for x in particles)==1
    result=dict(format='ESCPARTICLE16',nodes=prior['nodes'],particles=particles,materials=list(materials.values()))
    (out/'particles.json').write_text(json.dumps(result,separators=(',',':')),encoding='utf8')
    (out/'verification.tsv').write_text('\n'.join('\t'.join(str(x[k]) for k in ('goId','maxEnergy','maxSize','maxEmission','sortingOrder','tilesX','tilesY')) for x in particles),encoding='utf8')
    print('Recovered',len(particles),'legacy emitters; original materials:',[(x['name'],x['width'],x['height'],x['format']) for x in materials.values()])
    for x in particles:print(x['goId'],x['name'],x['material'],'rate',x['minEmission'],x['maxEmission'],'force',x['force'],'grow',x['sizeGrow'])

if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--project',type=Path,required=True);recover(ap.parse_args().project.resolve())
