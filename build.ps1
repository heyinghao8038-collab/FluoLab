param(
    [string]$Configuration = 'Release',
    [string]$DistributionDirectory = ''
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$assemblyRoot = 'C:\Windows\Microsoft.NET\assembly'
$build = Join-Path $root 'artifacts\build'
$dist = if ($DistributionDirectory) { [IO.Path]::GetFullPath($DistributionDirectory) } else { Join-Path $root 'dist' }
$portable = Join-Path $dist 'FluoLab'

if (-not (Test-Path -LiteralPath $csc)) {
    throw '.NET Framework 4.8 compiler was not found.'
}

New-Item -ItemType Directory -Force -Path $build | Out-Null
New-Item -ItemType Directory -Force -Path $portable | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $portable 'Modules') | Out-Null
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$common = @('/nologo', '/warnaserror+', '/optimize+', '/utf8output', '/codepage:65001')
$corePath = Join-Path $build 'FluoLab.Core.dll'
$exportPath = Join-Path $build 'FluoLab.Export.dll'
$cliPath = Join-Path $build 'FluoLab.Cli.exe'
$appPath = Join-Path $build 'FluoLab.exe'
$testsPath = Join-Path $build 'FluoLab.Tests.exe'
$setupPath = Join-Path $dist 'FluoLab-Setup-1.4.0.exe'

