"""Recover actual laser sprite, correct reactor material links and original recorded debris."""
import argparse,json,struct,sys,math
from pathlib import Path
from legacy_streamed_clip import read_streamed_clip

def recover(project):
    src=project/'RecoveredEscapeUnity/RecoverySource';sys.path.insert(0,str(src/'Tools'))
    from unity4_reader import UnitySerialized,Reader
    from PIL import Image,ImageOps
    import texture2ddecoder
    a=UnitySerialized(src/'OriginalUnityData/level0');shared=UnitySerialized(src/'OriginalUnityData/sharedassets1.assets');obs={o['id']:o for o in a.objects}
    def raw(id):
        o=obs[id];return a.data[o['offset']:o['offset']+o['size']]
    h=json.loads((project/'Assets/StreamingAssets/escape_hierarchy.json').read_text('utf8'));trans={t['id']:t for t in h['transforms']}
    old=json.loads((project/'Assets/RecoveredEffectsPass14Local/effects.json').read_text('utf8'))
    sm=json.loads((project/'Assets/StreamingAssets/escape_sprite_manifest.json').read_text('utf8'))
    geo=json.loads((project/'Assets/StreamingAssets/escape_sprite_geometry.json').read_text('utf8'))
    laserSource=next(r for r in sm['renderers'] if r['goId']==16824);sprite=next(s for s in sm['sprites'] if s['id']==laserSource['spriteId']);assert sprite['id']==705 and sprite['textureId']==48
    out=project/'Assets/RecoveredVisualsPass15Local';out.mkdir(parents=True,exist_ok=True)
    o=next(o for o in shared.objects if o['id']==48 and o['class_id']==28);r=Reader(shared.data,'<');r.at(o['offset']);r.aligned_string();w,hgt,size,fmt=struct.unpack_from('<4I',shared.data,r.p);assert (w,hgt,fmt)==(32,2048,54)
    n=struct.unpack_from('<I',shared.data,r.p+52)[0];blocks=shared.data[r.p+56:r.p+56+n]
    image=ImageOps.flip(Image.frombytes('RGBA',(w,hgt),texture2ddecoder.decode_astc(blocks,w,hgt,4,4),'raw','BGRA'))
    x,y,sw,sh=map(int,sprite['textureRect']);crop=image.crop((x,hgt-y-sh,x+sw,hgt-y));png=out/'laser_705.png'
    if png.exists():
        import io
        buf=io.BytesIO();crop.save(buf,format='PNG');assert png.read_bytes()==buf.getvalue()
    else:crop.save(png)
    laser=dict(id=sprite['id'],png=str(png.relative_to(project)).replace('\\','/'),ppu=sprite['pixelsPerUnit'],pivot=next(s['pivot'] for s in geo['sprites'] if s['id']==sprite['id']))
    trails=[]
    for id in (83324,83326):
        b=raw(id);assert struct.unpack_from('<ii',b,28)==(1,1);material=struct.unpack_from('<i',b,36)[0];assert material in (29,30)
        go=struct.unpack_from('<i',b,4)[0];prior=next(t for t in old['trails'] if t['goId']==go)
        trails.append(dict(goId=go,material=material,texture='Assets/RecoveredEffectsPass14Local/texture_'+str(73 if material==30 else 54)+'.png'))
    # Source animation channels and both recorded PhysicalGroup assets.
    clip=read_streamed_clip(shared,452,Reader);assert len(clip['streams'])==49
    groups=[]
    for groupid,recorderid in [(90903,91429),(92415,92414)]:
        b=raw(groupid);assert struct.unpack_from('<i',b,64)[0]==1 # playback, no re-simulation
        go=struct.unpack_from('<i',b,4)[0];value,scale=struct.unpack_from('<ff',b,36);assert struct.unpack_from('<iii',b,44)==(0,0,83105)
        rec=raw(recorderid);assert rec[24]==0 and struct.unpack_from('<i',rec,28)[0]==1
        asset=struct.unpack_from('<i',rec,32)[0];count=struct.unpack_from('<i',rec,36)[0]
        tids=[struct.unpack_from('<i',rec,44+i*8)[0] for i in range(count)]
        o=next(o for o in shared.objects if o['id']==asset and o['class_id']==114);r=Reader(shared.data[o['offset']:o['offset']+o['size']],'<');r.at(20);r.aligned_string();assert r.i32()==count
        tracks=[]
        for tid in tids:
            curves=[]
            for _ in range(6):
                n=r.i32();assert 0<n<10000;keys=[]
                for _ in range(n):
                    t,v,inn,outt=struct.unpack('<4f',r.read(16));assert all(math.isfinite(vv) for vv in (t,v,inn,outt));keys.append(dict(time=t,value=v,inTangent=inn,outTangent=outt))
                assert (r.i32(),r.i32())==(2,2);curves.append(dict(keys=keys))
            tracks.append(dict(goId=trans[tid]['goId'],curves=curves))
        assert r.p==len(r.b)
        binding=next(x for x in json.loads((project/'Assets/RecoveredPlayablePass12Local/zone003.json').read_text('utf8'))['pending'] if x['goId']==go and x['scriptId']==276)
        gate=clip['streams'][binding['firstChannel']];assert all(k['a']==k['b']==k['c']==0 for k in gate['keys'])
        groups.append(dict(goId=go,speed=value*scale,gate=gate,tracks=tracks))
    data=dict(format='ESCVIS15',nodes=old['nodes'],laser=laser,trails=trails,groups=groups)
    (project/'Assets/RecoveredVisualsPass15Local/visuals.json').write_text(json.dumps(data,separators=(',',':')),encoding='utf8')
    print('Actual laser sprite',laser['id'],'trails',[(t['goId'],t['material']) for t in trails],'recorded debris',sum(len(g['tracks']) for g in groups))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--project',type=Path,required=True);recover(p.parse_args().project.resolve())
