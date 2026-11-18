"""Private original survivor, background-camera and animation extraction for PASS17."""
import argparse,json,struct,sys,zlib,collections
from pathlib import Path

def recover(p):
 src=p/'RecoveredEscapeUnity/RecoverySource';sys.path.insert(0,str(src/'Tools'));sys.path.insert(0,str(p/'tools'))
 from unity4_reader import UnitySerialized,Reader
 a=UnitySerialized(src/'OriginalUnityData/level0');sh=UnitySerialized(src/'OriginalUnityData/sharedassets1.assets');ob={o['id']:o for o in a.objects}
 def raw(id):
  o=ob[id];return a.data[o['offset']:o['offset']+o['size']]
 def ptr(id,off):
  b=raw(id);assert struct.unpack_from('<i',b,off)[0]==0;return struct.unpack_from('<i',b,off+4)[0]
 def go(id):return struct.unpack_from('<i',raw(id),4)[0]
 h=json.loads((p/'Assets/StreamingAssets/escape_hierarchy.json').read_text());gs={g['id']:g for g in h['gameObjects']};tr={t['id']:t for t in h['transforms']};tg={t['goId']:t for t in tr.values()};children=collections.defaultdict(list)
 for t in tr.values():children[t['parentId']].append(t['goId'])
 cs=json.loads((src/'SceneData/components.json').read_text());byid={c['id']:c for c in cs}
 fx=json.loads((p/'Assets/RecoveredEffectsPass14Local/effects.json').read_text());prior=json.loads((p/'Assets/RecoveredPlayablePass12Local/zone003.json').read_text())
 # This variant adds verified constant channels and loop flags; it does not
 # change the strict reader used by previously accepted passes.
 from legacy_streamed_clip import read_streamed_clip
 code=(p/'tools/legacy_streamed_clip.py').read_text().replace('assert flags == [0,0,0,0,0,0,0,1,0]','assert all(v in (0,1) for v in flags)').replace('assert not array() and not array() and r.i32() == 0','assert not array(); constants=array(); assert r.i32() == 0').replace('return dict(name=name,duration=duration,bindings=bindings,streams=[dict(keys=s) for s in streams])','return dict(name=name,duration=duration,loop=bool(flags[1]),bindings=bindings,streams=[dict(keys=s) for s in streams]+[dict(keys=[dict(time=0,a=0,b=0,c=0,d=v)]) for v in constants])')
 ns={};exec(code,ns);read=ns['read_streamed_clip'];clips={id:read(sh,id,Reader) for id in [405,406,407,409,410,411,412,413]}
 colors={zlib.crc32(('m_Color.'+v).encode())&0xffffffff:i for i,v in enumerate('rgba')}
 def tracks(root,clip):
  paths=collections.defaultdict(list)
  def walk(g,path):
   paths[zlib.crc32(path.encode())&0xffffffff].append((g,path))
   for ch in children[tg[g]['id']]:walk(ch,path+('/' if path else '')+gs[ch]['name'])
  walk(root,'');result=[];index=0
  for b in clip['bindings']:
   matches=paths[b['pathHash']];assert len(matches)==1,(root,b,matches)
   g,path=matches[0];kind=b['attribute'] if b['classId']==4 else 4 if b['classId']==1 else 10+colors[b['attribute']] if b['classId']==212 else -1
   assert kind>=0,b
   n={1:3,2:4,3:3}.get(kind,1);result.append(dict(goId=g,path=path,kind=kind,channels=clip['streams'][index:index+n]));index+=n
  assert index==len(clip['streams'])
  return result
 people=[]
 for marker in fx['survivors']:
  root=marker['survivorGo'];c=next(c for c in cs if c['go_id']==root and c['script_class']=='Survivor');id=c['id']
  movement=ptr(id,24);link=ptr(id,32);attract=ptr(id,40);destroy=ptr(id,48);life=ptr(id,56);invincible=ptr(id,64);ia=ptr(id,72);collider=ptr(id,96)
  behaviour=ptr(attract,24);b=raw(behaviour);assert len(b)==80
  duration,smooth,minspin,maxspin,mode,reach=struct.unpack_from('<ffffif',b,56);assert mode in [0,1]
  anim=ptr(behaviour,48);controller=ptr(anim,24);native=ptr(controller,64);animRoot=go(native)
  movementRoot=go(ptr(movement,24));assert movementRoot==root
  state=struct.unpack_from('<i',raw(ia),24+4+7*12+8)[0] if len(raw(ia))>120 else 0
  # IA serialized states list has seven (enum, controller PPtr) pairs;
  # followed by tagFilterGroup PPtr and initial state (omitted when zero).
  ir=Reader(raw(ia),'<');ir.at(24);count=ir.i32();ir.read(count*12);ir.read(8);initial=ir.i32() if ir.p<len(ir.b) else 0
  destroyEvents=[x for x in cs if x['script_class']=='OnSurvivorIn_DestroySurvivor' and ptr(x['id'],24)==ptr(behaviour,40)]
  assert len(destroyEvents)==1
  deathDelay=struct.unpack_from('<f',raw(destroyEvents[0]['id']),32)[0]
  linkanim=ptr(link,40);laser=ptr(linkanim,32)
  animations=[dict(name=clips[k]['name'],duration=clips[k]['duration'],loop=clips[k]['loop'],tracks=tracks(animRoot,clips[k])) for k in [413,405,409,410,411,412,407]]
  linkclip=clips[406];linkRoot=go(ptr(linkanim,24))
  rb=raw(ptr(movement,24));mass,drag,angularDrag=struct.unpack_from('<3f',rb,8)
  people.append(dict(goId=root,markerGo=marker['goId'],colliderGo=go(collider),animatorGo=animRoot,initialState=initial,mass=mass,drag=drag,angularDrag=angularDrag,
   duration=duration,smooth=smooth,minSpin=minspin,maxSpin=maxspin,mode=mode,reach=reach,hideDelay=deathDelay,
   laserGo=tr[laser]['goId'],linkRoot=linkRoot,linkDuration=linkclip['duration'],linkLock=struct.unpack_from('<f',raw(link),48)[0],linkCut=struct.unpack_from('<f',raw(link),52)[0],linkTracks=tracks(linkRoot,linkclip),animations=animations))
 # Attraction event's probe is distinct from the larger reveal/door probe.
 attractor=next(c for c in cs if c['script_class']=='OnSurvivorNear_AttractSurvivor');probe=ptr(attractor['id'],24);target=ptr(attractor['id'],40);sphere=ptr(probe,28)
 groups=[];zoneclip=read(sh,452,Reader)
 for c in cs:
  if c['go_id'] not in [2676,33909] or c['script_class']!='SurvivorGroup_SetIA':continue
  b=raw(c['id']);group=ptr(c['id'],24);gb=raw(group);n=struct.unpack_from('<i',gb,24)[0];members=[go(struct.unpack_from('<i',gb,32+j*8)[0]) for j in range(n)]
  state=struct.unpack_from('<i',b,36)[0];dmin,dmax,inv=struct.unpack_from('<3f',b,44);assert dmin==dmax==0
  pending=next(x for x in prior['pending'] if x['goId']==c['go_id'] and x['scriptId']==112)
  groups.append(dict(goId=c['go_id'],state=state,invincibility=inv,members=members,gate=zoneclip['streams'][pending['firstChannel']]))
 follow=next(c for c in cs if c['go_id']==2676 and c['script_class']=='FollowPositionRotation');master=ptr(follow['id'],64);slave=ptr(follow['id'],72)
 gate=next(x for x in prior['pending'] if x['goId']==2676 and x['scriptId']==410)
 cameras=json.loads((p/'Assets/StreamingAssets/escape_camera_manifest.json').read_text())['cameras'];cam=next(x for x in cameras if x['goId']==33289)
 fov=struct.unpack_from('<f',raw(98832),40)[0];sizeScale=struct.unpack_from('<f',raw(97705),28)[0]
 backgrounds=[]
 for gid in [16192,33126]:
  strip=next(c for c in cs if c['go_id']==gid and c['script_class']=='ProceduralMesh_Strip');b=raw(strip['id']);segments=[]
  for j in range(struct.unpack_from('<i',b,32)[0]):
   seg=struct.unpack_from('<i',b,40+j*8)[0];sb=raw(seg);segments.append(dict(goId=go(seg),top=list(struct.unpack_from('<4f',sb,24)),bottom=list(struct.unpack_from('<4f',sb,40))))
  backgrounds.append(dict(goId=gid,segments=segments))
 linkprobe=ptr(ptr(97294,24),28)
 cameraZ=0.;t=tg[33289]
 while t:
  cameraZ+=t['position'][2];t=tr.get(t['parentId'])
 data=dict(format='ESCSURV17',nodes=prior['nodes'],people=people,groups=groups,probeGo=go(sphere),linkProbeGo=go(linkprobe),targetGo=tr[target]['goId'],
  follow=dict(masterGo=tr[master]['goId'],slaveGo=tr[slave]['goId'],gate=zoneclip['streams'][gate['firstChannel']]),backgrounds=backgrounds,fov=fov*sizeScale,backgroundMask=cam['cullingMask'],perspectiveZ=cameraZ)
 out=p/'Assets/RecoveredSurvivorsPass17Local';out.mkdir(exist_ok=True);(out/'survivors.json').write_text(json.dumps(data,separators=(',',':')),encoding='utf8')
 print('People',[(x['goId'],x['animatorGo'],x['initialState'],x['duration'],x['mode']) for x in people]);print('Groups',[(g['state'],g['members']) for g in groups]);print('Attraction probe',data['probeGo'],'target',data['targetGo'],'FOV',data['fov'])

if __name__=='__main__':
 ap=argparse.ArgumentParser();ap.add_argument('--project',type=Path,required=True);recover(ap.parse_args().project.resolve())
