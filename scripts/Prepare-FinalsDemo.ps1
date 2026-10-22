param([string]$DistributionDirectory = '')
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dist = if ($DistributionDirectory) { [IO.Path]::GetFullPath($DistributionDirectory) } else { Join-Path $root 'dist' }
$demo = Join-Path $dist 'Synthetic-Demo'
if (Test-Path -LiteralPath $demo) { throw 'Demo folder already exists. Choose a new distribution directory; do not overwrite edited examples.' }
New-Item -ItemType Directory -Path $demo | Out-Null
$app = Join-Path $dist 'FluoLab\FluoLab.exe'
$process = Start-Process -FilePath $app -ArgumentList @('--ph-demo-export', ('"'+$demo+'"')) -PassThru -Wait -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw 'Synthetic pH generation failed.' }
Copy-Item -LiteralPath (Join-Path $root 'examples\synthetic-literature\Synthetic-English-Article.pdf') -Destination $demo
Copy-Item -LiteralPath (Join-Path $root 'examples\synthetic-literature\Synthetic-Mouse-Backup.json') -Destination $demo
Copy-Item -LiteralPath (Join-Path $root 'docs\Demo-Guide.txt') -Destination (Join-Path $demo 'README.txt')
Copy-Item -LiteralPath (Join-Path $root 'docs\Trial-Record.md') -Destination $demo
Write-Host 'PASS: clearly synthetic ASC/PDF/mouse demo prepared; no real laboratory inputs used.'
