param(
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

& (Join-Path $root 'build.ps1')
if ($LASTEXITCODE -ne 0) {
    throw 'Build failed.'
}

$testRunner = Join-Path $root 'artifacts\build\FluoLab.Tests.exe'
& $testRunner

if ($LASTEXITCODE -ne 0) {
    throw 'FluoLab tests failed.'
}

