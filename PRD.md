# FluoLab 荧光数据分析器 — 产品需求文档

## Version 1.4.2 Release Correction — 2026-10-22

The initial 1.4.1 tag failed hosted UI verification because the offscreen test inherited the runner monitor's size cap. Keep that tag as a failure record and publish 1.4.2 instead, after explicit logical viewport tests and all remote jobs pass. This is a test portability/version correction, not a scientific or feature expansion. Do not remove assertions or modify runtime window-fitting behavior to satisfy CI.

## Version 1.4.1 Public Release — 2026-10-22

The owner now requests a GitHub update. Publish the reviewed 1.4.1 source from the isolated publication tree and create a new immutable version tag/release, preserving 1.4.0. Include CI-built installer, portable ZIP, synthetic demo and checksums. Exclude private data, reference assertions, local paths and memory. The existing optional CPU component remains compatible and separately distributed; do not imply that the main installer includes it. Acceptance requires public source, successful remote checks and anonymously accessible release assets, not merely a local push.

## Version 1.4.1: Current-Batch Dashboard — 2026-10-22

Add a teacher-facing batch overview beside the raw spectrum preview. Show discovered/valid/failed ASC counts (no success rate), total measured points across valid files, union wavelength range, exact common-point count, equal/different wavelength coverage, failure categories and current preview count. All batch metrics cover every discovered ASC regardless of preview/search selection. Do not add expected file counts, manual exclusion, quality scores or experimental-completeness claims. Failure means parser rejection only; negative intensity or unequal coverage is not failure. Files are never deleted.

Click failure count/category to inspect filenames and original reasons through the existing processing-log view; click coverage to open existing completeness details. Reuse Theme. Wide windows show a collapsible 280-DIP right sidebar. Below 1200-DIP window width, collapse it and open the same overview in a movable owned window on demand, preserving chart width. Loading hides prior batch statistics; replacement/cancellation/error cannot apply stale summaries. Empty/all-failed/no-common-point states must be explicit.

Acceptance: fixed synthetic layout review before runtime integration; count/point/coverage/category correctness; preview/search independence; filtered failure details; loading/replacement stale guards; collapse/narrow window behavior; raw/pH regressions and required size/scale renders. Deliver local 1.4.1 portable and installer, preserve running old binaries. No automatic public publication is required in this increment.

## Version 1.4: Finals Readiness — 2026-10-22

Prioritize verifiable laboratory workflow improvements over new scientific methods. Add a read-only batch completeness view (file status, point count, numeric wavelength union/intersection and per-file missing coverage). It must not label negative intensities as bad, infer instrument quality, exclude files, or change the two-sheet raw export.

Add page-linked human review to offline literature reading: retain extracted source and original machine output, edit a separate human draft, record explicit unreviewed/reviewed/questionable status with timestamped revision history, and keep document-level terminology notes. Notes are manual reference material, not model constraints or automatic substitutions. Review applies to one exact machine-output fingerprint; retranslating changed output revokes confirmation while retaining the earlier draft/history for comparison. Page references are exact; automatic paragraph alignment is not claimed. Export all provenance and review information in escaped HTML/TXT. Drafts remain session-local; export before closing. No new model, OCR, cloud, inference endpoint or medical accuracy claim.

Engineering deliverables: reproducible mouse frontend package manifest/lock, Windows CI for existing compiler and synthetic tests, versioned portable/installer/demo artifacts and SHA-256 manifest, clear installation and trial instructions. Publish only reviewed synthetic/public source and release files through the isolated publication tree. Preserve real reference fixtures locally. Human trials and other-PC/physical-DPI compatibility remain pending until actually performed.

Acceptance: core completeness edge cases; review separation/history/changed-output invalidation/legacy JSON/report escaping; unchanged scientific, real-78 and synthetic-100 regressions; fixed-data UI screenshots before runtime integration; review/extraction UI control tests; release archive and installer checks. CI and remote release availability are accepted only after external verification, not from local file existence.

## Public Source Publication — 2026-10-19

The owner has explicitly approved publishing a sanitized source snapshot to `https://github.com/heyinghao8038-collab/FluoLab`. The publication includes application source, build/test instructions, required third-party notices, and clearly labeled synthetic examples. It excludes real spectra, private reference assertions/paths, animal records, personal workspace memory, credentials, caches, generated binaries, and optional model/runtime archives.

