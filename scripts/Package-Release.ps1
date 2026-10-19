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
$portable = Join-Path $root 'dist\FluoLab'
$names = @('FluoLab.exe','FluoLab.Cli.exe','FluoLab.Core.dll','FluoLab.Export.dll','使用说明.txt','Modules\模块接口说明.md',
    'Modules\Literature\worker.py','Modules\Literature\component.sha256','Modules\Literature\THIRD-PARTY-NOTICES.txt',
    'Modules\Mouse\index.html','Modules\Mouse\app.js','Modules\Mouse\app.css')
$names += @(Get-ChildItem -LiteralPath (Join-Path $portable 'Modules\Mouse\licenses') -File | ForEach-Object { 'Modules\Mouse\licenses\'+$_.Name })
Write-ReleaseZip (Join-Path $root 'dist\FluoLab-Portable-1.3.1.zip') $portable $names
$demo = Join-Path $root 'outputs\v1.3-synthetic-literature'
Write-ReleaseZip (Join-Path $root 'dist\FluoLab-Synthetic-Literature-Demo-1.3.zip') $demo @('Synthetic-English-Article.pdf','Synthetic-Bilingual.html','Synthetic-Bilingual.txt','Synthetic-Mouse-Backup.json','README.txt')
$releaseFiles = @('FluoLab-Setup-1.3.1.exe','FluoLab-Portable-1.3.1.zip','FluoLab-Literature-CPU-1.0.zip','FluoLab-Synthetic-Literature-Demo-1.3.zip')
$lines = foreach ($name in $releaseFiles) { (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root ('dist\'+$name))).Hash.ToLowerInvariant() + '  ' + $name }
[IO.File]::WriteAllLines((Join-Path $root 'dist\FluoLab-1.3.1-SHA256.txt'),$lines,[Text.Encoding]::ASCII)
Write-Host 'PASS: explicit-allowlist portable/demo ZIPs and release hashes created; no lab records packaged.'
