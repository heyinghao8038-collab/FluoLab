# FluoLab UI Specification

This file is the visual source of truth for the first release.

## Version 1.4 Completeness and Literature Review

Keep Theme colors, native frame and wrapping Quick Actions. Add a secondary `批次完整性` action, disabled before import/during loading. Open an owned snapshot window with count/coverage summary, explicit non-QC explanation, filename filter and a virtualized read-only grid (file/status/points/range/missing count). Show empty/all-failed/no-common-point states in text; export a separate summary, never alter the raw workbook. Long names scroll and have full tooltips.

Literature retains its two original read-only tabs and adds `人工核对` and `术语备注`. The review tab has a wrapping explicit state selector/save action, a stale-machine warning and editable human draft, with an expandable scrollable revision history. The machine translation is never edited in place. Notes explain that terminology is manually maintained and not used to silently rewrite model output. Commands/export are disabled during worker I/O; review edits participate in the existing unsaved guard. Fixed synthetic screenshots cover normal, long text and narrow/high-scale states before connecting real documents.

## Version 1.3.1 Native Frame and Footer Override

This supersedes the custom caption strip below. Use the ordinary Windows title bar and border, including system caption buttons and native title drag/resize behavior. Preserve the light Theme client area. The release title includes 1.3.1 for main-window identification. Do not use a PNG title-bar reference as a UI asset.

Initial normal taskbar windows fit the actual monitor work area with an 8-DIP margin and current DPI conversion. A 900-DIP fixed default must not push the title above a scaled screen. Fit once, not as a permanent MaxHeight; retain ordinary native movement/resizing afterward. Actual desktop captures, not only content-only mock renders, must show the entire caption strip.

The footer is a white rounded card with a light divider outline and 12-pixel padding. Its wrapping top row shows Quick Actions, the current generic-organizer badge, and existing demo/open-project/laboratory actions. Actions use 14-point semibold blue text, blue outlines and >=44-pixel height; laboratory tools has a pale-blue fill. Status text is on a separate lower row, with output-folder/log controls on the right. Export remains the main solid-blue action in the header. Validate narrow/high-DPI wrapping rather than clipping.

## Version 1.3 Caption and Module Source of Truth

Follow the provided Windows caption reference: three right-aligned 46x32 controls for minimize, maximize/restore and close, gray hover and red close hover. Use vector lines/rectangles, not a screenshot asset. The rest of the caption supports native drag/double-click actions. Preserve visible keyboard focus and Chinese accessible names. Keep all three visible at minimum sizes.

A separate laboratory module window uses the existing white/gray/blue Theme cards. Literature has a wrapped command bar (component install/status, PDF import, previous/next page, page/all translation, cancel, export), actual PDF page on the left, source/Chinese text tabs on the right, page progress and a persistent review warning. A fixed synthetic article demonstrates the static state before real integration. Mouse management opens the local browser view and clearly states independent browser storage and backup migration. No confidential seed content is used in mocks or distribution.

## Version 1.2 Workflow Source of Truth

The existing Theme palette, rounded surfaces and typography remain authoritative. Analysis content uses a white card and padded accent-selected step tabs. Header actions wrap at narrow widths. Grouping starts with a compact filename search, review filter and live completed/problem counts; status is beside the source column. Double-clicking row status or a next-problem action navigates to a specific editable field. Bulk tools and TSV transfer are secondary controls.

Results contain three subviews (response chart, group statistics, sample detail), never one long stacked page. A Viewbox fits the entire vector scene in the available chart viewport by default; a readable-size mode scrolls without losing content. Mouse hover reports exact original extracted values; click opens a source chooser/trace view. Export remains full-size and independent of screen fitting. Header includes save/open work project; closing a dirty draft offers save/discard/cancel. Synthetic fixed-data screenshots precede integration of persistence and real batch data.

Minimum analysis size is 760x440 logical pixels; main size is 800x460. At 1280x720 with 150% scaling, wrap action bars and keep the active work area usable. Large legends can be small in fit mode, so readable-size mode remains available. Export fonts are not reduced to screen scale.

## Version 1.1 Analysis Window

Reuse Theme tokens. Add one secondary `pH 响应分析` entry beside original export. It is disabled without a valid batch. Open a modeless owned window with three selectable steps and a persistent status strip. Minimum logical size is 1000x620; content uses scrolling when OS scaling reduces available space.