Prepare an independent release tree under the ignored `artifacts` directory so the existing uncommitted workspace and private verification fixtures remain intact. Do not change scientific algorithms, choose a new source license, publish an installer/model pack, or claim new compatibility results as part of this task.

Acceptance: the sanitized source builds on this Windows machine; public synthetic and WPF tests pass; original application source matches the local release; publication files pass private-data/secret-pattern checks; the remote main branch contains the intended files; an anonymous read of the repository and README succeeds. The public repository must not be left as an empty shell or contain only a source ZIP.

## Version 1.3.1: Visible Native Window Frame and Quick Actions

The user's actual desktop screenshot and native capture reproduce a missing caption strip despite the synthetic WPF render and visual-tree tests passing. Restore a standard Windows-managed title bar and resizable border for every WPF window. Do not emulate caption buttons in client content or replace window content during Loaded. The native frame must expose minimize, maximize/restore and close, support title dragging/double-click and edge resizing, respect the desktop work area, and preserve existing close guards.

Native capture identifies the startup trigger: the 900-DIP default window exceeds the scaled desktop work area, producing a negative on-screen Y origin and hiding the title bar. Fit initial taskbar windows to the actual HWND monitor's work area using the current DPI, with a small safe margin. This is startup placement only, not a permanent maximum size. Respect explicit non-taskbar tool/QA placement.

Promote the existing footer actions into a white rounded Quick Actions card: visible section label, blue outlined buttons at least 44 logical pixels high with 14-point labels, wrap at narrow widths, and a distinct light-blue laboratory-tools action. Keep source-processing status and logs in a separate row. Do not introduce new functions or change data/analysis/export contracts.

Acceptance: native screen capture of the actual executable, native caption button actions, title drag and edge resize; normal and maximized desktop states; minimum-size and 1280x720/1920x1080 synthetic scaling checks; existing close-guard/scientific tests; a new 1.3.1 executable and installer with unchanged optional component compatibility. Do not infer native-frame success from a detached WPF content render.

## Version 1.3: Local Laboratory Modules and Window Controls

The user now authorizes implementation of the previously planned literature and mouse-management modules, plus all three visible window buttons. The existing fluorescence stack and scientific contracts remain unchanged. No cloud, account or data upload is introduced. The user accepts an optional first-download offline component pack for PDF rendering and English-to-Chinese translation.

- A module window provides distinct literature and mouse-management entry points without crowding the raw-spectrum workflow. Implemented capabilities only are exposed.
- Main and module WPF windows show minimize, maximize/restore and close controls. Close must call the existing close handlers, including unsaved draft protection. Drag, resize and double-click maximize remain conventional.
- Literature: import a local English text PDF, view actual page images, read extracted source text beside machine Chinese translation, translate a page or the entire document, cancel and export page-linked bilingual HTML/text. Source PDF remains unchanged. Show local component status, engine/model provenance and machine-translation review warnings. Unsupported scanned text/OCR, password-protected and malformed PDFs receive clear actionable messages; do not claim layout-preserving translated PDFs.
- An optional self-contained CPU component pack provides Python, PDFium, tokenizer and translation model. Install only after explicit selection, verify the release SHA-256 before extraction, preserve licenses and reject unsafe paths. Translation uses no API/model server and has no online fallback. Do not silently download or execute remotely supplied code.
- Mouse management reuses the existing local UI logic but starts with an empty independent database. Do not copy its imported records, existing localStorage or private seed data. Support the existing cage/group/breeding/history/search/JSON-backup workflow through a loopback-only browser module with a stable origin. Existing records can be moved only by explicit user backup import after a replacement confirmation. Original project and records remain untouched.
- Acceptance: existing build/scientific tests and real/100-file regressions; all caption button actions/dirty close; fixed UI screenshots; PDF page/text extraction, real offline inference, cancellation, blank/scanned/malformed states and escaped bilingual export; component hash/path validation; mouse UI empty start/new cage/backup restore with only synthetic records, no remote requests; install/upgrade and portable assets. Human domain translation accuracy and other-PC tests remain explicitly unverified.

