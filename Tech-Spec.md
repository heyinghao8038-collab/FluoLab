# FluoLab Technical Specification

## Version 1.4.1 Publication Tasks — 2026-10-22

Update the existing exact-tag CI release path to v1.4.1, with minimal workflow edits and the existing read/write job permissions. Run the allowlist sanitizer, review the isolated Git diff and production-source parity, build/test/package that copy, then push main and the new tag without force. Poll anonymous GitHub Actions and release APIs; inspect asset names and sizes. Keep previous release and private workspace untouched. Public acceptance documentation must not disclose reference sample values, source paths or private cleaning-example identities.

## Version 1.4.1 Dashboard Contract — 2026-10-22

Extend the existing completeness report with `TotalMeasurementCount` (Int64 sum of valid PointCount), `CoverageConsistent` (valid count > 0 and union equals intersection), and a display-only failure category per failed row. Categories recognize only explicit existing parser messages; unknown reasons remain Other. Preserve the original reason and raw dataset contract. There is no percentage/rate property.

`BatchDashboard` is a read-only WPF view consuming that report plus a separate preview count. It raises actions for failure category/details, never changes selection or analysis. MainWindow owns a cancellable background report request with batch identity and revision guards. Clear on import start; apply only the latest request; close detached overview/log snapshots on batch replacement. Reuse the report for completeness details when available. Narrow mode uses an owned overview window rather than an overlay over chart axes. Existing parsing, export and scientific services remain unchanged.

Tasks: specification -> fixed synthetic sidebar -> screenshots -> MainWindow wiring -> core/UI stale and selection tests -> reference/performance regressions -> 1.4.1 build/package/docs. No new dependency, stack change, data upload or unrelated refactor.

Raw plot tick density adapts to available height (1–5 Y intervals at >=28 DIP spacing); axis titles use concise Chinese labels. This is display-only, retaining original limits, hover lookup, zoom and all underlying measurements.

## Version 1.4 Contracts and Tasks — 2026-10-22

- `BatchCompleteness.Create(SpectrumBatch, CancellationToken)` consumes existing parsed datasets only. It returns ordered file rows with basename, success/error, point count, nullable range and missing-from-union count, plus sorted exact numeric union/intersection arrays. Empty/all-failed batches have empty arrays and nullable ranges. Intersection means common measured points, not overlapping min/max intervals. No intensity values, mutation, reparse or automatic exclusion. Build on a worker; the window owns one immutable batch reference and never caches across imports. A standalone UTF-8 text summary omits absolute source paths; raw XLSX remains unchanged.
- `LiteraturePage.Review` is optional for backward JSON compatibility. `LiteratureReviewRecord` contains machine-text SHA-256, separate draft, explicit enum state and bounded timestamped revision list. Each revision retains prior machine text and draft/status at that time, allowing changed-output review. `LiteratureDocument.TermNotes` is bounded editable plain text. `LiteratureReview.Save` validates translated content before reviewed/questionable status and never writes `ChineseText`. `Refresh` revokes a confirmed state when the machine hash changes, preserves prior drafts/history and marks the draft stale. The UI shows original PDF, extracted source, immutable machine text, and an editable review tab; page numbers are the source association, not inferred paragraph alignment. Save in-session review before navigation/export/retranslation, and include it in dirty-close protection. Export history and terminology with HTML escaping. The worker retains unknown JSON fields; native reload refreshes review fingerprints after success/cancel/error. No review content enters ordinary logs.
- Normal .NET build stays restore-free. `Modules/Mouse/package.json` and lock pin the existing frontend toolchain; local `npm ci` then build can recreate prebuilt assets without the original private project. CI uses Windows .NET Framework compiler, generated synthetic fixtures and no private reference path. Performance checks distinguish machine parsing from user end-to-end time.
- Packaged synthetic work projects reference the generated ASC folder relatively and use the fingerprints of those actual bytes. Pure in-memory legacy demos retain the empty-folder convention. Restore selects the virtual demo only for that empty-folder convention; file-backed synthetic projects verify bytes normally and remain explicitly synthetic. Canonical demo export rejects modified inputs instead of relabeling user data as simulation.
- Use existing `AtomicOutput` for standalone reports and release-safe replacement. Literature JSON checkpoints remain private; exported report is the durable user deliverable. No new external production dependency is introduced.