Step 1: editable grid (source file read-only; series, pH, repeat ID, repeat type, analysis participation, reason, validation). A compact batch toolbar applies series/type/participation/reason to selected rows. No automatic series assignment. A separate explicit confirmation action validates every row and affirms equal conditions within each series and one-level repeat design. Any edit revokes this confirmation.

Step 2: target wavelength in nm, optional confirmed intensity unit, exact-match count, per-file missing list, analysis button. Partial matching opens a complete scrollable missing-file confirmation dialog. Changing any input immediately hides old results and disables export.

Step 3: chart, group table and per-file extraction table; separate XLSX, PNG, SVG buttons. Chart shows actual pH positions, raw circles, mean diamonds, SD bars only for n>=2, series legend, wavelength and group n. No fit/connection line. Scroll container retains chart height; export includes all notes/legend. n is labeled 测量次数.

Synthetic QA states: incomplete grouping, confirmed grouping, partial/all missing, singleton SD, two series with long names, stale result, empty and failed export. Public demo labels all states 模拟数据. No absolute paths appear in demo screenshots or reports.

## Visual Tokens

- Background: `#F5F5F7`
- Primary surface: `#FFFFFF`
- Secondary surface: `#F2F2F4`
- Primary text: `#1D1D1F`
- Secondary text: `#6E6E73`
- Divider: `#D2D2D7`
- Accent: `#0071E3`
- Success: `#2E9E5B`
- Warning: `#B26A00`
- Error: `#D92D20`
- Corner radius: 14 px for cards, 10 px for controls, pill radius for status badges.
- Base spacing: 8 px; common gaps: 12, 16, 24, and 32 px.
- Typography: Segoe UI Variable when available, Segoe UI fallback.
- Shadows: subtle single-level card shadow only; no gradients or glass effects.

## Window Layout

Minimum size: 1120×680. Default: 1440×900.

```text
+-------------------------------------------------------------------+
| FluoLab 荧光数据分析器       Local-only badge       导出 Excel    |
+-------------------------------------------------------------------+
| Drop/import card: folder path, choose folder, progress, summary    |
+--------------------------+----------------------------------------+
| Files                    | Spectrum preview                       |
| Search/status controls   | Plot toolbar: reset / point details    |
| Select all / clear       |                                        |
| [x] file 1   success     |              plot                      |
| [ ] file 2   warning     |                                        |
| ...                      |                                        |
+--------------------------+----------------------------------------+
| Current module: 通用整理 | status / errors / privacy note         |
+-------------------------------------------------------------------+
```

- Header height: 64 px.
- Import card height: 104 px.
- Left pane: 340 px at default width, resizable from 280 to 460 px.
- Main plot receives all remaining space.
- Footer/module bar: 48 px.
- The primary Export button is the only filled blue button.

## Required States

### Empty

- Import card contains a large but restrained folder icon.
- Text: `拖入包含 ASC 文件的文件夹`.
- Secondary action: `选择文件夹`.
- Export disabled; file list and plot show concise empty guidance.

### Loading

- Keep the existing batch visible until the first new result arrives.
- Show file progress and `取消导入`.
- Selection actions and export are disabled while the new batch is incomplete.
- The dispatcher must remain responsive.

### Ready

- Import card becomes compact and shows folder, file count, valid count, and elapsed time.
- File rows expose checkbox, color marker, truncated display name with full tooltip, point count, and status.
- Plot uses thin anti-aliased lines; legend appears only when useful and can scroll.

### Ready with errors

- Do not use a blocking modal after the batch completes.
- Show an amber summary badge and an expandable processing-log panel.
- Each failed row has a concise reason and remains unselected.

### Exporting

- Disable the Export button and show inline progress.
- Success uses a non-blocking confirmation with `打开所在文件夹`.
- Failure presents an actionable Chinese message.

## Interaction Rules

- Dropping a folder anywhere over the import card highlights only that card.
- Checkbox changes update the plot within 100 ms after input settles.
- Hover tooltip shows display name, wavelength with source precision, and raw intensity.
- Ctrl+mouse wheel zooms; drag pans; double-click or toolbar button resets.
- Keyboard focus is visible. All actions have accessible names.
- Colors are not the only status indicator.
- No important action may rely solely on hover.

## Screenshot Acceptance

At 1280×720 and 1920×1080, and Windows scaling at 100%, 125%, and 150%:

- Header, import controls, and Export button are always visible.
- No labels clip or overlap.
- The file checkbox, status, and filename remain distinguishable.
- The plot retains a usable minimum region.
- Empty, loading, ready, error, and exporting states match the same spacing and hierarchy.

