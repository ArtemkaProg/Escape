#!/usr/bin/env python3
"""Extract original Sprite pivots and custom polygon geometry from LOCAL APK data.

Run in the Escape repo:
    py -3 tools/extract_sprite_geometry.py

Input: Unity 4.6.5f1 sharedassets1.assets, plus local sprite manifest from
Stage 2. Output: Assets/StreamingAssets/escape_sprite_geometry.json

This script is independently authored; it does not publish original sprites.
"""
import collections
import json
import math
import struct
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parent.parent


def main() -> int:
    locations = [
        ROOT / "RecoverySource",
        ROOT / "RecoveredEscapeUnity" / "RecoverySource",
    ]
    source = next(
        (p for p in locations
         if (p / "OriginalUnityData" / "sharedassets1.assets").is_file()
         and (p / "Tools" / "unity4_reader.py").is_file()),
        None
    )
    if source is None:
        print("Missing local RecoverySource/OriginalUnityData/sharedassets1.assets "
              "or Tools/unity4_reader.py; use the Stage 7 ZIP from the chat.",
              file=sys.stderr)
        return 1

    try:
        sys.path.insert(0, str(source / "Tools"))
        from unity4_reader import Reader, UnitySerialized

        data = UnitySerialized(
            source / "OriginalUnityData" / "sharedassets1.assets")
        if data.unity_version != "4.6.5f1":
            raise ValueError("Wrong source Unity version")

        manifest = json.loads(
            (ROOT / "Assets" / "StreamingAssets" /
             "escape_sprite_manifest.json").read_text(encoding="utf-8"))
        source_sprites = {int(s["id"]): s for s in manifest["sprites"]}
        uses = collections.Counter(int(x["spriteId"])
                                   for x in manifest["renderers"])
        converted = []

        for entry in data.objects:
            if entry["class_id"] != 213:
                continue
            raw = data.data[entry["offset"]:entry["offset"] + entry["size"]]
            reader = Reader(raw, "<")
            name = reader.aligned_string()
            off = reader.p
            rect = struct.unpack_from("<4f", raw, off)
            ppu = struct.unpack_from("<f", raw, off + 40)[0]
            texture_file, texture_id = struct.unpack_from("<2i", raw, off + 48)
            if texture_file != 0 or texture_id != source_sprites[entry["id"]]["textureId"]:
                raise ValueError("Wrong texture reference for " + name)

            vertex_count = struct.unpack_from("<I", raw, off + 56)[0]
            if not 3 <= vertex_count <= 10000:
                raise ValueError("Invalid vertex count for " + name)
            vert_start = off + 60
            vertices = [
                struct.unpack_from("<3f", raw, vert_start + 12 * i)
                for i in range(vertex_count)
            ]
            triangle_offset = vert_start + vertex_count * 12
            index_count = struct.unpack_from("<I", raw, triangle_offset)[0]
            if index_count % 3 or index_count > 40000:
                raise ValueError("Bad triangle count for " + name)
            triangles = list(struct.unpack_from(
                "<%dH" % index_count, raw, triangle_offset + 4))
            if max(triangles, default=-1) >= vertex_count:
                raise ValueError("Out-of-range triangle for " + name)
            expected_tail = (triangle_offset + 4 + index_count * 2 + 3) & ~3
            if expected_tail != len(raw) - 44:
                raise ValueError("Unexpected Unity 4 Sprite structure for " + name)

            texture_rect = struct.unpack_from("<4f", raw, len(raw) - 44)
            settings = struct.unpack_from("<I", raw, len(raw) - 20)[0]
            transform = struct.unpack_from("<4f", raw, len(raw) - 16)
            w, h = round(texture_rect[2]), round(texture_rect[3])
            if (w < 1 or h < 1 or not math.isfinite(ppu) or ppu <= 0 or
                not all(math.isfinite(n) for n in transform)):
                raise ValueError("Invalid sprite numeric fields for " + name)
            pivot = [(transform[1] - texture_rect[0]) / w,
                     (transform[3] - texture_rect[1]) / h]
            if not all(-.05 <= x <= 1.05 for x in pivot):
                raise ValueError("Sprite pivot outside texture for " + name)

            item = dict(
                id=entry["id"], name=name, textureId=texture_id, ppu=ppu,
                vertexCount=vertex_count, triangleCount=index_count // 3,
                pivot=pivot, uvTransform=list(transform),
                textureRect=list(texture_rect), originalRect=list(rect),
                settingsRaw=settings, instances=uses[entry["id"]],
            )

            if vertex_count > 4:
                item["vertices"] = [v for x, y, z in vertices for v in (x, y)]
                item["triangles"] = triangles
                item["uv"] = [
                    coord
                    for x, y, z in vertices
                    for coord in (
                        (transform[0] * x + transform[1] - texture_rect[0]) / w,
                        (transform[2] * y + transform[3] - texture_rect[1]) / h,
                    )
                ]
                if any(not (-.02 <= coord <= 1.02) for coord in item["uv"]):
                    raise ValueError("Sprite UV outside crop for " + name)
            converted.append(item)

        if len(converted) != 89:
            raise ValueError("Expected 89 Sprite assets, found " + str(len(converted)))
        out = ROOT / "Assets" / "StreamingAssets" / "escape_sprite_geometry.json"
        out.write_text(json.dumps({
            "unityVersion": "4.6.5f1",
            "source": "sharedassets1.assets",
            "sprites": converted,
        }, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
        polygons = [x for x in converted if x["vertexCount"] > 4]
        unusual = [x for x in converted if
                   abs(x["pivot"][0] - .5) > .03 or
                   abs(x["pivot"][1] - .5) > .03]
        print("Extracted", len(converted), "sprites;", len(polygons),
              "polygon definitions,", sum(x["instances"] for x in polygons),
              "polygon SpriteRenderer instances.")
        print("Noncentral pivot SpriteRenderer instances:",
              sum(x["instances"] for x in unusual))
        print("Created local JSON:", out)
        return 0
    except (OSError, KeyError, IndexError, ValueError, struct.error) as exc:
        print("ERROR:", exc, file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
