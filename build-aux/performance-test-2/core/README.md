# Build the Windows x64 core library

This build compiles the release tree's complete `libobs` CMake target, including libcaption, HEVC and its Win32 backends. It uses the 27 bundled core modules in the official Windows package's registration order. The official package supplies the pthreads DLL interface. The build links to the matching FFmpeg, zlib, jansson, and other dependencies pinned in the source tree's `CMakePresets.json`.

Use Python 3.11 or newer, CMake 3.28 or newer, Ninja, Visual Studio 2022 with the x64 MSVC v143 toolset, and Windows SDK 10.0.26100.0. The test binary was built with compiler 19.44.35230, CMake 4.4.2 and Release interprocedural optimization. Open an **x64 Native Tools Command Prompt for VS 2022**, change to the source root, and start PowerShell in that shell.

```powershell
python .\build-aux\performance-test-2\core\Build-Core.py --source . --release-zip .\OBS-Studio-33.0.1-Windows-x64.zip --deps-zip .\windows-deps-2026-10-23-x64.zip --output .\build-core
```

The script accepts arbitrary local paths for the two input files and verifies their SHA-256 hashes. Get the OBS archive from the [OBS Studio 33.0.1 release](https://github.com/obsproject/obs-studio/releases/tag/33.0.1) and the Windows x64 prebuilt dependency archive from [obs-deps 2026-10-23](https://github.com/obsproject/obs-deps/releases/tag/2026-10-23).

The output is `build-core/build/libobs/obs.dll`. It replaces `bin/64bit/obs.dll` in a separate copy of OBS 33.0.1. Version metadata uses revision `af7fc38` by default. Pass `--source-id` when building another revision. The compiler version, toolchain and build paths can affect the binary hash.
