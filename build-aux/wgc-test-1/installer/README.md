# Windows x64 installer for WGC Test 1

Run the executable to install the fork into a new folder. It defaults to `%LOCALAPPDATA%\Programs\OBS-WGC-Test-33.0.1-1`. The launch option on the last step opens OBS with a separate portable configuration. The folder's **Launch OBS WGC Test** shortcut starts the same build again. To remove the application, close OBS and back up any scenes and profiles you want to keep from the portable `config` folder before removing the installation folder.

Setup downloads the OBS Project's Windows x64 ZIP over HTTPS. For an offline installation, put the exact `OBS-Studio-33.0.1-Windows-x64.zip` beside the installer. Both paths verify the complete archive and its stock Windows capture DLL before applying the patched DLL. An invalid adjacent ZIP stops installation. [Upstream release](https://github.com/obsproject/obs-studio/releases/tag/33.0.1)

| Release input | SHA-256 |
| --- | --- |
| Official OBS Studio 33.0.1 Windows x64 ZIP, 223,786,669 bytes | `0d9436451c2d772b33428e3fdae9776f071895c335de17283176306fb72651d3` |
| Official `bin/64bit/libobs-winrt.dll` within that ZIP | `4777e510d6436c3a3496d7ac4c360b3147441c74da2ea0856a3f65ce26af57a1` |
| Patched DLL distributed in WGC Test 1 | `21e6c48da04c69245f8509fce34454ffb0318f10006113f6c5eb8de5e2d889b6` |

## Build

Build the modified DLL using the sibling `native/Build.ps1` recipe. From this installer directory, pass its output DLL and a writable build directory to the following command. The source tree's root `COPYING` supplies the GPL text. The script uses the Windows x64 .NET Framework C# compiler. A locally rebuilt native DLL may have a different digest, which setup records and verifies.

```powershell
.\Build.ps1 -PatchedDll '<native build directory>\libobs-winrt.dll' -OutputDirectory '.\build'
```

## Silent install

The destination must be a new absolute path. Silent setup extracts the app and creates its local launcher. Launch OBS separately when ready.

```text
OBS-33.0.1-WGC-Test-1-Windows-x64.exe --silent --install-dir "C:\path\to\new folder"
```

Setup returns 0 on success, 2 if the destination already exists, 3 for invalid archive data or a failed digest check, 4 for unsupported command-line arguments, and 1 for other failures. Extraction occurs in a temporary sibling of the destination and the finished application directory is moved into place after verification.
