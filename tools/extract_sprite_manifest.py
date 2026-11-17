#!/usr/bin/env python3
"""Extract Sprite + SpriteRenderer mapping from the user's PRIVATE Escape APK data.

Requires the local Unity 4.6.5f1 serialized resources already unpacked from the
APK through the recovery archive. This script publishes NO textures or game code.
Run in the repository root:
    py -3 tools/extract_sprite_manifest.py
"""
import collections
import csv
import json
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "RecoverySource" / "Tools"))
try:
    from unity4_reader import UnitySerialized, Reader
except ImportError as exc:
    raise SystemExit(
        "Missing RecoverySource/Tools/unity4_reader.py. "
        "Import Escape_2015_Unity6_Recovery_Project_FULL.zip locally first."
    ) from exc

original = ROOT / "RecoverySource" / "OriginalUnityData"
level = UnitySerialized(original / "level0")
shared = UnitySerialized(original / "sharedassets1.assets")

texture_records = {
    int(rec["path_id"]): rec
    for rec in csv.DictReader((ROOT / "Reports" / "textures.csv").open(encoding="utf-8"))
    if rec["file"] == "sharedassets1.assets"
}
sprites = []
for obj in shared.objects:
    if obj["class_id"] != 213:
        continue
    raw = shared.data[obj["offset"]:obj["offset"] + obj["size"]]
    reader = Reader(raw, "<")
    name = reader.aligned_string()
    start = reader.p
    rect = struct.unpack_from("<4f", raw, start)
    file_id, texture_id = struct.unpack_from("<ii", raw, start + 48)
    pixels_per_unit = struct.unpack_from("<f", raw, start + 40)[0]
    texture_rect = struct.unpack_from("<4f", raw, len(raw) - 44)
    texture = texture_records.get(texture_id)
    if file_id != 0 or texture is None:
        raise ValueError("Unresolved texture for " + name)
    if not (0 < pixels_per_unit <= 10000):
        raise ValueError("Invalid pixels-per-unit for " + name)
    x, y, w, h = texture_rect
    tw, th = int(texture["width"]), int(texture["height"])
    if min(w, h) <= 0 or x < -0.1 or y < -0.1 or x + w > tw + 1 or y + h > th + 1:
        raise ValueError("Invalid crop for " + name + ": " + str(texture_rect))
    sprites.append({
        "id": obj["id"],
        "name": name,
        "textureId": texture_id,
        "texturePath": texture["output"] if texture["status"] == "decoded_png" else "",
        "sourceStatus": texture["status"],
        "rect": [round(v, 4) for v in rect],
        "textureRect": [round(v, 4) for v in texture_rect],
        "pixelsPerUnit": pixels_per_unit,
    })

known_ids = {item["id"] for item in sprites}
renderers = []
for obj in level.objects:
    if obj["class_id"] != 212:
        continue
    raw = level.data[obj["offset"]:obj["offset"] + obj["size"]]
    if len(raw) != 96:
        raise ValueError("Unexpected SpriteRenderer size: " + str(len(raw)))
    go_file, go_id = struct.unpack_from("<ii", raw, 0)
    sprite_file, sprite_id = struct.unpack_from("<ii", raw, 72)
    if go_file != 0 or sprite_file not in (0, 1):
        raise ValueError("Invalid SpriteRenderer pointer")
    if sprite_file == 1 and sprite_id and sprite_id not in known_ids:
        raise ValueError("Unresolved sprite ID: " + str(sprite_id))
    renderers.append({
        "id": obj["id"],
        "goId": go_id,
        "spriteId": sprite_id if sprite_file == 1 else 0,
        "enabled": bool(raw[8]),
        "sortingLayerId": struct.unpack_from("<i", raw, 64)[0],
        "sortingOrder": struct.unpack_from("<h", raw, 68)[0],
        "color": [round(float(v), 6) for v in struct.unpack_from("<4f", raw, 80)],
    })

out = ROOT / "Assets" / "StreamingAssets" / "escape_sprite_manifest.json"
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(
    json.dumps(
        {"unityVersion": level.unity_version, "source": "level0 + sharedassets1.assets",
         "sprites": sprites, "renderers": renderers},
        ensure_ascii=False, separators=(",", ":")
    ),
    encoding="utf-8",
)

by_id = {s["id"]: s for s in sprites}
coverage = collections.Counter(
    by_id[r["spriteId"]]["sourceStatus"] if r["spriteId"] in by_id else "no_sprite"
    for r in renderers
)
print("Sprite definitions:", len(sprites))
print("Original SpriteRenderer components:", len(renderers))
print("Renderer coverage:", dict(coverage))
print("Local output:", out)
