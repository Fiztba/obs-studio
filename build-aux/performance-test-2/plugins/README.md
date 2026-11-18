# Building the Windows source and filter plugins

Use Python 3.11 or later, CMake 3.28 or later, Ninja, and an x64 Native Tools command prompt for Visual Studio with the C++ toolchain and Windows SDK. The tested build used MSVC v143 14.44 and Windows SDK 10.0.26100.0. Keep `CMakeLists.txt` and `build-plugins.py` in the same directory. The helper reads the plugin sources from the tagged fork checkout.

Download the [official OBS Studio 33.0.1 Windows x64 ZIP](https://github.com/obsproject/obs-studio/releases/download/33.0.1/OBS-Studio-33.0.1-Windows-x64.zip) and the [OBS Windows x64 dependencies for 2026-10-23](https://github.com/obsproject/obs-deps/releases/download/2026-10-23/windows-deps-2026-10-23-x64.zip). The helper validates both archives before extracting files. From the fork checkout, run this command with the paths to those archives and a dedicated build folder outside the checkout.

```bat
py build-aux\performance-test-2\plugins\build-plugins.py --source . --obs-zip C:\downloads\OBS-Studio-33.0.1-Windows-x64.zip --deps-zip C:\downloads\windows-deps-2026-10-23-x64.zip --build-dir C:\build\obs-test-plugins
```

The plugin targets compile all source files listed in their upstream CMake scripts, link against import libraries derived from official OBS binaries, and build with the release's RNNoise and SpeexDSP dependencies. The resulting `image-source.dll` and `obs-filters.dll` appear in `cmake\modules` under the designated build folder. Public symbols are written to `cmake\public-symbols`. MSVC debug identities can change DLL hashes between builds or relinks.
