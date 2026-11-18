"""Extract a verified LOCAL Zone003 timeline and original 3D collision shapes.

Requires the user's private Unity4 recovery reader and source assets. No APK
data is embedded in this tool. Output belongs in an ignored local directory.
"""
import argparse
import collections
import hashlib
import json
import math
import struct
import sys
import zlib
from pathlib import Path


def extract(project, output):
    src = next((project / s for s in ('RecoverySource', 'RecoveredEscapeUnity/RecoverySource')
                if (project / s / 'Tools/unity4_reader.py').is_file()), None)
    if src is None: raise ValueError('Private original recovery archive is required')
    sys.path.insert(0, str(src / 'Tools'))
    from unity4_reader import UnitySerialized, Reader
    level = UnitySerialized(src / 'OriginalUnityData/level0')
    shared = UnitySerialized(src / 'OriginalUnityData/sharedassets1.assets')
    main = UnitySerialized(src / 'OriginalUnityData/mainData')
    assert level.unity_version == shared.unity_version == '4.6.5f1'
    objects = {o['id']: o for o in level.objects}
    def raw(objid):
        o = objects[objid]
        return level.data[o['offset']:o['offset'] + o['size']]

    h = json.loads((project / 'Assets/StreamingAssets/escape_hierarchy.json').read_text('utf8'))
    gos = {g['id']: g for g in h['gameObjects']}
    transforms = {t['goId']: t for t in h['transforms']}
    children = collections.defaultdict(list)
    for t in transforms.values(): children[t['parentId']].append(t['goId'])
    parent = {g: t['goId'] for t in transforms.values() for g in children[t['id']]}
    nodes = []
    def walk(g, p, sibling):
        b = raw(g); r = Reader(b, '<'); count = r.i32(); r.read(count * 12)
        layer, name, tag, active = r.i32(), r.aligned_string(), r.u16(), r.u8()
        assert name == gos[g]['name'] and layer == gos[g]['layer']
        t = transforms[g]
        nodes.append(dict(id=g, parent=p, sibling=sibling, name=name, tag=tag,
                          layer=layer, active=bool(active), position=t['position'],
                          rotation=t['rotation'], scale=t['scale']))
        for index, child in enumerate(children[t['id']]): walk(child, g, index)
    walk(15787, 0, 0); walk(27907, 0, 0)
    selected = {n['id'] for n in nodes}
    assert gos[15787]['name'] == 'Zone003' and gos[27907]['name'] == 'Rocket'
    assert next(n for n in nodes if n['id'] == 27907)['tag'] == 6

    # Serialized TagManager custom tags follow the five built-in Unity4 tags.
    tagobj = next(o for o in main.objects if o['class_id'] == 78)
    r = Reader(main.data[tagobj['offset']:tagobj['offset'] + tagobj['size']], '<')
    tags = [r.aligned_string() for _ in range(r.i32())]
    assert tags[1] == 'Rocket' and tags[3] == 'DontHitRocket'
    hit = Reader(raw(97540), '<'); hit.at(24); hit.read(8)
    safe_tags = [hit.aligned_string() for _ in range(hit.i32())]
    assert hit.p == len(hit.b) and safe_tags == ['DontHitRocket']
    rb_go = {struct.unpack_from('<i', raw(o['id']), 4)[0]: o['id']
             for o in level.objects if o['class_id'] == 54}
    def collision_safe(g):
        owner = g; p = g
        while p in selected:
            if p in rb_go: owner = p; break
            p = parent.get(p, 0)
        tag = next(n['tag'] for n in nodes if n['id'] == owner)
        return tag >= 5 and tag - 5 < len(tags) and tags[tag - 5] in safe_tags

    colliders = []
    for o in level.objects:
        cid = o['class_id']
        if cid not in (64, 65, 135): continue
        b = raw(o['id']); sf, g = struct.unpack_from('<ii', b)
        if g not in selected: continue
        assert sf == 0 and b[16] in (0, 1) and b[17] in (0, 1)
        c = dict(id=o['id'], goId=g, kind=cid, trigger=bool(b[16]),
                 enabled=bool(b[17]), safe=collision_safe(g))
        if cid == 64:
            assert len(b) == 32 and b[20] in (0, 1) and b[21] in (0, 1)
            mf, mi = struct.unpack_from('<ii', b, 24)
            assert mf in (0, 1) and mi > 0
            c.update(mesh='Assets/RecoveredOriginalMeshes/mesh_%d_%d.asset' % (mf, mi), convex=bool(b[21]))
            assert (project / c['mesh']).is_file(), c['mesh']
        elif cid == 65:
            assert len(b) == 44
            c.update(size=list(struct.unpack_from('<3f', b, 20)), center=list(struct.unpack_from('<3f', b, 32)))
            assert all(v > 0 for v in c['size'])
        else:
            assert len(b) == 36
            c.update(radius=struct.unpack_from('<f', b, 20)[0], center=list(struct.unpack_from('<3f', b, 24)))
            assert c['radius'] > 0
        colliders.append(c)
    rb = raw(78023)
    assert len(rb) == 32 and not rb[20] and not rb[21]
    rigidbody = dict(mass=struct.unpack_from('<f', rb, 8)[0],
                     drag=struct.unpack_from('<f', rb, 12)[0],
                     angularDrag=struct.unpack_from('<f', rb, 16)[0],
                     gravity=bool(rb[20]), constraints=struct.unpack_from('<i', rb, 24)[0],
                     collisionDetection=struct.unpack_from('<i', rb, 28)[0])
    controller = raw(98501); count = struct.unpack_from('<i', controller, 24)[0]
    controlled = [struct.unpack_from('<ii', controller, 28 + i * 8)[1] for i in range(count)]
    assert count == 15 and set(controlled) <= {c['id'] for c in colliders}

    ph = next(o for o in main.objects if o['class_id'] == 55)
    phys = main.data[ph['offset']:ph['offset'] + ph['size']]
    assert len(phys) == 180 and struct.unpack_from('<i', phys, 48)[0] == 32
    layers = list(struct.unpack_from('<32I', phys, 52))
    assert all(bool(layers[i] & (1 << j)) == bool(layers[j] & (1 << i)) for i in range(32) for j in range(32))
    speed = struct.unpack_from('<f', raw(90571), 24)[0]
    zone_scale = struct.unpack_from('<f', raw(93984), 44)[0]
    assert speed == 1.25 and zone_scale == 1

    # Unity4 streamed animation, evaluated as cubic segments rather than fitted curves.
    clip = next(o for o in shared.objects if o['id'] == 452 and o['class_id'] == 74)
    clipraw = shared.data[clip['offset']:clip['offset'] + clip['size']]
    r = Reader(clipraw, '<')
    assert (r.aligned_string(), r.i32(), r.u8(), r.u8()) == ('anim_Zone003', 2, 0, 1)
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
    assert start == 0 and duration == 30 and not any(extra)
    r.read(r.i32() * 4); r.read(r.i32() * 8)
    flags = [r.u8() for _ in range(9)]; r.align()
    assert flags == [0, 0, 0, 0, 0, 0, 0, 1, 0]
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
    assert p == len(words) and ns == 49 and all(s and s[0]['time'] == 0 for s in streams)
    paths = collections.defaultdict(list)
    def path_walk(g, path):
        paths[zlib.crc32(path.encode()) & 0xffffffff].append((g, path))
        for child in children[transforms[g]['id']]:
            path_walk(child, path + ('/' if path else '') + gos[child]['name'])
    path_walk(30569, '')
    converted, pending, index = [], [], 0
    assert zlib.crc32(b'play') & 0xffffffff == 1586093754
    for b in bindings:
        assert len(paths[b['pathHash']]) == 1
        g, path = paths[b['pathHash']][0]
        n = {1:3,2:4,3:3}.get(b['attribute'],1) if b['classId'] == 4 else 1
        kind = b['attribute'] if b['classId'] == 4 else 0
        if b['classId'] == 114 and b['scriptId'] == 84 and b['attribute'] == 1586093754: kind = 5
        if b['classId'] == 1 and b['attribute'] == zlib.crc32(b'm_IsActive') & 0xffffffff: kind = 4
        record = dict(goId=g, path=path, kind=kind, channels=[dict(keys=s) for s in streams[index:index+n]])
        if kind: converted.append(record)
        else: pending.append(dict(**b, goId=g, path=path, firstChannel=index))
        index += n
    assert index == ns
    assert len(converted) == 16 and len(pending) == 7
    data = dict(format='ESCPLAY12', unityVersion=level.unity_version, duration=duration,
                speed=speed*zone_scale, zoneRoot=15787, rocketRoot=27907, rocketTarget=1005,
                viewpointTarget=20642, boundsCollider=81445, rocketColliders=controlled,
                rigidbody=rigidbody, layerMasks=layers, nodes=nodes, colliders=colliders,
                tracks=converted, pending=pending, clipSha256=hashlib.sha256(clipraw).hexdigest())
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(data, separators=(',',':')), encoding='utf8')
    print('Zone003:',duration,'animation seconds; speed',data['speed'])
    print('Restorable tracks:',len(converted),'pending:',len(pending),'native/source channels:',ns)
    print('Colliders:',dict(collections.Counter(c['kind'] for c in colliders)),'rocket:',len(controlled))
    print('Private output:',output)


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--project',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True)
    args=p.parse_args(); extract(args.project.resolve(),args.output.resolve())
