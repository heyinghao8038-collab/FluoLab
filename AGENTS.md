# FluoLab Project Instructions

## Product

FluoLab is a local-only Windows desktop application for organizing and previewing fluorescence spectra stored in PerkinElmer `.ASC` files. The user-facing interface and messages are Chinese.

## Supported Environment

- Windows 10/11 x64.
- No administrator privileges required for installation or use.
- Microsoft Excel and WPS Office are optional; exports must be standard `.xlsx` files and must not automate either application.
- The application must not upload experimental data or require an account or network connection.

## Architecture

- `src/FluoLab.Core`: ASC parsing, immutable dataset contracts, validation, sorting, wavelength alignment, and analysis-module contracts.
- `src/FluoLab.Export`: direct `.xlsx` generation.
- `src/FluoLab.App`: WPF desktop UI.
- `src/FluoLab.Cli`: command-line compatibility entry point.
- `tests/FluoLab.Tests`: unit and integration tests.
- `installer`: per-user Windows installer source.
- `docs`: design and operating documentation.

Parsing, alignment, export, and module behavior must not depend on UI types. Analysis modules consume the standard dataset contract and must not parse ASC files independently.

## Product Invariants

- Search for the first exact `#DATA` marker; do not assume a fixed line number.
- A valid data row contains exactly two finite numeric values: wavelength and fluorescence intensity.
- Reject an entire file on missing `#DATA`, malformed data rows, empty data, or duplicate wavelengths.
- Continue processing other files when one file fails.
- Sort source files by filename using case-insensitive ordinal ordering.
- Export the numeric union of all wavelengths in ascending order and leave missing measurements blank.
- Preview selection never changes which valid files are exported.
- Preserve original numeric values; do not normalize, average, fit, or round source values.
- Export sheets are named `汇总数据` and `处理日志`.

## Implementation Rules

- Target the Windows-inbox .NET Framework 4.8 and WPF so lab PCs need no separate runtime installation.
- Compile with the platform C# compiler and treat compiler warnings as errors in project code.
- Prefer small functions and immutable records. Avoid classes unless state or framework integration makes them clearer.
- Validate all file and folder inputs at trust boundaries.
- Do not log experimental values unless a focused diagnostic explicitly requires them.
- Do not add network, telemetry, account, or auto-update behavior.
- Keep the legacy PowerShell version unchanged until the replacement passes acceptance tests.

## Verification Commands

Run from the repository root:

```powershell
.\build.ps1
.\tests\Run-NewAppTests.ps1
.\artifacts\build\FluoLab.UiTests.exe
```

Before delivery, also verify:

- Synthetic invalid files and wavelength mismatches.
- The real 78-file batch and the known sample endpoint values.
- A 100-file × 3,101-point performance batch.
- Visual layout at 1280×720 and 1920×1080, including 125% and 150% scaling.
- Reopening and visually inspecting every generated workbook sheet.
- Clean per-user install, launch, upgrade, and uninstall.

## pH Analysis Invariants

- Keep grouping metadata and analysis participation independent of spectrum preview state.
- Require explicit grouping confirmation. Never infer series or repeat independence from filenames.
- Extract only exact original wavelength values; partial missing data needs an explicit reviewed exclusion decision.
- Report measurement count, arithmetic mean and sample SD. SD is null for n=1. Do not add inferential statistics or preprocessing without a new specification.
- Revoke stale results after metadata, participation, wavelength or unit changes. Export only the current confirmed snapshot.
- Public demo files must be marked synthetic and contain no real experimental values or absolute local paths.