Tasks: (1) specify contracts; (2) fixed synthetic completeness/review layouts, build and screenshot QA; (3) integrate core services and background UI actions; (4) unit/UI tests and real reference regression; (5) dependency lock/CI/docs/trial record template; (6) version 1.4.0 build/package/install verification; (7) privacy-reviewed public update and release, if authentication/network permits. Do not fabricate human results, Git history, CI runs or external availability. Public-source sanitization continues to omit local reference paths and real sample values.

## Public Source Snapshot Contract — 2026-10-19

- Destination: the owner-approved public `heyinghao8038-collab/FluoLab` repository, main branch. Use an isolated local Git tree; do not stage the original dirty workspace or rewrite any history.
- Input allowlist: `src`, `installer`, selected module assets/source/licenses, build/packaging scripts, synthetic tests, operating guides, and reviewed product/technical documentation. Do not recursively publish the workspace, `dist.zip`, `qa`, outputs, `.git`, local memory, caches, or real data.
- Public tests omit private reference filenames, endpoint measurements and machine paths. The original private reference suite remains unchanged and available locally. Public tests retain parser/alignment/statistics/export/boundary/performance/WPF coverage using generated synthetic data.
- Preserve runtime source bytes and the empty mouse seed. Retain third-party notices. Do not add a source license without an owner decision. Do not ship model/runtime packages or binary releases in Git.
- Synthetic demo files must be generated from `PhDemo`, marked synthetic, and separated from build/test outputs. Public artifacts must not contain absolute source paths or private measurements.
- Verification order: inspect allowlist -> sanitize a copy -> privacy and fingerprint checks -> clean source build -> synthetic/scientific/WPF tests -> Git diff review -> ordinary non-force push -> anonymous remote verification.
- Authenticate through the existing Git Credential Manager, if needed. The owner completes any browser authorization. Never inspect browser profiles, cookies, credentials, tokens, or credential-manager secret output.
- Record final commit and public verification status in local memory. No experimental values or credentials enter publication logs.

## Version 1.3.1 Frame Repair

Use the implicit Window style's SingleBorderWindow and CanResize setters, before HWND creation, and remove the late Loaded-time WindowChrome/content wrapper. Native Windows owns caption visuals, hit testing, movement, resizing and work-area maximize. Existing WPF Closing callbacks remain authoritative. Remove obsolete custom-caption code rather than adding more coordinate offsets.

The footer uses Auto height, a wrapping action row and separate status/log row. Add only a named QuickActionButton style based on the existing button template. No Core/Export data contract changes except the release version string. Regression tests inspect the in-process HWND style bits (caption/system menu/min/max/thick frame), verify content is not replaced after Show, and retain existing close-guard tests. Desktop Computer Use validates native rendering and real pointer input. Content-only renders are useful for layout, not evidence of native chrome.

`WindowPlacement` fits initial normal taskbar windows after HWND creation to Screen.FromHandle(...).WorkingArea converted by the current window DPI. The pure rectangle clamp is tested for 125/150% equivalent work areas, oversized windows and negative monitor origins. Do not persist a MaxHeight/primary-monitor assumption; later moves/resizes remain native. Non-taskbar windows retain explicitly controlled placement, including offscreen render tests.

Tasks: document reproduced failure -> add failing frame/content checks -> replace chrome and restyle fixed-data footer -> screenshot review -> native input checks -> regression/build/package/install verification. Keep the user's running old window/data intact until a normal confirmed close is possible; avoid overwriting a loaded executable.

## Version 1.3 Contracts and Execution Tasks

`WindowControls` wraps WPF content with a vector caption strip backed by WPF WindowChrome and standard WindowState/Close actions. It registers once, retains existing data and closing handlers, and leaves OS message boxes native.

