[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PatchedDll,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$LicenseFile = (Join-Path $PSScriptRoot '..\..\..\COPYING'),
    [string]$ExpectedDllSha256
)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'The Windows x64 .NET Framework C# compiler is required.'
}
$native = (Resolve-Path -LiteralPath $PatchedDll).Path
$license = (Resolve-Path -LiteralPath $LicenseFile).Path
$moduleHash = (Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash.ToLowerInvariant()
if ($ExpectedDllSha256 -and $moduleHash -ne $ExpectedDllSha256) {
    throw "Unexpected DLL SHA-256: $moduleHash"
}
$output = [IO.Directory]::CreateDirectory($OutputDirectory).FullName
$asset = Join-Path $output 'OBS-33.0.1-WGC-Test-1-Windows-x64.exe'
$descriptor = Join-Path $output ('build-input-' + [Guid]::NewGuid().ToString('N') + '.cs')
try {
    [IO.File]::WriteAllText($descriptor, "namespace WgcTestSetup { internal static class BuildInput { internal const string ModuleHash = `"$moduleHash`"; } }")
    $settings = @(
        '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/warnaserror+', '/codepage:65001',
        "/out:$asset", "/win32manifest:$PSScriptRoot\app.manifest",
        "/resource:$native,wgc.native", "/resource:$license,wgc.license", "/resource:$PSScriptRoot\NOTICE.txt,wgc.notice",
        '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
        '/reference:System.IO.Compression.dll', '/reference:Microsoft.CSharp.dll',
        (Join-Path $PSScriptRoot 'Setup.cs'), $descriptor
    )
    & $compiler @settings
    if ($LASTEXITCODE -ne 0) { throw "C# compilation failed with exit code $LASTEXITCODE." }
    Get-FileHash -LiteralPath $asset -Algorithm SHA256
} finally {
    if (Test-Path -LiteralPath $descriptor -PathType Leaf) { Remove-Item -LiteralPath $descriptor }
}
