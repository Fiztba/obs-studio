"""Collect upstream notices for the OBS Studio 33.0.1 Windows x64 base."""
import argparse
import hashlib
import re
import tarfile
from pathlib import Path, PurePosixPath
from zipfile import ZIP_DEFLATED, ZipFile, ZipInfo


parser = argparse.ArgumentParser(description=__doc__)
for arg in ("obs-sources", "deps", "qt6", "cef", "qt-license-dir", "output"):
    parser.add_argument("--" + arg, type=Path, required=True)
args = parser.parse_args()
pins = {
    args.obs_sources: "c8d3a8f69a0f17970ac0a8f2a2462f1bc4dacd324f6da0c4b408d442d4798913",
    args.deps: "7508972a867a0ef68c26cd8bdba61fd999edd0a92fa9eb9585774de9bf51bfbe",
    args.qt6: "78cd7a138464bb6edcdd3b85fcd63a5a7c69e369629963f3bc395bab74fb3f47",
    args.cef: "54cd191a3f56bb1f05be22b95e1fa8041061b38bd5fe94e0776fb34dd599f341",
}
for path, expected in pins.items():
    digest = hashlib.sha256()
    with path.open("rb") as archive:
        for block in iter(lambda: archive.read(1024 * 1024), b""):
            digest.update(block)
    if digest.hexdigest() != expected:
        raise ValueError("Unexpected SHA-256 for " + str(path))

files = {}
with ZipFile(args.deps) as source:
    for name in source.namelist():
        if name.startswith("licenses/") and not name.endswith("/"):
            files["obs-deps/" + name] = source.read(name)
with ZipFile(args.qt6) as source:
    for name in source.namelist():
        if name.startswith(("licenses/", "sbom/")) and not name.endswith("/"):
            files["qt6/" + name] = source.read(name)
with ZipFile(args.cef) as source:
    for name in ("LICENSE.txt", "CREDITS.html"):
        files["cef/" + name] = source.read(name)
for path in args.qt_license_dir.glob("*.txt"):
    files["qt6/qtbase-LICENSES/" + path.name] = path.read_bytes()
for name in ("GPL-3.0-only.txt", "LGPL-3.0-only.txt"):
    if "qt6/qtbase-LICENSES/" + name not in files:
        raise ValueError("Expected the LICENSES directory from Qt Base v6.11.1")
notice = re.compile(r"^(?:AUTHORS|COPYRIGHT|COPYING|LICEN[CS]E|NOTICE|PATENTS|OFL)(?:$|[._-])", re.I)
with tarfile.open(args.obs_sources, "r:gz") as source:
    for entry in source:
        if not entry.isfile():
            continue
        path = PurePosixPath(entry.name)
        if notice.match(path.name) or path.name in ("obs-qsv11-LICENSE.txt", "QSV11-License-Clarification-Email.txt"):
            rel = path.relative_to("obs-studio-33.0.1-sources")
            files["obs-studio/" + rel.as_posix()] = source.extractfile(entry).read()
with ZipFile(args.output, "x") as dest:
    for name, data in sorted(files.items()):
        entry = ZipInfo(name, (2026, 10, 29, 0, 0, 0))
        entry.compress_type = ZIP_DEFLATED
        dest.writestr(entry, data)
print(f"{len(files)} upstream notice and inventory files in {args.output}")