`LiteratureDocument` owns source basename/SHA-256, page count and `LiteraturePage` records (page number, extracted English, translated Chinese, status, numeric-review warning). A bounded local worker produces atomic UTF-8 JSON, per-page PNG and count-only progress. Native .NET services validate inputs, use direct non-shell child processes, keep work caches private, support cancellation, and export escaped text/HTML with no source absolute paths. A completed source page is never silently omitted. Images are original PDF pages, not translated bitmap approximations. CPU translation uses a pinned Argos English-Chinese model through CTranslate2/SentencePiece. No Torch/LLM/API dependency is required.

Optional component ZIP installation uses an exact release digest, bounded extraction, no absolute/traversal/linked entries, versioned staging and an atomic directory activation. Python is embedded only in that pack, not required for fluorescence. Worker makes no network requests and disables Python socket creation after dependency imports; page text stays local. Machine translations remain labeled and number-token differences are flagged, not guessed. PDFium multi-column/table/equation/scan limitations stay visible. SentencePiece receives model bytes to avoid its Windows Unicode filename-loader limitation.

Mouse UI sources are copied into the FluoLab module with an empty seed and separate storage key; no original JSON record is read/copied. Prebuilt React assets are served by a small bounded GET/HEAD-only TCP loopback server at stable `127.0.0.1:41873`, with fixed asset mapping, Host checks and restrictive CSP. No Node runtime is needed on lab PCs. Do not fall back silently to a new port because that would change browser storage origin. Original service/records at the prior origin are not migrated automatically. A busy foreign port must fail clearly. Reuse the original feature rules; no new animal-experiment assumptions.

Sequence: specify -> fixed caption/module/literature mock UI screenshot -> native caption and module services -> private-data-free mouse assets -> actual component/translation integration -> focused tests/visual checks -> full regression/build/installer. Downloads are public dependency/model artifacts only, with source URLs, versions, hashes and licenses recorded. No lab data is sent to those sources.

## Version 1.2 Contracts, Tasks and Verification

- `GroupingTransfer` validates headered TSV (source filename, series, pH, repeat ID/type, inclusion, reason), resolves exact unique filenames and returns copied metadata only after all rows pass. It handles quoted tabs/newlines, enforces size bounds and rejects unknown/duplicate filenames and non-finite provided pH. It does not modify or confirm a session.
- `WorkProjectStore` writes versioned, DTD-disabled XML through atomic replacement. It stores relative source-folder location, synthetic flag, per-file fingerprints, copied metadata and text parameters. Results and grouping/missing consents are deliberately absent. Restoration first verifies the complete source set and fingerprints and returns a fresh unconfirmed session. XML file size/count/text limits protect the input boundary. No arbitrary type deserialization is used.
- Parsed datasets gain a SHA-256 digest over the bytes actually read, computed on the original parse stream. In-memory demo datasets use explicitly tagged SHA-256 of binary64 wavelength/intensity arrays. Results carry that immutable fingerprint string for manifests and review. Raw values and original workbook contracts are unchanged.
- `AnalysisBundleExporter` accepts one current result plus a chart-writer callback, builds a temporary ZIP, and atomically publishes it. Raw source inclusion verifies bytes against the imported digest before writing. Entry names are fixed or validated basenames; no path traversal or public absolute paths. ZIP failures retain existing targets.
- UI owns draft dirty state and safe close behavior. Filter refreshes preserve the edited item; errors remain navigable with search cleared. Confirmation remains explicit. All save/load/bundle I/O is off the dispatcher. Chart hit testing operates in scene coordinates and maps only original extracted measurements, not mean symbols. The trace dialog reuses `SpectrumPlot`.

Tasks: specify -> fixed synthetic UI and screenshot review -> clipboard/project/fingerprint/bundle services -> wire runtime -> focused tests and regressions -> full chart/XLSX/SVG renders -> version 1.2 packaging and docs. No stack migration, new package, network or scientific-method change.

## Version 1.1 Analysis Contract and Tasks

New `PhAnalysis.cs` in Core owns `PhSampleMetadata`, `PhAnalysisParameters`, `PhExtractionResult`, `PhGroupStatistics`, `PhAnalysisResult`, and `PhAnalysisSession`. Metadata is independent of raw chart checkboxes. Result snapshots copy all metadata and scalar values. Raw dataset arrays are read only by the service.

