#!/usr/bin/env python3
"""Export private camera/sorting metadata from the local Escape 2015 APK files.

This is a repeatable helper; it does NOT publish assets or original code.
Run from the repository root:
    py -3 tools/extract_camera_manifest.py
Or use the ready-made Stage 6 metadata ZIP from the conversation.
"""
import argparse
import collections
import json
import math
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def original_level(root):
    for rel in (
        "RecoverySource/OriginalUnityData/level0",
        "RecoveredEscapeUnity/RecoverySource/OriginalUnityData/level0",
    ):
        path = root / rel
        if path.is_file():
            reader = path.parent.parent / "Tools" / "unity4_reader.py"
            if reader.is_file():
                sys.path.insert(0, str(reader.parent))
                from unity4_reader import UnitySerialized
                return UnitySerialized(path)
            raise FileNotFoundError(
                "Original level0 found, but RecoverySource/Tools/unity4_reader.py "
                "was not extracted. Use the Stage 6 metadata ZIP."
            )
    raise FileNotFoundError(
        "No original level0 found. Extract the full local recovery archive "
        "or use the Stage 6 metadata ZIP provided in the conversation."
    )


def inspect_camera(asset, entry, gos, transforms, by_id):
    if entry["size"] != 104:
        raise ValueError("Unexpected Unity 4.6 Camera size for " + str(entry["id"]))
    data = asset.data
    off = entry["offset"]
    fileid, goid = struct.unpack_from("<ii", data, off)
    if fileid != 0 or goid not in gos:
        raise ValueError("Camera GameObject pointer is invalid")
    enabled = data[off + 8]
    clear = struct.unpack_from("<i", data, off + 12)[0]
    bg = list(struct.unpack_from("<4f", data, off + 16))
    viewport = list(struct.unpack_from("<4f", data, off + 32))
    near, far, fov = struct.unpack_from("<3f", data, off + 48)
    ortho = struct.unpack_from("<i", data, off + 60)[0]
    size, depth = struct.unpack_from("<2f", data, off + 64)
    mask, rendering = struct.unpack_from("<ii", data, off + 72)

    numbers = bg + viewport + [near, far, fov, size, depth]
    if (enabled not in (0, 1) or clear not in (1, 2, 3, 4)
        or ortho not in (0, 1) or not all(math.isfinite(v) for v in numbers)
        or not (0 < near < far < 1_000_000)
        or not (0 < fov < 179) or not (0 < size < 100_000)):
        raise ValueError("Invalid Camera data at " + str(entry["id"]))

    chain = []
    current = goid
    while True:
        chain.append(gos[current]["name"])
        parent = transforms[current]["parentId"]
        if not parent:
            break
        if parent not in by_id:
            raise ValueError("Broken camera Transform hierarchy")
        current = by_id[parent]["goId"]
    return {
        "id": int(entry["id"]), "goId": goid, "name": gos[goid]["name"],
        "path": "/".join(reversed(chain)), "enabled": bool(enabled),
        "clearFlags": clear, "backgroundColor": bg,
        "normalizedViewport": viewport, "nearClip": near, "farClip": far,
        "fieldOfView": fov, "orthographic": bool(ortho),
        "orthographicSize": size, "depth": depth,
        "cullingMask": mask, "renderingPath": rendering,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=ROOT)
    args = parser.parse_args()
    root = args.project.resolve()
    try:
        scene = json.loads((root / "Assets/StreamingAssets/escape_hierarchy.json")
                           .read_text(encoding="utf-8"))
        sprite = json.loads((root / "Assets/StreamingAssets/escape_sprite_manifest.json")
                            .read_text(encoding="utf-8"))
        gos = {int(g["id"]): g for g in scene["gameObjects"]}
        transforms = {int(t["goId"]): t for t in scene["transforms"]}
        by_id = {int(t["id"]): t for t in scene["transforms"]}
        level = original_level(root)
        if level.unity_version != "4.6.5f1":
            raise ValueError("Unexpected Unity version " + level.unity_version)
        cameras = [inspect_camera(level, e, gos, transforms, by_id)
                   for e in level.objects if e["class_id"] == 20]
        if len(cameras) != 8 or len({c["goId"] for c in cameras}) != 8:
            raise ValueError("Expected 8 different Camera objects in original level0")

        counter = collections.Counter(int(r["sortingLayerId"]) for r in sprite["renderers"])
        if len(counter) != 5 or 0 not in counter:
            raise ValueError("Unexpected Sorting Layer ID count in sprite manifest")
        sorted_ids = [0] + [x for x in sorted(counter) if x != 0]
        layers = [
            {"id": sid, "name": "Default" if sid == 0 else f"RecoveredLayer_{i:02d}",
             "rendererCount": counter[sid]}
            for i, sid in enumerate(sorted_ids)
        ]
        result = {
            "unityVersion": level.unity_version, "sourceFile": "level0",
            "notes": "Original numeric IDs and camera values; custom Sorting Layer names/order are provisional.",
            "cameras": cameras, "sortingLayers": layers,
        }
        out = root / "Assets/StreamingAssets/escape_camera_manifest.json"
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n",
                       encoding="utf-8")
        print("Restored", len(cameras), "original camera records")
        print("Sorting Layer IDs:", [(x["id"], x["rendererCount"]) for x in layers])
        print("Written:", out)
    except (OSError, ValueError, KeyError, ImportError) as exc:
        print("ERROR:", exc, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
