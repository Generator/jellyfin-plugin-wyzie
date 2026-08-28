#!/usr/bin/env python3
"""Build and package Wyzie Subtitles releases for Jellyfin 12.0+.

Mirrors the jellyfin-plugin-extractsubs packaging model: a single Jellyfin
12.0 target, a zip containing meta.json + the plugin DLLs, and no in-repo
manifest.json (the jellyfin-plugin-repo-action regenerates the catalog on the
gh-pages branch from the GitHub release).

Usage:
    python3 scripts/package.py --version 1.0.5 --repo owner/repo
"""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
ARTIFACTS = ROOT / "artifacts"
CSPROJ = ROOT / "src/Jellyfin.Plugin.Wyzie" / "Jellyfin.Plugin.Wyzie.csproj"

PLUGIN_GUID = "b2c9f7a0-2d4e-4b8f-9a1c-7e3d4c5a6b70"
PLUGIN_NAME = "Wyzie Subtitles"
PLUGIN_DESC = "On-demand subtitle provider backed by sub.wyzie.io."
PLUGIN_OVERVIEW = (
    "Streams subtitle content directly from Wyzie when playback starts. "
    "Requires a free API key from https://sub.wyzie.io/redeem."
)
PLUGIN_CATEGORY = "Subtitles"
PLUGIN_OWNER = "wyzie"
PLUGIN_IMAGE_URL = ""

# Jellyfin target -> (targetAbi, TFM)
JELLYFIN_TARGETS = {
    "12.0": ("12.0.0.0", "net10.0"),
}


def md5(path: pathlib.Path) -> str:
    h = hashlib.md5()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def run(cmd: list[str]) -> None:
    print(f"$ {' '.join(cmd)}", flush=True)
    subprocess.run(cmd, check=True)


def publish_jellyfin(jf_version: str, manifest_version: str, out_dir: pathlib.Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    run([
        "dotnet", "publish", str(CSPROJ),
        "-c", "Release",
        f"-p:Version={manifest_version}",
        "-o", str(out_dir),
        "--nologo",
    ])


def build_jellyfin_zip(
    jf_version: str,
    manifest_version: str,
    changelog: str,
    timestamp: str,
    target_abi: str,
    publish_dir: pathlib.Path,
) -> pathlib.Path:
    meta = {
        "category": PLUGIN_CATEGORY,
        "guid": PLUGIN_GUID,
        "name": PLUGIN_NAME,
        "description": PLUGIN_DESC,
        "overview": PLUGIN_OVERVIEW,
        "owner": PLUGIN_OWNER,
        "targetAbi": target_abi,
        "version": manifest_version,
        "changelog": changelog,
        "timestamp": timestamp,
        "assemblies": ["Jellyfin.Plugin.Wyzie.dll", "Wyzie.Common.dll"],
    }
    zip_path = ARTIFACTS / "Jellyfin.Plugin.Wyzie.zip"
    dll = publish_dir / "Jellyfin.Plugin.Wyzie.dll"
    common = publish_dir / "Wyzie.Common.dll"
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("meta.json", json.dumps(meta, indent=2))
        z.write(dll, dll.name)
        z.write(common, common.name)
    return zip_path


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--version", required=True, help="Plugin version (e.g. 1.0.5)")
    p.add_argument("--repo", default="", help="owner/repo for GitHub release URL")
    p.add_argument("--changelog", default="", help="Release notes blurb")
    p.add_argument(
        "--jellyfin",
        default="12.0",
        help="Comma-separated Jellyfin targets to build (default: 12.0).",
    )
    args = p.parse_args()

    base_version = args.version.lstrip("v")
    timestamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

    if ARTIFACTS.exists():
        shutil.rmtree(ARTIFACTS)
    ARTIFACTS.mkdir()

    jf_requested = [v.strip() for v in args.jellyfin.split(",") if v.strip()]

    for jf_version in jf_requested:
        if jf_version not in JELLYFIN_TARGETS:
            print(f"!! unknown Jellyfin target: {jf_version} — skipping", file=sys.stderr)
            continue
        target_abi, _tfm = JELLYFIN_TARGETS[jf_version]
        manifest_version = base_version

        publish_dir = ARTIFACTS / f"publish-jf-{jf_version}"
        publish_jellyfin(jf_version, manifest_version, publish_dir)

        zip_path = build_jellyfin_zip(
            jf_version, manifest_version, args.changelog, timestamp, target_abi, publish_dir,
        )
        checksum = md5(zip_path)
        print(f"  -> {zip_path.name}  md5={checksum}  abi={target_abi}")

        if args.repo:
            print(
                f"  source: https://github.com/{args.repo}/releases/download/v{base_version}/{zip_path.name}"
            )

    return 0


if __name__ == "__main__":
    sys.exit(main())