- Metadata identifies one valid source path and stores editable string inputs, repeat enum, inclusion, reason, and review confirmation.
- Parameters contain finite target wavelength, confirmed optional unit, and an explicit allow-missing decision. A validation preview enumerates missing filenames before execution.
- Results contain all valid source files, including excluded/missing rows, nullable extracted intensity and nullable sample SD. Analysis record includes software version, timestamp, confirmation decisions, source counts and exact-match/statistical rules.
- Session revision increments on every edit. Confirmation and result revisions must match. Batch identity uses the original batch object; a different batch cannot reuse results.
- Numeric input parsing is shared with AscParser. Exact wavelength lookup uses Array.BinarySearch over complete raw arrays. Mean/SD use scaled arithmetic to avoid avoidable overflow. Non-finite derived results fail explicitly.
- Group validation is per series+pH; one repeat type per series; duplicate identifiers within a group and obvious hierarchical IDs are unsupported. n remains measurement count even when independent repeats are explicitly selected.

Implementation sequence: (1) synthetic three-step WPF window and vector chart with fixed data; screenshot QA; (2) service validation/extraction/statistics and session invalidation; (3) analysis XLSX using existing SpreadsheetML primitives and atomic file helper; (4) shared chart scene for screen, 2400px PNG and SVG; (5) main-window entry, batch reset, packaged synthetic demo, version 1.1 installer; (6) focused unit/integration, workbook and image QA, regression and install/upgrade checks.

Analysis workbook is a snapshot for review, not a live spreadsheet model. Numeric values are serialized with round-trip precision. Empty intensity/SD remain absent cells with separate status text. All strings use inlineStr. Export helpers write a same-directory temporary file then File.Replace or File.Move; failures preserve existing files.

The plot scene contains only lines, circles, rectangles and text, generated once from the analysis result. WPF and SVG consume the same coordinates. Text uses XML escaping. Legend wraps outside axes; n is listed beneath the chart. Many series/groups enlarge exported canvas height rather than clipping. PNG renders the complete scene at >=2400px width. Unconfirmed units are blank and shown as 荧光强度.

No new packages or runtime are introduced. Existing compiler-compatible C# syntax remains mandatory. Ordinary logs contain only counts/timings, never full intensity arrays. Demonstration uses deterministic synthetic spectra and relative file identifiers.

## 1. Technology

- Runtime: Windows-inbox .NET Framework 4.8, Windows x64.
- UI: WPF with MVVM-style state separation and no external MVVM framework.
- Plotting: a focused WPF `FrameworkElement` renderer with min/max decimation and raw-point hover lookup.
- Workbook generation: direct ISO/IEC 29500 SpreadsheetML ZIP package using inbox XML and compression APIs.
- Tests: project-owned C# test runner and PowerShell acceptance harness.
- Installer: project-owned per-user setup executable.

The implementation avoids Excel COM, WPS automation, package restore, and external runtime installation. Export behavior does not depend on installed office software and cannot leave office processes running.

## 2. Solution Layout

```text
src/
  FluoLab.Core/
  FluoLab.Export/
  FluoLab.App/
  FluoLab.Cli/
tests/
  FluoLab.Tests/
installer/
docs/
```

`FluoLab.Core` has no UI or workbook dependency. `FluoLab.Export` depends only on Core and inbox XML/compression APIs. Both UI and CLI call the same application services.

## 3. Data Contracts

```csharp
public sealed record SpectrumDataset(
    string DisplayName,
    string SourcePath,
    ImmutableArray<double> Wavelengths,
    ImmutableArray<double> Intensities);

public sealed record FileProcessingResult(
    string FileName,
    string SourcePath,
    ProcessingStatus Status,
    SpectrumDataset? Dataset,
    string? ErrorMessage);

public sealed record SpectrumBatch(
    string SourceFolder,
    DateTimeOffset ProcessedAt,
    ImmutableArray<FileProcessingResult> Files);

public interface IAnalysisModule
{
    string Id { get; }
    string DisplayName { get; }
    bool IsAvailable { get; }
    ModuleResult Analyze(SpectrumBatch batch, CancellationToken cancellationToken);
}
```