## Version 1.2: Workflow Efficiency and Reproducibility

This increment adds filename search, incomplete/error/review filters, completion counts, actionable cell navigation and validated TSV clipboard transfer to grouping. Clipboard rows map by exact source filename, never visual row position. Parsing a paste is transactional and never confirms experimental meaning.

Results use separate chart/statistics/details views. The chart defaults to fit-to-window with full axes and legend, offers readable-size scrolling, and links original point hover/click to the corresponding original spectrum. Overlapping points expose every matching source in a list. Existing chart exports retain the complete vector scene.

Local `.fluolab` work projects preserve grouping, exclusions, wavelength and unit. Source fingerprints detect changed/replaced content. Opening requires source verification and fresh human grouping confirmation; no cached results or missing-file consent are restored. Unfinished/invalid metadata may be saved. Unsaved edits prompt before close or batch replacement. A moved source folder can be explicitly relocated. This is one-batch work resumption, not a cross-batch experimental protocol.

One analysis ZIP contains XLSX, PNG, SVG, a Chinese readme and a source-fingerprint manifest from the same current result. Raw ASC inclusion is opt-in and byte fingerprints are verified before inclusion. No absolute local paths enter analysis ZIPs. Existing output remains intact on any failure or declined overwrite. Synthetic exports remain labeled.

Acceptance: unchanged original tests; transactional TSV cases; filter and error navigation; project roundtrip/incomplete draft/corrupt schema/changed or missing source/relocation; dirty close cancel; exact point traceability; full chart at target sizes/scales; ZIP contents/hash agreement/raw opt-in/locked file; rebuilt portable app and installer. Human usability and native WPS/other-machine validation must remain explicitly pending unless actually performed.

## Version 1.1: pH Response Analysis

This release adds a separate three-step workflow: sample grouping, exact wavelength selection, and descriptive results. It reuses the current SpectrumBatch without changing raw spectra or preview selection. The source export contract remains unchanged.

- Every valid file has editable series, finite numeric pH, repeat identifier, repeat type, inclusion state, and exclusion reason. Filename suggestions require explicit review confirmation. Series is never inferred.
- Only explicit `pH = <number>` tokens and a trailing `-<integer>` are suggested. Ambiguous or absent tokens remain blank.
- Included rows require a series, pH, repeat identifier, and confirmed grouping. Excluded rows require a reason. Unknown repeat type is permitted and n is always described as measurement count.
- A series must use one repeat type. Duplicate repeat identifiers within series+pH or identifiers expressing nested measurements are rejected; users must resolve the design, not silently aggregate it.
- Extraction uses exact numeric equality with raw wavelengths. Partial matches require a file-by-file exclusion preview and explicit confirmation. All-missing and empty analyses are blocked.
- Group by exact series and numeric pH, sort pH numerically, calculate arithmetic mean and sample SD (n-1). n=1 has blank SD and no error bar. Negative intensity remains valid.
- Show raw points, means and mean±SD with series legend, actual numeric pH spacing, wavelength, optional confirmed intensity unit and per-group n. No fitting or inferential claims.
- Separate analysis export produces sample detail, group statistics and analysis record sheets, plus full-layout PNG and real-vector SVG. Text is escaped and stored as text. Existing files are replaced atomically only after confirmation.
- All parameter edits invalidate the result immediately. A new batch closes the old analysis window. Exports require the current confirmed revision.
- Public demo inputs and outputs are explicitly synthetic and contain no real experimental data or absolute local paths.

Acceptance includes hand-computable 1,2,3 => n=3, mean=2, SD=1; n=1; exact-vs-near wavelength; missing values; exclusions; grouping and type validation; stale-state prevention; locked exports; source contract regression; synthetic UI and PNG/SVG render checks at required sizes/scales; real 78-file and 100x3101 regression. Parse, analysis, and end-to-end timings are reported separately.

Out of scope: pKa, calibration, detection limits, significance tests, automatic outlier removal, preprocessing, nested repeats, saved protocols, cloud and additional formats. No future-feature placeholders are exposed.

## 1. 产品目标

