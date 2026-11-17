#!/usr/bin/env python3
"""Escape 2015: decode original Unity ETC2_RGBA8 KTX textures into LOCAL PNGs.

Run from a cloned repo (Windows PowerShell / Git Bash):
    py -3 -m pip install Pillow
    py -3 tools/decode_etc2_stage5.py

No game data is uploaded. This reads KTX files in the previously extracted APK
recovery archive, decodes them with the included third-party ETCImagePlugin,
and updates the LOCAL ignored sprite manifest only after all textures succeed.

ETCImagePlugin by FluffyJay1:
https://github.com/FluffyJay1/ETCImagePlugin (see vendor/THIRD_PARTY_NOTICE.txt)
"""

import argparse
import json
import re
import struct
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parent.parent
KTX_MAGIC = b"\xabKTX 11\xbb\r\n\x1a\n"
GL_COMPRESSED_RGBA8_ETC2_EAC = 0x9278
TEXTURE_ID_FROM_FILENAME = re.compile(r"^sharedassets1_(\d{4})_.+\.ktx$")


def find_ktx_directory(project_root: Path) -> Path:
    options = [
        project_root / "RecoverySource" / "RawTextures",
        project_root / "RecoveredEscapeUnity" / "RecoverySource" / "RawTextures",
        project_root / "RawTextures",
    ]
    for option in options:
        if option.is_dir() and any(option.glob("sharedassets1_*.ktx")):
            return option
    raise FileNotFoundError(
        "The 12 original ETC2 KTX files are not present. Look for "
        "RecoveredEscapeUnity/RecoverySource/RawTextures in your original "
        "Escape_2015_Unity6_Recovery_Project_FULL.zip, and extract that "
        "directory to the project root. Checked:\n" + "\n".join(map(str, options))
    )


