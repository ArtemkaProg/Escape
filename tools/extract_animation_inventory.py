#!/usr/bin/env python3
"""Inspect the ORIGINAL Escape (2015) AnimationClip/Animator data locally.

Run in the Escape project root:
    py -3 tools/extract_animation_inventory.py
Writes Assets/StreamingAssets/escape_animation_inventory.json (git-ignored).
This tool extracts only metadata and references. It does not convert animation
curves or reconstruct the game's original runtime behaviour.
"""
import argparse
import csv
import json
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCRIPT_CLASSES = [
    "RocketChips.RocketInputController",
    "RocketChips.RocketMovementController",
    "RocketChips.RocketFuelController",
    "RocketChips.RocketAnimatorController",
    "RocketChips.CameraManager",
    "RocketChips.Rocket",
]


def main(project):
    source = next(
        (p for p in [project / "RecoverySource",
                     project / "RecoveredEscapeUnity" / "RecoverySource"]
         if all((p / f).is_file() for f in (
             "Tools/unity4_reader.py",
             "OriginalUnityData/level0",
             "OriginalUnityData/sharedassets0.assets",
             "OriginalUnityData/sharedassets1.assets"))),
        None)
    if source is None:
        raise FileNotFoundError("Missing private Unity4 original files. "
                                "Extract the complete local Escape recovery archive.")

    sys.path.insert(0, str(source / "Tools"))
    from unity4_reader import Reader, UnitySerialized

    scene = json.loads(
        (project / "Assets/StreamingAssets/escape_hierarchy.json")
        .read_text(encoding="utf-8"))
    if scene.get("unityVersion") != "4.6.5f1":
        raise ValueError("Unexpected source hierarchy")
    gos = {int(g["id"]): g for g in scene["gameObjects"]}
    transforms = {int(t["goId"]): t for t in scene["transforms"]}
    transform_owner = {int(t["id"]): int(t["goId"]) for t in scene["transforms"]}
    if len(gos) != 34993 or len(transforms) != 34993:
        raise ValueError("Original GameObject count mismatch")

    def go_path(goid):
        result, seen = [], set()
        while goid:
            if goid in seen or goid not in gos:
                raise ValueError("Invalid Animator object hierarchy")
            seen.add(goid)
            result.append(gos[goid]["name"])
            parent = int(transforms[goid]["parentId"])
            goid = transform_owner[parent] if parent else 0
        return "/".join(reversed(result))

    clips, controllers, controller_by_id = [], [], {}
    for filename, fid in (("sharedassets0.assets", 2),
                          ("sharedassets1.assets", 1)):
        asset = UnitySerialized(source / "OriginalUnityData" / filename)
        if asset.unity_version != "4.6.5f1":
            raise ValueError("Unexpected Unity version in " + filename)
        for ob in asset.objects:
            if ob["class_id"] not in (74, 91):
                continue
            raw = asset.data[ob["offset"]:ob["offset"] + ob["size"]]
            name = Reader(raw, "<").aligned_string()
            if not name:
                raise ValueError("Empty animation asset name")
            item = {"fileId": fid, "id": int(ob["id"]),
                    "name": name, "serializedBytes": ob["size"]}
            if ob["class_id"] == 74:
                clips.append(item)
            else:
                controllers.append(item)
                controller_by_id[(fid, ob["id"])] = item

    asset = UnitySerialized(source / "OriginalUnityData/level0")
    if asset.unity_version != "4.6.5f1":
        raise ValueError("Unexpected Unity version in level0")
    animators = []
    for ob in asset.objects:
        if ob["class_id"] != 95:
            continue
        raw = asset.data[ob["offset"]:ob["offset"] + ob["size"]]
        if len(raw) != 42:
            raise ValueError("Unexpected Animator record size")
        go_file, goid = struct.unpack_from("<ii", raw, 0)
        avatar = struct.unpack_from("<ii", raw, 12)
        controller = struct.unpack_from("<ii", raw, 20)
        if go_file != 0 or goid not in gos or raw[8] not in (0, 1):
            raise ValueError("Invalid Animator GameObject pointer")
        if controller != (0, 0) and controller not in controller_by_id:
            raise ValueError("Unresolved AnimatorController " + str(controller))
        animators.append({
            "id": int(ob["id"]),
            "goId": goid, "goName": gos[goid]["name"],
            "goPath": go_path(goid),
            "enabled": bool(raw[8]),
            "goOriginallyActive": bool(gos[goid]["active"]),
            "avatarFileId": avatar[0], "avatarId": avatar[1],
            "controllerFileId": controller[0], "controllerId": controller[1],
            "controllerName": controller_by_id.get(controller, {}).get("name", "")
        })

    if (len(clips), len(controllers), len(animators)) != (152, 108, 1408):
        raise ValueError("Animation asset counts differ from verified APK")

    method_lists = {k: [] for k in SCRIPT_CLASSES}
    path = source / "DisassembledIL/Assembly-CSharp.methods.csv"
    if path.is_file():
        with path.open(encoding="utf-8-sig", newline="") as stream:
            for row in csv.DictReader(stream):
                k = row.get("declaring_type", "")
                if k in method_lists:
                    method_lists[k].append(row["name"])

    data = {
        "unityVersion": "4.6.5f1", "format": "ESCANIM1",
        "source": "level0, sharedassets0.assets, sharedassets1.assets",
        "scope": "Original asset/pointer inventory only; curves and state machines not converted.",
        "animationClips": clips, "animatorControllers": controllers,
        "animators": animators,
        "rocketControlMethodNames": [
            {"className": k, "methodNames": v} for k, v in method_lists.items()
        ],
    }
    dest = project / "Assets/StreamingAssets/escape_animation_inventory.json"
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n",
                    encoding="utf-8")
    linked = sum(a["controllerId"] != 0 for a in animators)
    print(f"Verified: {len(clips)} clips, {len(controllers)} controllers, "
          f"{len(animators)} scene Animators ({linked} linked, "
          f"{len(animators)-linked} without controller)")
    print("Local JSON:", dest)
    print("NOTE: this does not yet restore animation curves, physics or gameplay.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=ROOT)
    args = parser.parse_args()
    try:
        main(args.project.resolve())
    except (OSError, ValueError, KeyError, IndexError, struct.error) as exc:
        print("ERROR:", exc, file=sys.stderr)
        sys.exit(1)
