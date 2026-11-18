#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OfficialZip,
    [Parameter(Mandatory)][string]$ModuleDirectory,
    [Parameter(Mandatory)][string]$ThirdPartyNotices,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('Bundled','Web')][string]$PayloadMode = 'Bundled',
    [string]$LicenseFile = (Join-Path $PSScriptRoot '../../../COPYING')
)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler -PathType Leaf)) { throw 'The x64 .NET Framework C# compiler is required.' }
$archive = (Get-Item -LiteralPath $OfficialZip).FullName
$license = (Get-Item -LiteralPath $LicenseFile).FullName
$credits = (Get-Item -LiteralPath $ThirdPartyNotices).FullName
$modules = (Get-Item -LiteralPath $ModuleDirectory).FullName
if ((Get-FileHash -LiteralPath $archive).Hash -ne '0D9436451C2D772B33428E3FDAE9776F071895C335DE17283176306FB72651D3') {
    throw 'Expected the official OBS-Studio-33.0.1-Windows-x64.zip archive.'
}
$stockDigests = [ordered]@{
    'bin/64bit/obs.dll' = '34d3213b1ed9386cb0c2072fafb8946b2fced699f22219c496fcaba5bf1de6bb'
    'bin/64bit/libobs-winrt.dll' = '4777e510d6436c3a3496d7ac4c360b3147441c74da2ea0856a3f65ce26af57a1'
    'core/obs-filters/obs-filters.dll' = '943bd20e5c5f384bf5d991ee43aaf5ec2b72a4135fa6ae53ff46ef90b8b54273'
    'core/image-source/image-source.dll' = '50bd922cf58557eca7a6e455cf27fddba1adc02844124e9e5b7f04752a81ec28'
}
$output = [IO.Directory]::CreateDirectory($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)).FullName
$descriptor = Join-Path $output ('build-input-' + [guid]::NewGuid().ToString('N') + '.cs')
$stem = 'OBS-33.0.1-Performance-Test-2-Windows-x64'
if ($PayloadMode -eq 'Web') { $stem += '-Web' }
$asset = Join-Path $output ($stem + '.exe')
$settings = @(
    '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/warnaserror+', '/codepage:65001',
    "/out:$asset", "/win32manifest:$PSScriptRoot/app.manifest",
    "/resource:$license,test2.license", "/resource:$credits,test2.credits", "/resource:$PSScriptRoot/NOTICE.txt,test2.notice",
    '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:System.IO.Compression.dll',
    '/reference:Microsoft.CSharp.dll', (Join-Path $PSScriptRoot 'Setup.cs'), $descriptor
)
if ($PayloadMode -eq 'Bundled') { $settings += "/resource:$archive,test2.upstream" }
$rows = foreach ($relative in $stockDigests.Keys) {
    $name = [IO.Path]::GetFileName($relative)
    $module = (Get-Item -LiteralPath (Join-Path $modules $name)).FullName
    $new = (Get-FileHash -LiteralPath $module).Hash.ToLowerInvariant()
    if ($new -eq $stockDigests[$relative]) { throw "$name is identical to the upstream DLL." }
    $settings += "/resource:$module,test2.$name"
    'new string[] { "' + $relative + '", "' + $stockDigests[$relative] + '", "' + $new + '" }'
}
try {
    $contents = 'namespace PerformanceTestSetup { internal static class BuildInput { internal static readonly string[][] Modules = { ' +
        ($rows -join ",`n") + ' }; } }'
    [IO.File]::WriteAllText($descriptor, $contents)
    & $compiler @settings
    if ($LASTEXITCODE) { throw "C# compilation failed with exit code $LASTEXITCODE." }
    Get-FileHash -LiteralPath $asset
} finally {
    if (Test-Path -LiteralPath $descriptor -PathType Leaf) { Remove-Item -LiteralPath $descriptor }
}