def parse_ktx(path: Path):
    contents = path.read_bytes()
    if len(contents) < 68 or contents[:12] != KTX_MAGIC:
        raise ValueError(f"{path.name}: invalid KTX1 header")
    endianness = struct.unpack_from("<I", contents, 12)[0]
    if endianness != 0x04030201:
        raise ValueError(f"{path.name}: only little-endian KTX1 is supported")
    (gl_type, gl_type_size, gl_format, gl_internal, gl_base, width, height,
     depth, array_count, faces, mipmaps, meta_size) = struct.unpack_from("<12I", contents, 16)
    if (gl_type, gl_type_size, gl_format) != (0, 1, 0) or \
       (gl_internal, gl_base) != (GL_COMPRESSED_RGBA8_ETC2_EAC, 0x1908) or \
       (depth, array_count, faces, mipmaps) != (0, 0, 1, 1):
        raise ValueError(f"{path.name}: unsupported KTX texture format/layout")
    if not (0 < width <= 4096 and 0 < height <= 4096):
        raise ValueError(f"{path.name}: unreasonable dimensions: {width} x {height}")
    header_end = 64 + meta_size
    if len(contents) < header_end + 4:
        raise ValueError(f"{path.name}: truncated KTX key-values")
    encoded_bytes = struct.unpack_from("<I", contents, header_end)[0]
    required_bytes = ((width + 3) // 4) * ((height + 3) // 4) * 16
    if encoded_bytes != required_bytes:
        raise ValueError(
            f"{path.name}: expected {required_bytes} ETC2 bytes; got {encoded_bytes}"
        )
    start = header_end + 4
    if start + encoded_bytes > len(contents):
        raise ValueError(f"{path.name}: compressed image is truncated")
    return width, height, contents[start:start + encoded_bytes]


def convert(project_root: Path, dry_run: bool = False, force: bool = False):
    project_root = project_root.resolve()
    ktx_directory = find_ktx_directory(project_root)
    manifest_path = (project_root / "Assets" / "StreamingAssets" /
                     "escape_sprite_manifest.json")
    if not manifest_path.is_file():
        raise FileNotFoundError("Missing " + str(manifest_path))
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    sprites = manifest.get("sprites", [])
    renderers = manifest.get("renderers", [])
    if not isinstance(sprites, list) or not isinstance(renderers, list):
        raise ValueError("Invalid sprite manifest structure")
    sources = {}
    total_bytes = 0
    for path in sorted(ktx_directory.glob("*.ktx")):
        match = TEXTURE_ID_FROM_FILENAME.fullmatch(path.name)
        if match is None:
            continue
        texture_id = int(match.group(1))
        if texture_id in sources:
            raise ValueError(f"Repeated texture ID {texture_id}")
        width, height, raw = parse_ktx(path)
        sources[texture_id] = (path, width, height, raw)
        total_bytes += len(raw)

    needed = {int(s["textureId"]) for s in sprites
              if s.get("sourceStatus") == "compressed_etc2_ktx"}
    missing = needed - sources.keys()
    if missing:
        raise ValueError("Required ETC2 texture IDs are missing: " + str(sorted(missing)))
    print(f"KTX directory: {ktx_directory}")
    print(f"Validated {len(sources)} ETC2_RGBA8 textures ({total_bytes} encoded bytes)")
    print(f"Texture IDs used by compressed sprites: {sorted(needed)}")
    if dry_run:
        print("Dry-run: KTX headers and sprite references verified; no files changed.")
        return

    try:
        from PIL import Image, ImageOps
    except ImportError as exc:
        raise RuntimeError(
            "Pillow is required. Install once using: py -3 -m pip install Pillow"
        ) from exc
    try:
        import texture2ddecoder  # fast, optional C/C++ decoder
        print("ETC2 backend: texture2ddecoder (native, fast)")
    except ImportError:
        texture2ddecoder = None
        print("ETC2 backend: ETCImagePlugin (pure Python, considerably slower)")

    if texture2ddecoder is None:
        vendor = PROJECT_ROOT / "tools" / "vendor"
        sys.path.insert(0, str(vendor))
        try:
            import ETCImagePlugin  # author: FluffyJay1 (third-party)
            from ETCImagePlugin import TextureType
        except ImportError as exc:
            raise RuntimeError(
                "Missing tools/vendor/ETCImagePlugin.py. Run git pull origin main."
            ) from exc

    output_dir = project_root / "Assets" / "RecoveredTextures"
    output_dir.mkdir(parents=True, exist_ok=True)
    outputs = {}
    for tid, (path, width, height, blocks) in sorted(sources.items()):
        output_name = path.stem + ".png"
        relative = "Assets/RecoveredTextures/" + output_name
        target = output_dir / output_name
        if target.exists() and not force:
            with Image.open(target) as already:
                if already.size != (width, height):
                    raise ValueError(f"Existing PNG has wrong dimensions: {target}")
                already.verify()
            print("Keeping existing:", output_name)
        else:
            # ETC blocks are in Unity's bottom-left raster order.
            # The existing extracted PNGs were vertically flipped too.
            if texture2ddecoder is not None:
                pixels = texture2ddecoder.decode_etc2a8(blocks, width, height)
                if len(pixels) != width * height * 4:
                    raise ValueError("Native decoder returned wrong pixel count")
                img = Image.frombytes("RGBA", (width, height), pixels, "raw", "BGRA")
            else:
                img = Image.frombytes(
                    "RGBA", (width, height), blocks, "ETC",
                    TextureType.ETC2PACKAGE_RGBA_NO_MIPMAPS
                )
            img = ImageOps.flip(img)
            temporary = target.with_name(target.name + ".tmp")
            try:
                img.save(temporary, format="PNG", optimize=True)
                with Image.open(temporary) as verified:
                    verified.verify()
                temporary.replace(target)
            finally:
                temporary.unlink(missing_ok=True)
            print("Decoded:", output_name, f"({width}x{height})")
        outputs[tid] = relative

    changed = 0
    for sprite in sprites:
        tid = int(sprite["textureId"])
        if sprite.get("sourceStatus") == "compressed_etc2_ktx" and tid in outputs:
            sprite["texturePath"] = outputs[tid]
            sprite["sourceStatus"] = "decoded_etc2_png"
            changed += 1

    temp_manifest = manifest_path.with_name(manifest_path.name + ".tmp")
    temp_manifest.write_text(
        json.dumps(manifest, ensure_ascii=False, separators=(",", ":")),
        encoding="utf-8"
    )
    temp_manifest.replace(manifest_path)

    sprite_map = {int(s["id"]): s for s in sprites}
    now_supported = sum(
        bool(sprite_map.get(int(r["spriteId"]), {}).get("texturePath"))
        for r in renderers
    )
    print(f"Completed: {len(sources)} ETC2 textures; {changed} sprite references updated")
    print(f"Expected usable SpriteRenderers in the next Unity pass: "
          f"{now_supported} / {len(renderers)}")
    print("Open Unity > Escape Recovery > 4 - Restore ETC2 sprites (visual pass 2)")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=PROJECT_ROOT)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--force", action="store_true", help="Re-decode existing outputs")
    args = parser.parse_args()
    try:
        convert(args.project, dry_run=args.dry_run, force=args.force)
    except Exception as exc:
        print("ERROR:", exc, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
