# Windows capture test release

The test release uses the official x64 distribution of OBS Studio 33.0.1 and replaces `bin/64bit/libobs-winrt.dll`. The modified component is built from this checkout. The installer obtains the official OBS ZIP from the OBS Project and verifies its published SHA-256 before extracting it.

## Build the capture component

Use an x64 Visual Studio Native Tools prompt. The distributed capture DLL was compiled with MSVC 14.44.35207 and Windows SDK 10.0.26100.0. Run this command from the repository root.

```powershell
powershell -NoProfile -File .\build-aux\wgc-test-1\native\Build.ps1 -OutputDirectory .\build-wgc-test
```

This creates `build-wgc-test/libobs-winrt.dll` using the 33.0.1 ABI. The `native` folder supplies the import definitions and compiler headers needed to build this component independently. The original Windows distribution is available from the [OBS Studio 33.0.1 release](https://github.com/obsproject/obs-studio/releases/tag/33.0.1).

## Package the Windows installer

The packaging script embeds the compiled DLL and its license into a Windows x64 executable. It uses the C# compiler supplied by .NET Framework 4.x on Windows.

```powershell
powershell -NoProfile -File .\build-aux\wgc-test-1\installer\Build.ps1 -PatchedDll .\build-wgc-test\libobs-winrt.dll -OutputDirectory .\build-wgc-test
```

[Installer behavior and command-line options](installer/README.md)