Public boundary rules:

- Arrays have identical non-zero length.
- Wavelengths are finite, unique, and strictly ascending.
- Intensities are finite.
- Paths are normalized absolute paths.
- Results represent both successes and failures; one failure never aborts the batch.

## 4. Parsing

1. Enumerate only top-level files whose extension equals `.asc` case-insensitively.
2. Sort by filename with `StringComparer.OrdinalIgnoreCase`.
3. Parse files on background tasks with bounded parallelism.
4. Open with `FileShare.ReadWrite | FileShare.Delete` so a recently created instrument file can be read safely.
5. Detect encoding using BOM when present, otherwise try UTF-8 and the current Windows ANSI code page.
6. Locate the first trimmed line exactly equal to `#DATA`.
7. For every following non-empty line, require exactly two whitespace-separated values and parse with invariant culture, falling back to current culture only for compatibility.
8. Reject non-finite values and duplicate wavelengths.
9. Sort valid points by wavelength and retain original `double` values.

Cancellation is checked during enumeration and line parsing. Progress is reported after each file.

## 5. Performance

- Limit parsing concurrency to `min(Environment.ProcessorCount, 4)` to avoid excessive disk contention.
- Never block the WPF dispatcher on parsing or export.
- Keep datasets immutable after parsing.
- Reuse plot series and update visibility rather than rebuilding the entire plot for each checkbox.
- Debounce repeated selection changes by 80 ms.
- Use min/max bucket decimation for preview when a series exceeds the pixel-informed display budget.
- Hover lookup uses binary search over the original wavelength array, never decimated points.
- Cap simultaneous preview selection at 60; all valid files remain exportable.

## 6. Workbook Export

Build the union of wavelengths with a sorted set. Each source dataset is indexed by wavelength for alignment. The workbook is first written to a temporary file in the destination directory and then atomically moved into place.

`汇总数据`:

- Cell A1: `Wavelength (nm)`.
- B1 onward: display names without `.ASC`.
- A2 downward: numeric wavelength union, ascending.
- Intensity columns: exact `double` values or blank.
- Freeze row 1 and column 1.
- Enable wrapped headers and an autofilter.
- Use a neutral header fill, thin borders only around the header, and readable widths.

`处理日志`:

- Source folder, timestamp, discovered/succeeded/skipped counts.
- One row per source file with status and error reason.

Worksheet names and core layout are API-compatible with the legacy output.

## 7. UI State and Flow

The window owns one batch view model. Import starts a cancellable task and transitions:

```text
Empty -> Loading -> Ready
                  -> ReadyWithErrors
                  -> EmptyOnCancel
```

Export is available only when at least one valid dataset exists. A second folder drop cancels the previous import after confirmation only if work is still active; otherwise it replaces the batch directly.

UI text is Chinese. Error messages explain what the user can do next and never expose stack traces by default.

## 8. CLI Contract

```text
FluoLab.Cli.exe <source-folder> [output.xlsx]
```

- If output is omitted, use the standard timestamped filename in the source folder.
- Exit `0`: workbook created.
- Exit `1`: invalid arguments or no valid ASC files.
- Exit `2`: export failure.
- Print one concise line per skipped file and a final count summary.

## 9. Packaging

- Compile for `AnyCPU` with 64-bit preference on supported systems.
- Installer writes only to per-user locations.
- Default install root: `{localappdata}\Programs\FluoLab`.
- Upgrade identity remains stable across versions.
- No service, scheduled task, driver, registry shell extension, network permission, or background updater.

## 10. Verification

- Unit-test parsing and wavelength alignment.
- Integration-test workbook structure and exact numeric values.
- Run the real 78-file reference batch.
- Generate and test a 100 × 3,101 synthetic performance batch.
- Render both workbook sheets and inspect for truncation or invalid values.
- Run UI screenshot checks using fixed data before and after real-data integration.
- Install into a disposable per-user location, launch, and uninstall.
