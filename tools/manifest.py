"""Writes the plugin repository manifest Jellyfin reads (Dashboard -> Plugins -> Repositories).

Each release carries a manifest.json listing every version released so far, newest first, so
https://github.com/bisforboman/jellyfin-plugin-shotchapters/releases/latest/download/manifest.json always lists them
all. The fields are Jellyfin's PackageInfo and VersionInfo (MediaBrowser.Model/Updates); the checksum is the zip's MD5,
which Jellyfin checks before installing (InstallationManager).

Usage: python tools/manifest.py <zip> <zip's download URL> <previous manifest.json or -> <output manifest.json>
"""
import datetime
import hashlib
import json
import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from package import read_build_yaml  # noqa: E402


def main():
    zip_path, url, previous, output = pathlib.Path(sys.argv[1]), sys.argv[2], sys.argv[3], pathlib.Path(sys.argv[4])
    b = read_build_yaml(pathlib.Path(__file__).resolve().parent.parent / "build.yaml")
    version = {
        "version": b["version"],
        "changelog": b["changelog"],
        "targetAbi": b["targetAbi"],
        "sourceUrl": url,
        "checksum": hashlib.md5(zip_path.read_bytes()).hexdigest(),
        "timestamp": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    old = []
    if previous != "-" and pathlib.Path(previous).exists():
        for package in json.loads(pathlib.Path(previous).read_text(encoding="utf-8")):
            if package.get("guid") == b["guid"]:
                old = [v for v in package.get("versions", []) if v.get("version") != b["version"]]
    manifest = [{
        "guid": b["guid"],
        "name": b["name"],
        "description": b["description"],
        "overview": b["overview"],
        "owner": b["owner"],
        "category": b["category"],
        "versions": [version, *old],
    }]
    output.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"{output}: {b['name']} {b['version']} (+{len(old)} earlier), md5 {version['checksum']}")


if __name__ == "__main__":
    main()
