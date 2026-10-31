"""Packs the plugin as Jellyfin installs it: a zip of build.yaml's artifacts plus meta.json.

Usage: python tools/package.py <publish folder> <output zip>
"""
import datetime
import json
import pathlib
import re
import sys
import zipfile


def read_build_yaml(path):
    """The few build.yaml fields meta.json needs (the file is flat; no YAML library needed)."""
    text = path.read_text(encoding="utf-8")
    fields = {}
    for key in ("name", "guid", "version", "targetAbi", "overview", "category", "owner"):
        m = re.search(rf'^{key}:\s*"([^"]*)"', text, re.M)
        if not m:
            sys.exit(f"build.yaml has no {key}")
        fields[key] = m.group(1)
    for key in ("description", "changelog"):
        m = re.search(rf"^{key}:\s*>\n((?:  .*\n?)+)", text, re.M)
        fields[key] = " ".join(line.strip() for line in m.group(1).splitlines()) if m else ""
    m = re.search(r"^artifacts:\n((?:- .*\n?)+)", text, re.M)
    fields["artifacts"] = re.findall(r'- "([^"]+)"', m.group(1)) if m else []
    return fields


def main():
    publish, output = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
    b = read_build_yaml(pathlib.Path(__file__).resolve().parent.parent / "build.yaml")
    meta = {
        "category": b["category"],
        "changelog": b["changelog"],
        "description": b["description"],
        "guid": b["guid"],
        "name": b["name"],
        "overview": b["overview"],
        "owner": b["owner"],
        "targetAbi": b["targetAbi"],
        "timestamp": datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "version": b["version"],
        # Jellyfin loads only these (else every .dll in the folder, which breaks on a native one).
        "assemblies": b["artifacts"],
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as z:
        for name in b["artifacts"]:
            f = publish / name
            if not f.exists():
                sys.exit(f"{name} isn't in {publish}")
            z.write(f, name)
        z.writestr("meta.json", json.dumps(meta, indent=2))
    print(f"{output}: {', '.join(b['artifacts'])}, meta.json")


if __name__ == "__main__":
    main()
