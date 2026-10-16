#!/usr/bin/env python3
"""Package a built plugin into a Jellyfin-installable zip and (optionally) update manifest.json.

Usage:
  python3 scripts/package.py --bin <build output dir> --jellyfin 12.0.0 --version 2.0.0 \
      [--repo owner/name --tag v2.0.0 --manifest manifest.json] [--changelog "..."]

The zip contains the plugin DLL and a meta.json, so it can be installed either through a
plugin repository (manifest.json) or by unzipping it into Jellyfin's plugins folder.
"""
import argparse
import datetime as dt
import hashlib
import json
import pathlib
import zipfile

GUID = "b7e8dce1-44df-4b37-91a3-15fcf9f3b76a"
NAME = "Custom Logo"
DLL = "Jellyfin.Plugin.CustomLogo.dll"
DESCRIPTION = "Replace the Jellyfin logos in the web client with your own."
OVERVIEW = ("Upload your own banner, icon and favicon from the plugin settings page. "
            "They replace the Jellyfin branding in the web client: header, side menu, "
            "loading screen, screensaver, browser tab and home-screen icon.")
CATEGORY = "General"


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", required=True, help="folder containing the built DLL")
    ap.add_argument("--jellyfin", required=True, help="Jellyfin package version built against, e.g. 12.0.0")
    ap.add_argument("--version", required=True, help="plugin version, e.g. 2.0.0")
    ap.add_argument("--out", default="dist")
    ap.add_argument("--repo", help="GitHub owner/name, enables manifest update")
    ap.add_argument("--tag", help="release tag the zip is attached to")
    ap.add_argument("--manifest", default="manifest.json")
    ap.add_argument("--changelog", default="")
    ap.add_argument("--owner", default="")
    a = ap.parse_args()

    major = a.jellyfin.split(".")[0]
    full_version = f"{a.version}.{major}"
    target_abi = ".".join((a.jellyfin.split(".") + ["0", "0", "0", "0"])[:4])
    timestamp = dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    owner = a.owner or (a.repo.split("/")[0] if a.repo else "")

    dll = pathlib.Path(a.bin) / DLL
    if not dll.is_file():
        raise SystemExit(f"{dll} not found - build first")

    meta = {
        "category": CATEGORY,
        "changelog": a.changelog,
        "description": DESCRIPTION,
        "guid": GUID,
        "name": NAME,
        "overview": OVERVIEW,
        "owner": owner,
        "targetAbi": target_abi,
        "timestamp": timestamp,
        "version": full_version,
        "status": "Active",
        "autoUpdate": True,
        "imagePath": "",
        "assemblies": [DLL],
    }

    out = pathlib.Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    zip_name = f"custom-logo_{full_version}.zip"
    zip_path = out / zip_name
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(dll, DLL)
        z.writestr("meta.json", json.dumps(meta, indent=2))

    md5 = hashlib.md5(zip_path.read_bytes()).hexdigest()
    print(f"{zip_path}  md5={md5}  targetAbi={target_abi}")

    if not a.repo:
        return

    manifest_path = pathlib.Path(a.manifest)
    manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else []
    entry = next((p for p in manifest if p.get("guid") == GUID), None)
    if entry is None:
        entry = {"guid": GUID, "versions": []}
        manifest.append(entry)

    entry.update({
        "name": NAME,
        "description": DESCRIPTION,
        "overview": OVERVIEW,
        "owner": owner,
        "category": CATEGORY,
        "imageUrl": f"https://raw.githubusercontent.com/{a.repo}/main/static/icon.png",
    })
    versions = [v for v in entry.get("versions", []) if v.get("version") != full_version]
    versions.append({
        "version": full_version,
        "changelog": a.changelog,
        "targetAbi": target_abi,
        "sourceUrl": f"https://github.com/{a.repo}/releases/download/{a.tag}/{zip_name}",
        "checksum": md5,
        "timestamp": timestamp,
    })
    versions.sort(key=lambda v: tuple(int(x) for x in v["version"].split(".")), reverse=True)
    entry["versions"] = versions
    # keep key order readable
    ordered = {k: entry[k] for k in ["guid", "name", "description", "overview", "owner", "category", "imageUrl", "versions"]}
    manifest[manifest.index(entry)] = ordered
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")
    print(f"updated {manifest_path}")


if __name__ == "__main__":
    main()
