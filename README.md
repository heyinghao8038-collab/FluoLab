# FluoLab

Public source: <https://github.com/heyinghao8038-collab/FluoLab>. Version 1.4.2 adds the current-batch dashboard. Download published installers and demo packages from <https://github.com/heyinghao8038-collab/FluoLab/releases>. Publication uses a separate reviewed tree and excludes real laboratory data, personal memory/paths, generated binaries and optional runtime/model archives. See `docs/Source-Publication.md`.

Local-only Windows fluorescence-spectrum organization and traceable pH descriptive analysis. The user interface and analysis reports are Chinese. Built for Windows 10/11 x64 with the inbox .NET Framework 4.8, WPF and C# compiler. No Office automation, cloud service, telemetry or production package restore.

## Build and verify

Run in Windows PowerShell from the project root:

```powershell
.\build.ps1
.\tests\Run-NewAppTests.ps1
.\artifacts\build\FluoLab.UiTests.exe
```

Distribution: `dist/FluoLab/FluoLab.exe` (keep sibling DLLs and Modules) and the per-user `dist/FluoLab-Setup-1.4.2.exe` installer. Use `build.ps1 -DistributionDirectory <folder>` when an existing portable instance is running. The private workspace keeps local builds separately under `dist/v1.4.2`. See the Chinese operating guide for scientific rules, work projects and export bundles.

## Version 1.4

Version 1.4.2 adds a collapsible current-batch sidebar beside the raw chart: discovered/valid/failed counts, failure categories, measured-point totals and exact wavelength coverage. It deliberately has no success rate or quality score. Preview/search only change the preview, never batch totals or export scope. At narrow widths the same overview opens in a movable owned window. Loading clears old summaries; revision guards prevent stale worker results. The original raw XLSX and scientific calculations are unchanged. Version 1.4.0 downloads remain available separately.

Read-only batch completeness summarizes exact measured-point coverage without changing raw data, inclusion or workbook contracts. Offline literature review keeps machine output separate from the human draft, records explicit review states and revision history, revokes review after changed machine output, and exports page-linked audit reports with manual terminology notes. It does not automatically align paragraphs or replace terms. Reading drafts are session-local until HTML/TXT export. Generic model output is not validated scientific translation.

The Windows workflow builds with the existing compiler and runs generated synthetic and WPF checks; the frontend job restores its lockfile and performs strict TypeScript/build checks. Actual CI execution and release availability must be checked in the repository, not inferred from this file. Human/other-PC trials remain pending; use `docs/Trial-Record.md`.

## Scope

ASC import, aligned raw XLSX export, exact original wavelength extraction, explicit grouping, arithmetic mean and sample SD, vector response graphs, source traceability, local draft resumption and opt-in raw-source analysis bundles. No fitting, significance tests, inferred experimental independence or automatic preprocessing.

The synthetic demo is explicitly labeled and may be used without exposing laboratory measurements. Run `FluoLab.exe --ph-demo`.

## Local laboratory tools (1.3)

The laboratory-tools entry opens separate PDF and mouse workflows. WPF windows expose minimize, maximize/restore and close while retaining draft guards.

PDF translation requires the optional `dist/FluoLab-Literature-CPU-1.0.zip` (~122 MiB; allow 1 GiB free disk for staged installation). Select it in the literature window to verify its release hash and install per user. It includes embedded Python, PDFium and an attributed OPUS-MT English-Chinese neural translation model. No account, external model endpoint or online fallback is used. An x64 Microsoft Visual C++ runtime may be required on other PCs; the probe reports missing native dependencies. Fluorescence remains independent of this optional runtime. Scanned OCR, translated-PDF layout reconstruction and resumable reading projects are not implemented. Page-linked HTML/TXT and original-page images support review; generic machine output is not a certified scientific or medical translation.

Mouse management reuses the existing cage/group/breeding/log UI with an empty seed and separate storage key. A GET/HEAD-only server binds `127.0.0.1:41873`, checks Host and serves fixed assets under restrictive CSP. Records belong to the chosen browser/profile's localStorage, not the installer. Export JSON backups before clearing storage or moving PCs. Original records are untouched; migration requires validated JSON import with replacement confirmation. No Node runtime is shipped. This is not multi-user synchronization or regulated animal-facility compliance software.

The normal .NET build uses prebuilt mouse assets without package restore. Developer rebuild: run `npm ci --ignore-scripts` then `npm run build` in `Modules/Mouse`; the lockfile pins React 19.2.6, Vite 8.0.13 and TypeScript 5.9.3. It performs strict checks and writes `artifacts/mouse-bundle/app.js`; copy it to `Modules/Mouse/app.js` before building. No private project directory or records are needed. `scripts/Build-LiteraturePack.py` builds the optional pack and records versions, sources, hashes and licenses. Third-party notices ship with both modules.

## Sharing and source control

Keep raw/private outputs, local memory, generated binaries and QA runtime packages out of public source control. Review existing reference-fixture paths and scientific acceptance documentation before publishing. No remote repository is created or uploaded automatically. Select a distribution/source license deliberately before public release; this repository does not invent an open-source license on the owner's behalf.

See `docs/Usability-Study.md` for unperformed human/other-machine validation. Built-in numerical and UI tests do not substitute for those studies.
