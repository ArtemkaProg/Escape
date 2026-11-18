"""Recover local ASTC rocket art and serialized movement settings.

Requires Pillow, texture2ddecoder and the private recovery archive's Tools.
Never trusts the old KTX format label: reads the original Texture2D instead.
Outputs are private derivatives; original PNGs, crops and scenes are untouched.
"""
import argparse
import hashlib
import io
import json
import struct
import sys
from pathlib import Path


def recover(project):
    source = next((project / p for p in ('RecoverySource', 'RecoveredEscapeUnity/RecoverySource')
                   if (project / p / 'Tools/unity4_reader.py').is_file()), None)
    if source is None:
        raise ValueError('Private recovery Tools and OriginalUnityData are required')
    sys.path.insert(0, str(source / 'Tools'))
    from unity4_reader import UnitySerialized, Reader
    from dotnet_inspect import Metadata
    from PIL import Image, ImageOps
    import texture2ddecoder

    # Confirm the enum in this APK's engine, independent of current Unity docs.
    md = Metadata(source / 'OriginalUnityData/Managed/UnityEngine.dll')
    types = md.tables[2]
    i = next(i for i, t in enumerate(types) if md.text(t[1]) == 'TextureFormat')
    field = next(j for j in range(types[i][4], types[i + 1][4])
                 if md.text(md.tables[4][j - 1][1]) == 'ASTC_RGBA_4x4')
    constant = next(c for c in md.tables[11] if c[1] == field << 2)
    astc_format = struct.unpack('<i', md.blob_at(constant[2]))[0]
    if astc_format != 54:
        raise ValueError('Unexpected original TextureFormat enum')

    sm = json.loads((project / 'Assets/StreamingAssets/escape_sprite_manifest.json').read_text('utf8'))
    gm = json.loads((project / 'Assets/StreamingAssets/escape_sprite_geometry.json').read_text('utf8'))
    geometry = {s['id']: s for s in gm['sprites']}
    rocket = [s for s in sm['sprites'] if s['name'].startswith('Rocket') and s['textureId'] == 32]
    if len(rocket) != 16:
        raise ValueError('Unexpected rocket atlas coverage')
    shared = UnitySerialized(source / 'OriginalUnityData/sharedassets1.assets')
    obj = next(o for o in shared.objects if o['class_id'] == 28 and o['id'] == 32)
    r = Reader(shared.data, '<'); r.at(obj['offset'])
    name = r.aligned_string()
    w, h, size, fmt = struct.unpack_from('<4I', shared.data, r.p)
    length = struct.unpack_from('<I', shared.data, r.p + 52)[0]
    start = r.p + 56
    required = ((w + 3) // 4) * ((h + 3) // 4) * 16
    if (w, h, fmt) != (1024, 512, astc_format) or length < required or start + length != obj['offset'] + obj['size']:
        raise ValueError('Unexpected source ASTC texture layout')
    blocks = shared.data[start:start + required]
    pixels = texture2ddecoder.decode_astc(blocks, w, h, 4, 4)
    if len(pixels) != w * h * 4:
        raise ValueError('Wrong decoded size')
    image = ImageOps.flip(Image.frombytes('RGBA', (w, h), pixels, 'raw', 'BGRA'))

    level = UnitySerialized(source / 'OriginalUnityData/level0')
    component = next(o for o in level.objects if o['id'] == 99451 and o['class_id'] == 114)
    raw = level.data[component['offset']:component['offset'] + component['size']]
    # Native MonoBehaviour header + three serialized PPtrs + five public floats.
    if len(raw) != 68 or struct.unpack_from('<2i', raw, 12) != (2, 364):
        raise ValueError('Movement component does not reference the expected script')
    code = Metadata(source / 'OriginalUnityData/Managed/Assembly-CSharp.dll')
    ts = code.tables[2]
    i = next(i for i, t in enumerate(ts) if code.text(t[1]) == 'RocketMovementController')
    public = [code.text(code.tables[4][j - 1][1]) for j in range(ts[i][4], ts[i + 1][4])
              if code.tables[4][j - 1][0] & 7 == 6]
    names = ['thrustStrength', 'gravity', 'physicScale', 'angularAmplitudeReduction', 'rotationInertia']
    if public != ['rocketInputController', 'rocketRigidBody', 'rocketTargetGetter'] + names:
        raise ValueError('Movement field layout mismatch')
    values = dict(zip(names, struct.unpack_from('<5f', raw, 48)))
    if any(not 0 < v < 100 for v in values.values()):
        raise ValueError('Invalid physics settings')

    out = project / 'Assets/RecoveredRocketPass11Local'
    out.mkdir(parents=True, exist_ok=True)
    def save_png(target, img):
        buf = io.BytesIO(); img.save(buf, format='PNG'); data = buf.getvalue()
        if target.exists() and target.read_bytes() != data:
            raise ValueError('Refusing to replace a different derivative: ' + str(target))
        if not target.exists():
            target.write_bytes(data)
    save_png(out / 'rocket_atlas_astc.png', image)
    sprites = []
    for s in rocket:
        x, y, sw, sh = s['textureRect']
        if any(int(v) != v for v in (x, y, sw, sh)) or not (0 <= x < x + sw <= w and 0 <= y < y + sh <= h):
            raise ValueError('Invalid crop')
        crop = image.crop((int(x), int(h-y-sh), int(x+sw), int(h-y)))
        filename = 'escape_sprite_' + str(s['id']) + '.png'
        save_png(out / filename, crop)
        sprites.append(dict(id=s['id'], name=s['name'], png='Assets/RecoveredRocketPass11Local/' + filename,
                            ppu=s['pixelsPerUnit'], pivot=geometry[s['id']]['pivot']))
    result = dict(format='ESCROCKET11', unityVersion=level.unity_version,
                  sourceTextureFormat=fmt, sourceTextureFormatName='ASTC_RGBA_4x4',
                  sourceBlockSha256=hashlib.sha256(blocks).hexdigest(),
                  movementComponentSha256=hashlib.sha256(raw).hexdigest(),
                  movement=values, sprites=sprites)
    (out / 'recovery.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf8')
    print('Verified original ASTC format, decoded', len(sprites), 'rocket sprites.')
    print('Serialized movement:', values)
    print('Private output:', out)


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--project', type=Path, required=True)
    recover(p.parse_args().project.resolve())
