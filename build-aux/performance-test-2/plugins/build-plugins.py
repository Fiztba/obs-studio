"""Compile the fork's image-source and obs-filters modules in an x64 MSVC developer shell."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import zipfile


def sha256(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def verified(path, expected):
    if sha256(path) != expected:
        raise SystemExit(f'SHA-256 mismatch: {path}')


def executable(name):
    path = shutil.which(name)
    if not path:
        raise SystemExit(f'{name} was not found. Start an x64 Native Tools command prompt for Visual Studio.')
    return path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True, help='root of the fork source checkout')
    parser.add_argument('--obs-zip', type=Path, required=True, help='official OBS-Studio-33.0.1-Windows-x64.zip')
    parser.add_argument('--deps-zip', type=Path, required=True, help='official 2026-10-23 prebuilt obs-deps for Windows x64')
    parser.add_argument('--build-dir', type=Path, required=True, help='dedicated build directory outside the source tree')
    args = parser.parse_args()
    cmake_file = Path(__file__).resolve().parent
    source = args.source.resolve(strict=True)
    obs_zip = args.obs_zip.resolve(strict=True)
    deps_zip = args.deps_zip.resolve(strict=True)
    build = args.build_dir.resolve()
    if os.name != 'nt' or not os.environ.get('WindowsSDKVersion'):
        parser.error('Use an x64 Native Tools command prompt with the Windows SDK environment initialized.')
    if build.is_relative_to(source):
        parser.error('Select a build directory outside the OBS source checkout.')
    cl, dumpbin, lib, link, cmake, rc, mt = map(executable, ('cl.exe', 'dumpbin.exe', 'lib.exe', 'link.exe', 'cmake', 'rc.exe', 'mt.exe'))
    ninja = executable('ninja')
    verified(obs_zip, '0d9436451c2d772b33428e3fdae9776f071895c335de17283176306fb72651d3')
    presets = json.loads((source/'CMakePresets.json').read_text(encoding='utf-8'))['configurePresets']
    dependencies = next(p for p in presets if p['name'] == 'dependencies')['vendor']['obsproject.com/obs-studio']['dependencies']
    verified(deps_zip, dependencies['prebuilt']['hashes']['windows-x64'])
    imports = build/'imports'
    imports.mkdir(parents=True, exist_ok=True)
    deps = build/'deps'
    with zipfile.ZipFile(deps_zip) as archive:
        for filename in archive.namelist():
            if filename.startswith(('include/', 'lib/')):
                archive.extract(filename, deps)
    with zipfile.ZipFile(obs_zip) as archive:
        for module in ('obs', 'w32-pthreads'):
            dll = imports/(module + '.dll')
            dll.write_bytes(archive.read('bin/64bit/' + dll.name))
            text = subprocess.check_output([dumpbin, '/nologo', '/exports', dll], text=True)
            names = re.findall(r'^\s*\d+\s+[0-9A-F]+\s+[0-9A-F]{8}\s+(\S+)\s*$', text, re.M)
            if not names:
                raise RuntimeError(f'No exports found in {dll}')
            defs = imports/(module + '.def')
            defs.write_text('LIBRARY ' + dll.name + '\nEXPORTS\n' + '\n'.join(names) + '\n', encoding='ascii')
            subprocess.run([lib, '/nologo', '/machine:x64', '/def:' + str(defs), '/out:' + str(imports/(module + '.lib'))], check=True)
    def option(key, value):
        return '-D' + key + '=' + str(value).replace('\\', '/')
    subprocess.run([cmake, '-G', 'Ninja', '-S', cmake_file, '-B', build/'cmake', option('OBS_SOURCE', source),
                    option('CMAKE_PREFIX_PATH', deps), option('IMPORT_DIR', imports), '-DCMAKE_BUILD_TYPE=Release',
                    option('CMAKE_C_COMPILER', cl), option('CMAKE_CXX_COMPILER', cl), option('CMAKE_RC_COMPILER', rc),
                    option('CMAKE_LINKER', link), option('CMAKE_MT', mt), option('CMAKE_MAKE_PROGRAM', ninja),
                    '-DENABLE_SPEEXDSP=ON', '-DENABLE_RNNOISE=ON', '-DENABLE_CCACHE=OFF'], check=True)
    subprocess.run([cmake, '--build', build/'cmake', '--target', 'image-source', 'obs-filters'], check=True)
    for name in ('image-source', 'obs-filters'):
        dll = build/'cmake/modules'/(name + '.dll')
        print(sha256(dll), dll)


if __name__ == '__main__':
    main()
