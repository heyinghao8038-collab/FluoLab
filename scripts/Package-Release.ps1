param([string]$DistributionDirectory = '', [string]$DemoDirectory = '', [string]$OptionalComponent = '')
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Write-ReleaseZip([string]$Target, [string]$SourceRoot, [string[]]$Names) {
    $temporary = $Target + '.building'
    $stream = [IO.File]::Open($temporary,[IO.FileMode]::Create)
    $archive = New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create)
    try { foreach ($name in $Names) { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $SourceRoot $name),$name.Replace('\','/')) | Out-Null } }
    finally { $archive.Dispose(); $stream.Dispose() }
    if ([IO.File]::Exists($Target)) { [IO.File]::Replace($temporary,$Target,[NullString]::Value) } else { [IO.File]::Move($temporary,$Target) }
}
$dist = if ($DistributionDirectory) { [IO.Path]::GetFullPath($DistributionDirectory) } else { Join-Path $root 'dist' }
$portable = Join-Path $dist 'FluoLab'
$names = @('FluoLab.exe','FluoLab.Cli.exe','FluoLab.Core.dll','FluoLab.Export.dll','使用说明.txt','Modules\模块接口说明.md',
    'Modules\Literature\worker.py','Modules\Literature\component.sha256','Modules\Literature\THIRD-PARTY-NOTICES.txt',
    'Modules\Mouse\index.html','Modules\Mouse\app.js','Modules\Mouse\app.css')
$names += @(Get-ChildItem -LiteralPath (Join-Path $portable 'Modules\Mouse\licenses') -File | ForEach-Object { 'Modules\Mouse\licenses\'+$_.Name })
Write-ReleaseZip (Join-Path $dist 'FluoLab-Portable-1.4.0.zip') $portable $names
$demo = if ($DemoDirectory) { [IO.Path]::GetFullPath($DemoDirectory) } else { Join-Path $dist 'Synthetic-Demo' }
if (-not (Test-Path -LiteralPath (Join-Path $demo 'README.txt'))) { throw 'Prepare the synthetic demo before packaging.' }
$demoNames = @('README.txt','Trial-Record.md','Synthetic-English-Article.pdf','Synthetic-Mouse-Backup.json',
    'Synthetic-pH-analysis.xlsx','Synthetic-pH-response.png','Synthetic-pH-response.svg','Synthetic-pH-work.fluolab','Synthetic-pH-bundle.zip')
$demoNames += @(Get-ChildItem -LiteralPath (Join-Path $demo 'ASC') -File -Filter '*.ASC' | ForEach-Object { 'ASC\'+$_.Name })
Write-ReleaseZip (Join-Path $dist 'FluoLab-Synthetic-Demo-1.4.0.zip') $demo $demoNames
$releaseFiles = @('FluoLab-Setup-1.4.0.exe','FluoLab-Portable-1.4.0.zip','FluoLab-Synthetic-Demo-1.4.0.zip')
if ($OptionalComponent) {
    $expected = (Get-Content -LiteralPath (Join-Path $root 'Modules\Literature\component.sha256')).Trim()
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $OptionalComponent).Hash.ToLowerInvariant() -ne $expected) { throw 'Optional CPU pack digest mismatch.' }
    $target = Join-Path $dist 'FluoLab-Literature-CPU-1.0.zip'
    if ([IO.Path]::GetFullPath($OptionalComponent) -ne [IO.Path]::GetFullPath($target)) { Copy-Item -LiteralPath $OptionalComponent -Destination $target -Force }
    $releaseFiles += 'FluoLab-Literature-CPU-1.0.zip'
}
$lines = foreach ($name in $releaseFiles) { (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $dist $name)).Hash.ToLowerInvariant() + '  ' + $name }
[IO.File]::WriteAllLines((Join-Path $dist 'FluoLab-1.4.0-SHA256.txt'),$lines,[Text.Encoding]::ASCII)
Write-Host 'PASS: explicit-allowlist portable/demo ZIPs and release hashes created; no lab records packaged.'