把实验室成员反复打开 ASC、复制两列数据、手工对齐波长和整理 Excel 的流程，压缩成一次拖入文件夹、检查曲线、导出结果。第一版重点是可靠、流畅和易学，并为后续 pH、线性、选择性等分析保留清晰扩展入口。

## 2. 用户与场景

- 3–5 名实验室成员，各自在 Windows 10/11 64 位电脑上使用。
- 部分电脑安装 Microsoft Excel，部分安装 WPS，部分可能都未安装。
- 用户通常一次处理几十份 ASC，实际批次可达 78 份，设计上支持 100 份。
- 数据属于本地科研数据，不需要账号、服务器、云同步或网络上传。

## 3. 第一版范围

### 导入与解析

- 拖入一个文件夹，或点击按钮选择文件夹。
- 只读取该文件夹直属的 `.ASC` 文件，不递归扫描子文件夹。
- 后台解析，显示文件数量、总体进度、成功/失败状态，并可取消。
- 文件按不区分大小写的文件名字母顺序排列。
- 搜索首个准确的 `#DATA` 标记，读取后续两列有限数值。
- 缺少标记、数据为空、数据行非数值或重复波长时整份文件失败；其他文件继续处理。

### 文件与曲线

- 左侧显示文件列表、状态、全选、清空和有效/异常数量。
- 用户可勾选最多 60 条预览曲线；超出时给出明确提示，不影响导出。
- 右侧实时叠加曲线；大量点位时采用仅用于绘图的降采样。
- 悬停提示必须来自最近的原始测量点，显示完整文件名、准确波长和原始荧光强度。
- 支持缩放、平移和恢复视图，切换勾选时界面不冻结。

### 导出

- 一个明确的“导出 Excel”主按钮。
- 直接生成标准 `.xlsx`，不调用 Excel 或 WPS。
- 保存窗口默认文件名：`批次文件夹名_ASC汇总_时间戳.xlsx`。
- 仅在用户明确确认后覆盖已存在文件。
- `汇总数据`：A1 为 `Wavelength (nm)`；B1 起为不含扩展名的源文件名；以下按波长数值并集升序对齐，缺失位置留空。
- `处理日志`：记录源文件夹、运行时间、发现/成功/跳过数量和每份异常文件的原因。
- 冻结首行首列，提供可读列宽、换行表头和稳定数值格式；不嵌入图表。
- 导出始终包含全部有效文件，不受预览勾选影响。

### 分析扩展

- 首页提供“通用整理”模块。
- 1.1 版启用独立的 pH 响应分析；其他尚未实现的分析不显示占位入口。
- 新模块只消费统一数据集对象，不自行重复解析 ASC。
- 保留命令行批处理入口。

### 安装与隐私

- 提供 Windows x64 自包含安装包，按当前用户安装，不需要管理员权限。
- 创建开始菜单入口和可选桌面快捷方式。
- 第一版手动更新，接受未签名程序的系统提示。
- 应用不上传数据、不收集遥测、不需要登录。

## 4. 非目标

- 通用整理不计算派生值；1.1 的 pH 模块仅提供精确波长提取、均值和样本 SD，不提供归一化、pKa、线性拟合或选择性评分。
- 不支持 ASC 以外的数据格式。
- 不提供云端协作、账户系统、自动更新、深色模式或移动端。
- 不把曲线图写入导出的工作簿。

## 5. 完成标准

- 真实 78 文件全部成功，均为 3,101 点、490.0–800.0 nm，导出为 3,102 行 × 79 列。
- Reference endpoint assertions remain in the private local acceptance suite; public validation uses synthetic observations.
- 100 份 × 3,101 点的代表性批次解析期间 UI 可操作、进度可见，目标在典型电脑五秒内完成。
- 不同波长范围和缺点数据正确按并集对齐。
- 缺少 `#DATA`、非数值、重复波长、空目录、取消保存、中文路径、长文件名和被占用文件均有明确结果。
- Excel 与 WPS 均能打开输出；工作表名称、尺寸、数值和样式符合本需求。
- 在 1280×720、1920×1080、125% 和 150% 缩放下，无文字截断、控件重叠或主要操作被遮挡。
- 安装、启动、升级覆盖和卸载均不要求管理员权限。

