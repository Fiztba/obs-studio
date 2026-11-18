"""Build the x64 OBS 33.0.1 performance test's obs.dll in a VS developer shell."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import zipfile


RELEASE_ZIP_SHA256 = "0d9436451c2d772b33428e3fdae9776f071895c335de17283176306fb72651d3"


def verify(path, digest):
    with path.open("rb") as file:
        if hashlib.file_digest(file, "sha256").hexdigest() != digest:
            raise SystemExit(f"SHA-256 verification failed for {path}")


def tool(name):
    executable = shutil.which(name)
    if not executable:
        raise SystemExit(f"{name} was not found. Use a Visual Studio x64 developer shell.")
    return Path(executable).as_posix()


def build():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path, help="Combined release source root")
    parser.add_argument("--release-zip", required=True, type=Path, help="Official OBS 33.0.1 Windows x64 ZIP")
    parser.add_argument("--deps-zip", required=True, type=Path, help="obs-deps Windows x64 ZIP pinned by this release")
    parser.add_argument("--output", required=True, type=Path, help="Separate build directory")
    parser.add_argument("--source-id", default="af7fc38", help="Revision for OBS version metadata")
    args = parser.parse_args()
    source = args.source.resolve()
    output = args.output.resolve()
    inputs = output / "inputs"
    deps = output / "deps"
    build_dir = output / "build"
    cmake, compiler, rc, mt, lib, dumpbin = map(tool, ("cmake", "cl", "rc", "mt", "lib", "dumpbin"))
    tool("ninja")
    config = json.loads((source / "CMakePresets.json").read_text(encoding="utf-8"))
    dep_preset = next(p for p in config["configurePresets"] if p["name"] == "dependencies")
    prebuilt = dep_preset["vendor"]["obsproject.com/obs-studio"]["dependencies"]["prebuilt"]
    verify(args.deps_zip, prebuilt["hashes"]["windows-x64"])
    verify(args.release_zip, RELEASE_ZIP_SHA256)
    inputs.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(args.deps_zip) as z:
        for member in z.namelist():
            if member.startswith(("include/", "lib/", "share/")) and not member.endswith("/"):
                z.extract(member, deps)
    with zipfile.ZipFile(args.release_zip) as z:
        pthread = inputs / "w32-pthreads.dll"
        pthread.write_bytes(z.read("bin/64bit/w32-pthreads.dll"))
        # The 33.0.1 Windows module registration order is alphabetical.
        modules = sorted(m.group(1) for entry in z.namelist()
                         if (m := re.fullmatch(r"core/([^/]+)/\1\.dll", entry, re.I)))
        if len(modules) != 27:
            raise SystemExit("Unexpected 33.0.1 core module list")
        (inputs / "modules.txt").write_text("\n".join(modules) + "\n", encoding="ascii")
    exported = subprocess.check_output([dumpbin, "/nologo", "/exports", pthread], text=True)
    names = re.findall(r"^\s*\d+\s+[0-9A-F]+\s+[0-9A-F]{8}\s+(\w+)", exported, re.M | re.I)
    if len(names) != 120:
        raise SystemExit("Unexpected pthreads exports in the release")
    definition = inputs / "w32-pthreads.def"
    definition.write_text("LIBRARY w32-pthreads.dll\nEXPORTS\n" + "\n".join(names) + "\n", encoding="ascii")
    subprocess.run([lib, "/nologo", "/machine:x64", "/def:" + str(definition),
                    "/out:" + str(inputs / "w32-pthreads.lib")], check=True)
    config_args = [cmake, "-S", Path(__file__).resolve().parent, "-B", build_dir, "-G", "Ninja",
                   "-DCMAKE_BUILD_TYPE=Release", "-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreadedDLL",
                   "-DCMAKE_C_COMPILER=" + compiler, "-DCMAKE_CXX_COMPILER=" + compiler,
                   "-DCMAKE_RC_COMPILER=" + rc, "-DCMAKE_MT=" + mt,
                   "-DCMAKE_PREFIX_PATH=" + deps.as_posix(), "-DOBS_REPOSITORY=" + source.as_posix(),
                   "-DPTHREADS_DIRECTORY=" + inputs.as_posix(), "-DSOURCE_COMMIT=" + args.source_id]
    subprocess.run(config_args, check=True)
    subprocess.run([cmake, "--build", build_dir, "--target", "libobs", "--parallel", "8"], check=True)
    result = build_dir / "libobs" / "obs.dll"
    print(f"{hashlib.sha256(result.read_bytes()).hexdigest()}  {result}")


if __name__ == "__main__":
    build()
