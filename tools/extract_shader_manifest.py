#!/usr/bin/env python3
"""Local-only original Material colour metadata extractor for Escape (2015).

Requires original sharedassets0/1.assets and unity4_reader.py from the private
recovery archive, plus Assets/StreamingAssets/escape_material_manifest.json.
Does not export or redistribute original ShaderLab source or game binaries.
Run: py -3 tools/extract_shader_manifest.py
"""
import argparse
import json
import math
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def color_property(blob, name):
    # Match the *entire* serialized property: _ColorMask can precede _Color.
    raw = name.encode("utf-8")
    marker = struct.pack("<I", len(raw)) + raw
    values = []
    cursor = 0
    while True:
        index = blob.find(marker, cursor)
        if index < 0:
            break
        start = (index + len(marker) + 3) & ~3
        if start + 16 <= len(blob):
            floats = struct.unpack_from("<4f", blob, start)
            if all(math.isfinite(x) and -1 <= x <= 8 for x in floats):
                values.append(list(floats))
        cursor = index + 1
    if len(values) > 1:
        raise ValueError("Ambiguous colour property " + name)
    return values[0] if values else None


def extract(project):
    roots = [project / "RecoverySource", project / "RecoveredEscapeUnity" / "RecoverySource"]
    source = next((p for p in roots if
                   (p / "Tools/unity4_reader.py").is_file() and
                   (p / "OriginalUnityData/sharedassets0.assets").is_file() and
                   (p / "OriginalUnityData/sharedassets1.assets").is_file()), None)
    if source is None:
        raise FileNotFoundError("Original APK data not found; use the private Stage 10 ZIP from the chat.")
    sys.path.insert(0, str(source / "Tools"))
    from unity4_reader import UnitySerialized

    stage9 = json.loads((project / "Assets/StreamingAssets/escape_material_manifest.json").read_text("utf-8"))
    previous = {(int(m["fileId"]), int(m["id"])): m for m in stage9["materials"]}
    result = []
    for filename, file_id in (("sharedassets0.assets", 2), ("sharedassets1.assets", 1)):
        asset = UnitySerialized(source / "OriginalUnityData" / filename)
        if asset.unity_version != "4.6.5f1":
            raise ValueError("Unexpected source Unity version " + asset.unity_version)
        for obj in asset.objects:
            if obj["class_id"] != 21:
                continue
            key = (file_id, int(obj["id"]))
            if key not in previous:
                raise ValueError("Original Material absent from Stage 9: " + str(key))
            raw = asset.data[obj["offset"]:obj["offset"] + obj["size"]]
            old = previous[key]
            result.append({
                "fileId": file_id, "id": int(obj["id"]), "name": old["name"],
                "shaderFileId": old["shaderFileId"], "shaderId": old["shaderId"],
                "color": color_property(raw, "_Color") or [1., 1., 1., 1.],
                "color2": color_property(raw, "_Color2") or [1., 1., 1., 1.],
            })
    if len(result) != 33 or len({(m["fileId"], m["id"]) for m in result}) != 33:
        raise ValueError("Expected 33 unique original Materials")
    black = next(m for m in result if (m["fileId"], m["id"]) == (1, 7))
    if black["shaderId"] != 357 or black["color"] != [0., 0., 0., 1.]:
        raise ValueError("Black.mat colour verification failed")
    if sum(m["shaderId"] == 359 for m in result) != 14:
        raise ValueError("Expected 14 background materials with inverted texture shader")
    path = project / "Assets/StreamingAssets/escape_shader_manifest.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"unityVersion": "4.6.5f1", "format": "ESCSHADER1",
                                "materials": result}, ensure_ascii=False, indent=2) + "\n",
                    encoding="utf-8")
    print("Verified original Materials: 33; negative texture shader: 14; Black.mat=(0,0,0,1)")
    print("Output (local-only):", path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=ROOT)
    args = parser.parse_args()
    try:
        extract(args.project.resolve())
    except (OSError, ValueError, KeyError, struct.error) as exc:
        print("ERROR:", exc, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
