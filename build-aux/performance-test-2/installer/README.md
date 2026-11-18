# Performance Test 2 installer for Windows x64

`OBS-33.0.1-Performance-Test-2-Windows-x64.exe` contains the full application. It installs offline into a new folder and keeps the fork's portable configuration there. The default folder is `%LOCALAPPDATA%\Programs\OBS-Performance-Test-33.0.1-2`. Open **Launch OBS Performance Test 2** in that folder to run it again.

The optional `-Web.exe` downloads the official OBS Studio 33.0.1 Windows x64 ZIP on first install. To use it offline, place the exact official ZIP beside it. Both installers verify the original ZIP and the four replacement DLLs, and both install identical application files. They omit the upstream PDBs for the four replaced DLLs.

```text
OBS-33.0.1-Performance-Test-2-Windows-x64.exe --silent --install-dir "C:\path\to\new folder"
```

Silent installation leaves OBS closed. Exit codes are 0 for success, 2 for an existing destination, 3 for invalid archive data or a failed digest check, 4 for unrecognized arguments, and 1 for other failures. The application folder appears after extraction and verification. To uninstall, close OBS, back up any portable scenes and profiles you want to keep from its `config` folder, and delete the installation folder.

## Build

Build the fork's x64 modules from this release's source. Copy `obs.dll`, `libobs-winrt.dll`, `obs-filters.dll` and `image-source.dll` into a single build input directory. The installer build records their SHA-256 digests and checks each against its installed copy. The installer uses the Windows x64 .NET Framework C# compiler.

The notice collector uses Python 3 and the following upstream inputs.

| Input | Original release file |
| --- | --- |
| OBS Studio base and source | `OBS-Studio-33.0.1-Windows-x64.zip` and `OBS-Studio-33.0.1-Sources.tar.gz`. [OBS Studio 33.0.1 release](https://github.com/obsproject/obs-studio/releases/tag/33.0.1) |
| Windows dependencies and Qt | `windows-deps-2026-10-23-x64.zip` and `windows-deps-qt6-2026-10-23-x64.zip`. [OBS dependency release](https://github.com/obsproject/obs-deps/releases/tag/2026-10-23) |
| Chromium Embedded Framework | `cef_binary_7871_windows_x64.zip`. [OBS CEF build](https://cdn-fastly.obsproject.com/downloads/cef_binary_7871_windows_x64.zip) |
| Qt Base licenses | The `LICENSES` directory from Qt Base v6.11.1. [Qt Base license files](https://github.com/qt/qtbase/tree/59c81a3c2247b821b9b84b4eb8d939b77e07e276/LICENSES) |

From the installer source directory with these inputs in the current directory, run the following commands. Use a new notice archive path. `Collect-Notices.py` checks the upstream package digests and gathers the dependency license files, the Qt software bills of materials and the Chromium credits. `Build.ps1` uses the root `COPYING` from the source checkout.

```powershell
python .\Collect-Notices.py --obs-sources OBS-Studio-33.0.1-Sources.tar.gz --deps windows-deps-2026-10-23-x64.zip --qt6 windows-deps-qt6-2026-10-23-x64.zip --cef cef_binary_7871_windows_x64.zip --qt-license-dir .\qtbase-6.11.1\LICENSES --output .\notices.zip
.\Build.ps1 -OfficialZip .\OBS-Studio-33.0.1-Windows-x64.zip -ModuleDirectory .\modules -ThirdPartyNotices .\notices.zip -OutputDirectory .\dist
.\Build.ps1 -OfficialZip .\OBS-Studio-33.0.1-Windows-x64.zip -ModuleDirectory .\modules -ThirdPartyNotices .\notices.zip -OutputDirectory .\dist -PayloadMode Web
```
