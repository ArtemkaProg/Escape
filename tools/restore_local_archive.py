#!/usr/bin/env python3
"""Restore the private, user-provided recovery ZIP into this local Unity project.

This tool does not download or publish any copyrighted game resources.
"""

import argparse
import shutil
import stat
import sys
import zipfile
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parent.parent
PREFIX = "RecoveredEscapeUnity/"


def restore(archive: Path, dry_run: bool = False) -> tuple[int, int]:
    installed = skipped = 0
    with zipfile.ZipFile(archive) as src:
        names = src.namelist()
        if not any(n.startswith(PREFIX + "Assets/") for n in names):
            raise ValueError("Not the expected Escape recovery ZIP (missing Unity Assets/)")
        if PREFIX + "Assets/StreamingAssets/escape_hierarchy.json" not in names:
            raise ValueError("Missing escape_hierarchy.json; use the FULL recovery archive")

        for info in src.infolist():
            name = info.filename
            if not name.startswith(PREFIX) or info.is_dir():
                continue
            relative = PurePosixPath(name[len(PREFIX):])
            if not relative.parts or relative.is_absolute() or ".." in relative.parts:
                raise ValueError(f"Unsafe path inside ZIP: {name}")
            file_mode = info.external_attr >> 16
            if stat.S_IFMT(file_mode) == stat.S_IFLNK:
                raise ValueError(f"Unsupported symbolic link: {name}")
            target = ROOT.joinpath(*relative.parts)
            if target.exists():
                skipped += 1
                continue
            if not dry_run:
                target.parent.mkdir(parents=True, exist_ok=True)
                with src.open(info) as source, target.open("wb") as dest:
                    shutil.copyfileobj(source, dest)
            installed += 1
    return installed, skipped


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path, help="Local Escape_2015_Unity6_Recovery_Project_FULL.zip")
    parser.add_argument("--dry-run", action="store_true", help="Check the ZIP without writing files")
    args = parser.parse_args()
    try:
        installed, skipped = restore(args.archive, args.dry_run)
        verb = "Would install" if args.dry_run else "Installed"
        print(f"{verb} {installed} files; skipped {skipped} existing files")
        print("Original APK resources remain local and are excluded by .gitignore")
    except (OSError, ValueError, zipfile.BadZipFile, RuntimeError) as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
