# Builds Release and packs a Thunderstore zip into ./dist
# Usage: powershell -ExecutionPolicy Bypass -File .\package.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$manifest = Get-Content (Join-Path $root 'thunderstore\manifest.json') -Raw | ConvertFrom-Json
$version = $manifest.version_number
Write-Host "Packaging $($manifest.name) $version"

dotnet build (Join-Path $root 'StorageVision.csproj') -c Release -p:Version=$version
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$dll = Join-Path $root 'bin\Release\StorageVision.dll'
$icon = Join-Path $root 'thunderstore\icon.png'
foreach ($f in @($dll, $icon)) { if (-not (Test-Path $f)) { throw "Missing $f" } }

# Entry name in zip -> source file. Written manually because Compress-Archive in
# Windows PowerShell 5.1 stores backslashes in entry names, which breaks on Thunderstore.
$entries = [ordered]@{
    'manifest.json'             = (Join-Path $root 'thunderstore\manifest.json')
    'icon.png'                  = $icon
    'README.md'                 = (Join-Path $root 'README.md')
    'CHANGELOG.md'              = (Join-Path $root 'CHANGELOG.md')
    'plugins/StorageVision.dll' = $dll
}

$distDir = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $distDir | Out-Null
$zip = Join-Path $distDir "StorageVision-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in $entries.Keys) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $entries[$name], $name) | Out-Null
    }
}
finally {
    $archive.Dispose()
}
Write-Host "Created $zip"