& $csc @common /target:library "/out:$corePath" `
    "/reference:$(Join-Path $framework 'System.Web.Extensions.dll')" `
    "/reference:$(Join-Path $framework 'System.IO.Compression.dll')" `
    "/reference:$(Join-Path $framework 'System.IO.Compression.FileSystem.dll')" `
    (Join-Path $root 'src\FluoLab.Core\Models.cs') `
    (Join-Path $root 'src\FluoLab.Core\AscParser.cs') `
    (Join-Path $root 'src\FluoLab.Core\BatchProcessor.cs') `
    (Join-Path $root 'src\FluoLab.Core\BatchCompleteness.cs') `
    (Join-Path $root 'src\FluoLab.Core\PhAnalysis.cs') `
    (Join-Path $root 'src\FluoLab.Core\PhDemo.cs') `
    (Join-Path $root 'src\FluoLab.Core\WorkflowData.cs') `
    (Join-Path $root 'src\FluoLab.Core\Literature.cs') `
    (Join-Path $root 'src\FluoLab.Core\LiteratureReview.cs') `
    (Join-Path $root 'src\FluoLab.Core\LocalLiterature.cs') `
    (Join-Path $root 'src\FluoLab.Core\LoopbackAssetServer.cs') `
    (Join-Path $root 'src\FluoLab.Core\ModuleContracts.cs')
if ($LASTEXITCODE -ne 0) { throw 'FluoLab.Core build failed.' }

& $csc @common /target:library "/out:$exportPath" `
    "/reference:$corePath" `
    "/reference:$(Join-Path $framework 'System.IO.Compression.dll')" `
    "/reference:$(Join-Path $framework 'System.IO.Compression.FileSystem.dll')" `
    (Join-Path $root 'src\FluoLab.Export\WorkbookExporter.cs') `
    (Join-Path $root 'src\FluoLab.Export\PhWorkbookExporter.cs') `
    (Join-Path $root 'src\FluoLab.Export\AnalysisBundleExporter.cs')
if ($LASTEXITCODE -ne 0) { throw 'FluoLab.Export build failed.' }

& $csc @common /target:exe /platform:anycpu32bitpreferred "/out:$cliPath" `
    "/reference:$corePath" `
    "/reference:$exportPath" `
    (Join-Path $root 'src\FluoLab.Cli\Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'FluoLab.Cli build failed.' }

$wpfReferences = @(
    "/reference:$(Join-Path $assemblyRoot 'GAC_32\PresentationCore\v4.0_4.0.0.0__31bf3856ad364e35\PresentationCore.dll')",
    "/reference:$(Join-Path $assemblyRoot 'GAC_MSIL\PresentationFramework\v4.0_4.0.0.0__31bf3856ad364e35\PresentationFramework.dll')",
    "/reference:$(Join-Path $assemblyRoot 'GAC_MSIL\WindowsBase\v4.0_4.0.0.0__31bf3856ad364e35\WindowsBase.dll')",
    "/reference:$(Join-Path $assemblyRoot 'GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll')",
    "/reference:$(Join-Path $framework 'System.Windows.Forms.dll')"
)

& $csc @common /target:winexe /platform:anycpu32bitpreferred "/out:$appPath" `
    "/win32manifest:$(Join-Path $root 'src\FluoLab.App\app.manifest')" `
    "/reference:$corePath" `
    "/reference:$exportPath" `
    @wpfReferences `
    (Join-Path $root 'src\FluoLab.App\Theme.cs') `
    (Join-Path $root 'src\FluoLab.App\WindowPlacement.cs') `
    (Join-Path $root 'src\FluoLab.App\LiteratureWindow.cs') `
    (Join-Path $root 'src\FluoLab.App\BatchCompletenessWindow.cs') `
    (Join-Path $root 'src\FluoLab.App\LaboratoryModulesWindow.cs') `
    (Join-Path $root 'src\FluoLab.App\SpectrumPlot.cs') `
    (Join-Path $root 'src\FluoLab.App\MainWindow.cs') `
    (Join-Path $root 'src\FluoLab.App\WorkProjectUi.cs') `
    (Join-Path $root 'src\FluoLab.App\PhAnalysisWindow.cs') `
    (Join-Path $root 'src\FluoLab.App\PhWorkflowUi.cs') `
    (Join-Path $root 'src\FluoLab.App\PhResponseChart.cs') `
    (Join-Path $root 'src\FluoLab.App\Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'FluoLab.App build failed.' }

& $csc @common /target:exe /platform:anycpu32bitpreferred "/out:$testsPath" `
    "/reference:$corePath" `
    "/reference:$exportPath" `
    "/reference:$(Join-Path $framework 'System.IO.Compression.dll')" `
    "/reference:$(Join-Path $framework 'System.IO.Compression.FileSystem.dll')" `
    (Join-Path $root 'tests\FluoLab.Tests\Program.cs') `
    (Join-Path $root 'tests\FluoLab.Tests\PhAnalysisTests.cs') `
    (Join-Path $root 'tests\FluoLab.Tests\WorkflowTests.cs') `
    (Join-Path $root 'tests\FluoLab.Tests\FinalsTests.cs') `
    (Join-Path $root 'tests\FluoLab.Tests\LaboratoryTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'FluoLab.Tests build failed.' }

& $csc @common /target:exe /platform:anycpu32bitpreferred "/out:$(Join-Path $build 'FluoLab.UiTests.exe')" `
    "/win32manifest:$(Join-Path $root 'src\FluoLab.App\app.manifest')" `
    "/reference:$corePath" @wpfReferences `
    (Join-Path $root 'tests\FluoLab.UiTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'FluoLab.UiTests build failed.' }

Copy-Item -Force -LiteralPath $appPath -Destination (Join-Path $portable 'FluoLab.exe')
Copy-Item -Force -LiteralPath $cliPath -Destination (Join-Path $portable 'FluoLab.Cli.exe')
Copy-Item -Force -LiteralPath $corePath -Destination (Join-Path $portable 'FluoLab.Core.dll')
Copy-Item -Force -LiteralPath $exportPath -Destination (Join-Path $portable 'FluoLab.Export.dll')
Copy-Item -Force -LiteralPath (Join-Path $root 'docs\UserGuide.txt') -Destination (Join-Path $portable '使用说明.txt')
Copy-Item -Force -LiteralPath (Join-Path $root 'Modules\模块接口说明.md') -Destination (Join-Path $portable 'Modules\模块接口说明.md')

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$moduleFiles = @('Literature\worker.py', 'Literature\component.sha256', 'Literature\THIRD-PARTY-NOTICES.txt', 'Mouse\index.html', 'Mouse\app.js', 'Mouse\app.css')
$moduleFiles += @(Get-ChildItem -LiteralPath (Join-Path $root 'Modules\Mouse\licenses') -File | ForEach-Object { 'Mouse\licenses\' + $_.Name })
$moduleArchivePath = Join-Path $build 'Modules.zip'
$moduleStream = [IO.File]::Open($moduleArchivePath, [IO.FileMode]::Create)
$moduleArchive = New-Object IO.Compression.ZipArchive($moduleStream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($moduleFile in $moduleFiles) {
        $source = Join-Path $root ('Modules\' + $moduleFile)
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($moduleArchive, $source, $moduleFile.Replace('\','/')) | Out-Null
        foreach ($destinationRoot in @($build, $portable)) {
            $destination = Join-Path $destinationRoot ('Modules\' + $moduleFile)
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
            Copy-Item -LiteralPath $source -Destination $destination -Force
        }
    }
} finally { $moduleArchive.Dispose(); $moduleStream.Dispose() }

& $csc @common /target:winexe /platform:anycpu32bitpreferred "/out:$setupPath" `
    "/win32manifest:$(Join-Path $root 'installer\app.manifest')" `
    "/reference:$(Join-Path $framework 'System.Windows.Forms.dll')" `
    "/reference:$(Join-Path $framework 'System.Drawing.dll')" `
    "/reference:$(Join-Path $framework 'System.IO.Compression.dll')" `
    "/resource:$appPath,FluoLab.Payload.App" `
    "/resource:$cliPath,FluoLab.Payload.Cli" `
    "/resource:$corePath,FluoLab.Payload.Core" `
    "/resource:$exportPath,FluoLab.Payload.Export" `
    "/resource:$(Join-Path $root 'docs\UserGuide.txt'),FluoLab.Payload.Guide" `
    "/resource:$(Join-Path $root 'Modules\模块接口说明.md'),FluoLab.Payload.ModuleGuide" `
    "/resource:$moduleArchivePath,FluoLab.Payload.Modules" `
    (Join-Path $root 'installer\Setup.cs')
if ($LASTEXITCODE -ne 0) { throw 'FluoLab installer build failed.' }

Write-Host "Build complete: $setupPath"

