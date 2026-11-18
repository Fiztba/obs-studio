# OBS Studio 33.0.1 Windows performance test 2

The Windows x64 installer combines the deferred Windows Graphics Capture copy from test 1 with seven fixes backported to OBS Studio 33.0.1. It creates a separate portable installation. Its default folder is `%LOCALAPPDATA%\Programs\OBS-Performance-Test-33.0.1-2`. To reopen the test build, run **Launch OBS Performance Test 2** from that folder.

| Installed component | Changes | Build |
| --- | --- | --- |
| `bin/64bit/libobs-winrt.dll` | Copies the latest pending Windows Graphics Capture frame when OBS draws the source. The published DLL is identical to test 1. | [Capture module build](../wgc-test-1/README.md) |
| `bin/64bit/obs.dll` | Skips inactive audio tracks and fixes stale samples on late connections, the position reported after an array seek, and timing reported for divided encoder frame rates. | [Core build](core/README.md) |
| `core/obs-filters/obs-filters.dll` | Releases Render Delay textures on source loss and honors the full configured frame delay. | [Plugin builds](plugins/README.md) |
| `core/image-source/image-source.dll` | Ends non-looping slideshows once and fixes random selection on large playlists. | [Plugin builds](plugins/README.md) |

Build the components, then pass the four DLLs and the original release archives to the packaging tools. The Output folder will contain either the full offline installer or a smaller web installer. Both install the same files. [Packaging instructions](installer/README.md)

The executable and modified DLLs are unsigned. Stock PDB files for the four replacement DLLs are omitted. The matching OBS source archive distributed with the installer includes the source of the bundled OBS submodules. The supplemental dependency source download includes upstream recipes, patches and source archives for third-party libraries. [Release and downloads](https://github.com/Fiztba/obs-studio/releases/tag/33.0.1-wgc-test.2)
