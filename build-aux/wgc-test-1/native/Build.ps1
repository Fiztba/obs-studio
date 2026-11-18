#requires -Version 5.1
# Run from an x64 MSVC Native Tools prompt with the Windows 11 SDK.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../../..')).Path
$out = [IO.Directory]::CreateDirectory($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)).FullName
$compiler = (Get-Command cl.exe -CommandType Application).Source
$linker = Join-Path (Split-Path $compiler) 'link.exe'
$librarian = Join-Path (Split-Path $compiler) 'lib.exe'
$common = @('/nologo','/c','/std:c++17','/EHsc','/MD','/O2','/permissive-','/Zc:__cplusplus',
    '/D_SILENCE_EXPERIMENTAL_COROUTINE_DEPRECATION_WARNINGS','/D_WIN32_WINNT=0x0A00',
    '/DNOMINMAX','/DNDEBUG','/DUNICODE','/D_UNICODE','/utf-8',"/FI$PSScriptRoot/preheader.hpp",
    "/I$PSScriptRoot","/I$repo/libobs","/I$repo/libobs-winrt","/I$repo/deps/w32-pthreads")

& $librarian /nologo /machine:x64 "/def:$PSScriptRoot/obs.def" "/out:$out/obs.lib"
if ($LASTEXITCODE) { throw 'OBS import library creation failed.' }

foreach ($unit in 'winrt-capture','winrt-dispatch') {
    & $compiler @common "/Fo$out/$unit.obj" "$repo/libobs-winrt/$unit.cpp"
    if ($LASTEXITCODE) { throw "Compiling $unit failed." }
}

& $linker /nologo /dll /machine:x64 /opt:ref /opt:icf /dynamicbase /nxcompat "/out:$out/libobs-winrt.dll" "$out/winrt-capture.obj" "$out/winrt-dispatch.obj" "$out/obs.lib" Dwmapi.lib WindowsApp.lib kernel32.lib user32.lib ole32.lib
if ($LASTEXITCODE) { throw 'Linking the Windows capture DLL failed.' }
