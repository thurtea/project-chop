#!/usr/bin/env python3
"""Zips the release builds for GitHub releases.

Usage: python Tools/package_release.py [builds_folder] [version]

Looks for Builds/Release/<platform>/ folders (windows, linux, macos), adds PLAYTEST_README.txt to each and writes
Builds/Release/Chopper-<version>-<platform>.zip. Linux and macOS executables keep their executable bit inside the zip
so players can run them without chmod on most systems.
"""
import os
import stat
import sys
import zipfile

PLATFORMS = {"windows": "Windows", "linux": "Linux", "macos": "macOS"}
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def add_file(zf, path, arcname, executable):
    info = zipfile.ZipInfo.from_file(path, arcname)
    mode = 0o755 if executable else 0o644
    info.external_attr = (stat.S_IFREG | mode) << 16
    info.compress_type = zipfile.ZIP_DEFLATED
    with open(path, "rb") as handle:
        zf.writestr(info, handle.read())


def is_executable(platform, relative):
    name = relative.replace("\\", "/")
    if platform == "linux":
        return name.endswith(".x86_64")
    if platform == "macos":
        return "/Contents/MacOS/" in name
    return False


def main():
    builds = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Builds", "Release")
    version = sys.argv[2] if len(sys.argv) > 2 else "0.1.0"
    readme = os.path.join(ROOT, "Tools", "PLAYTEST_README.txt")
    made = []
    for key, label in PLATFORMS.items():
        folder = os.path.join(builds, key)
        if not os.path.isdir(folder):
            print(f"skip {label}: {folder} not found")
            continue
        out = os.path.join(builds, f"Chopper-{version}-{key}.zip")
        if os.path.exists(out):
            os.remove(out)
        with zipfile.ZipFile(out, "w") as zf:
            for dirpath, dirnames, filenames in os.walk(folder):
                # Burst debug symbols are large and only useful for crash analysis.
                dirnames[:] = [d for d in dirnames if "DoNotShip" not in d]
                for filename in filenames:
                    full = os.path.join(dirpath, filename)
                    relative = os.path.relpath(full, folder)
                    arc = os.path.join("Chopper", relative)
                    add_file(zf, full, arc, is_executable(key, arc))
            if os.path.exists(readme):
                add_file(zf, readme, "Chopper/PLAYTEST_README.txt", False)
        made.append(out)
        print(f"wrote {out} ({os.path.getsize(out) // (1024 * 1024)} MB)")
    if not made:
        print("nothing packaged")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
