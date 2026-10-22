using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluoLab.Core;
using FluoLab.Export;
using Microsoft.Win32;

namespace FluoLab.App
{
    internal sealed class LiteratureWindow : Window
    {
        private readonly string workerScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules", "Literature", "worker.py");
        private string componentRoot = LiteratureComponent.InstalledRoot;
        private readonly WrapPanel commands = new WrapPanel();
        private readonly TextBlock componentLabel = Note("");
        private readonly TextBlock documentLabel = Note("未导入文献");
        private readonly TextBlock pageLabel = Note("");
        private readonly TextBlock status = Note("先安装配套组件，再导入本地英文 PDF。所有处理离线完成。");
        private readonly Image pageImage = new Image { Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center };
        private readonly TextBox original = ReadingBox();
        private readonly TextBox chinese = ReadingBox();
        private readonly TextBox reviewDraft = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontSize = 15, MaxLength = 100000 };
        private readonly TextBox termNotes = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontSize = 14, MaxLength = 16000 };
        private readonly TextBox revisionHistory = ReadingBox();
        private readonly ComboBox reviewState = new ComboBox { MinWidth = 135, Margin = new Thickness(0, 0, 8, 6) };
        private readonly Button saveReview = new Button { Content = "保存本页核对", Margin = new Thickness(0, 0, 0, 6) };
        private readonly TextBlock reviewLabel = Note("");
        private readonly TabControl texts = new TabControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        private readonly Button importButton, previousButton, nextButton, pageButton, allButton, cancelButton, exportButton, installButton;
        private LiteratureDocument document;
        private string workFolder, sourcePath, documentPath;
        private int pageIndex;
        private bool verified, busy, dirty, qaMode, refreshingReview, reviewPending;
        private CancellationTokenSource cancellation;

        public LiteratureWindow()
        {
            Title = "FluoLab 文献阅读与离线翻译"; Width = 1260; Height = 840; MinWidth = 760; MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var intro = new StackPanel(); intro.Children.Add(new TextBlock { Text = "文献阅读 · 本机英中翻译", FontSize = 20, FontWeight = FontWeights.SemiBold });
            intro.Children.Add(componentLabel); intro.Children.Add(documentLabel); root.Children.Add(intro);
            installButton = Button("安装离线组件", InstallComponent); importButton = Button("导入 PDF", ImportPdf);
            previousButton = Button("上一页", delegate { Navigate(-1); }); nextButton = Button("下一页", delegate { Navigate(1); });
            pageButton = Button("翻译当前页", delegate { Translate(false); }); allButton = Button("翻译全文", delegate { Translate(true); });
            cancelButton = Button("取消任务", delegate { if (cancellation != null) cancellation.Cancel(); }); exportButton = Button("导出中英对照", Export);
            foreach (var button in new[] { installButton, importButton, previousButton, nextButton, pageButton, allButton, cancelButton, exportButton }) commands.Children.Add(button);
            Grid.SetRow(commands, 1); root.Children.Add(commands); Grid.SetRow(pageLabel, 2); root.Children.Add(pageLabel);
            var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition()); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) }); body.ColumnDefinitions.Add(new ColumnDefinition());
            var pages = new ScrollViewer { Content = pageImage, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = Theme.SecondarySurfaceBrush };
            pages.SizeChanged += delegate { pageImage.Width = Math.Max(100, pages.ActualWidth - 20); };
            body.Children.Add(pages); var divider = new GridSplitter { Width = 6, HorizontalAlignment = HorizontalAlignment.Center, Background = Theme.DividerBrush }; Grid.SetColumn(divider, 1); body.Children.Add(divider);
            texts.Items.Add(new TabItem { Header = "机器译文", Content = chinese }); texts.Items.Add(new TabItem { Header = "原文", Content = original });
            texts.Items.Add(new TabItem { Header = "人工核对", Content = BuildReviewPanel(), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch });
            var terms = new DockPanel(); var termsHelp = Note("手工记录本文术语，例如 fluorescence → 荧光。备注不会约束模型或自动替换译文；请按原文语境核对。随中英对照报告导出，关闭前请导出。"); DockPanel.SetDock(termsHelp, Dock.Top); terms.Children.Add(termsHelp); terms.Children.Add(termNotes);
            texts.Items.Add(new TabItem { Header = "术语备注", Content = terms }); Grid.SetColumn(texts, 2); body.Children.Add(texts);
            Grid.SetRow(body, 3); root.Children.Add(body);
            var footer = new StackPanel(); footer.Children.Add(status); footer.Children.Add(new TextBlock { Text = LiteratureReport.ReviewWarning + " 扫描 OCR 暂不支持，多栏提取顺序需核对。", Foreground = Theme.WarningBrush, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            Grid.SetRow(footer, 4); root.Children.Add(footer); Content = root;
            componentLabel.Text = LiteratureComponent.Available(componentRoot) ? "离线组件已安装，正在检查运行环境…" : "离线组件未安装 · 支持配套 CPU 组件包，无账号、无在线回退";
            SetControls(); Loaded += async delegate { if (!qaMode && LiteratureComponent.Available(componentRoot)) await Probe(); };
            reviewDraft.TextChanged += delegate {
                if (refreshingReview || document == null) return;
                reviewState.SelectedIndex = 0; reviewPending = true; dirty = true; reviewLabel.Text = "核对稿已修改，原核对状态失效。请重新核对并保存。";
            };
            reviewState.SelectionChanged += delegate { if (!refreshingReview && document != null) { reviewPending = true; dirty = true; } };
            termNotes.TextChanged += delegate { if (!refreshingReview && document != null) { document.TermNotes = termNotes.Text; dirty = true; } };
            saveReview.Click += delegate { if (CommitReview(true)) status.Text = "本页核对已保存在当前会话。关闭前请导出中英对照，包含修订记录。"; };
            Closing += BeforeClose;
            Closed += delegate { if (cancellation != null) cancellation.Cancel(); CleanupWorkFolder(); };
        }
        private UIElement BuildReviewPanel()
        {
            var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var top = new StackPanel();
            top.Children.Add(Note("人工稿不覆盖机器译文；按页对照原 PDF 核对，不自动对齐段落。"));
            var actions = new WrapPanel();
            foreach (var state in new[] { "未核对", "已人工核对", "有疑问" }) reviewState.Items.Add(state);
            reviewState.SelectedIndex = 0; actions.Children.Add(reviewState); actions.Children.Add(saveReview); top.Children.Add(actions); top.Children.Add(reviewLabel);
            root.Children.Add(top); Grid.SetRow(reviewDraft, 1); root.Children.Add(reviewDraft);
            revisionHistory.Height = 140;
            var history = new Expander { Header = "修订记录（只读，可展开）", Content = revisionHistory, Margin = new Thickness(0, 6, 0, 0) }; Grid.SetRow(history, 2); root.Children.Add(history); return root;
        }
        private LiteratureWorker Worker() { return new LiteratureWorker(componentRoot, workerScript); }
        private IProgress<LiteratureProgress> Progress(string label)
        { return new Progress<LiteratureProgress>(value => status.Text = label + " " + value.Completed + "/" + value.Total + " 页（本机处理）"); }
        private async Task Probe()
        {
            busy = true; SetControls();
            try { await Worker().RunAsync(new[] { "probe" }, null, CancellationToken.None); verified = true; componentLabel.Text = "离线组件就绪 · 英文 → 中文 · CPU 本机处理 · 无网络请求"; }
            catch (Exception error) { verified = false; componentLabel.Text = "组件未通过运行检查"; status.Text = error.Message; }
            finally { busy = false; SetControls(); }
        }
        private async void InstallComponent()
        {
            if (busy) return;
            var picker = new OpenFileDialog { Title = "选择配套离线组件包", Filter = "FluoLab 离线组件 (*.zip)|*.zip" };
            if (picker.ShowDialog(this) != true) return;
            if (MessageBox.Show(this, "组件包含本地运行引擎和模型，安装后会在本机执行。仅选择随 FluoLab 正式交付、来源可信的配套 ZIP。不会上传文献。是否安装？", "安装离线组件", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            busy = true; SetControls(); status.Text = "正在校验并安装组件，请稍候…";
            try
            {
                var hashPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules", "Literature", "component.sha256");
                var expected = File.ReadAllText(hashPath).Trim();
                componentRoot = await Task.Run(delegate { return LiteratureComponent.Install(picker.FileName, expected, Path.GetDirectoryName(LiteratureComponent.InstalledRoot)); });
                await Probe(); status.Text = verified ? "组件安装完成，可导入本地 PDF。旧组件备份保留在组件目录，文献未上传。" : status.Text;
            }
            catch (Exception error) { status.Text = "组件安装失败：" + error.Message; }
            finally { busy = false; SetControls(); }
        }
        private async void ImportPdf()
        {
            if (busy || !verified) return;
            var picker = new OpenFileDialog { Title = "导入英文文献（原 PDF 不会被修改）", Filter = "PDF 文献 (*.pdf)|*.pdf" };
            var previousDirty = dirty;
            if (picker.ShowDialog(this) != true || !ConfirmDiscard()) return;
            dirty = previousDirty;
            await ImportPath(picker.FileName);
        }
        internal async Task ImportPath(string path)
        {
            busy = true; cancellation = new CancellationTokenSource(); SetControls();
            var candidate = Path.Combine(Path.GetTempPath(), "FluoLab-paper-" + Guid.NewGuid().ToString("N"));
            try
            {
                if (!File.Exists(path)) throw new InvalidOperationException("PDF 已移动或无法访问，请重新选择。");
                if (new FileInfo(path).Length > 200L * 1024 * 1024) throw new InvalidOperationException("PDF 超过 200 MB，请拆分后导入。");
                Directory.CreateDirectory(candidate);
                var copy = Path.Combine(candidate, "source.pdf"); var result = Path.Combine(candidate, "document.json");
                await Task.Run(delegate { File.Copy(path, copy); });
                await Worker().RunAsync(new[] { "inspect", copy, result }, Progress("提取原文"), cancellation.Token);
                var loaded = LiteratureWorker.ReadDocument(result); loaded.FileName = Path.GetFileName(path); LiteratureWorker.WriteDocument(result, loaded);
                var firstImage = Path.Combine(candidate, "page-0.png");
                await Worker().RunAsync(new[] { "render", copy, "0", firstImage }, null, cancellation.Token);
                var bitmap = ReadBitmap(firstImage);
                CleanupWorkFolder(); workFolder = candidate; candidate = null; sourcePath = copy; documentPath = result; document = loaded; pageIndex = 0; dirty = false;
                documentLabel.Text = loaded.FileName + " · " + loaded.Pages.Count + " 页 · 原文件未修改";
                pageImage.Source = bitmap; ShowTexts();
                status.Text = loaded.Pages.Any(page => !string.IsNullOrWhiteSpace(page.OriginalText)) ? "已提取原文。可按页或全文离线翻译，请核对多栏、表格与公式的阅读顺序。" : "此 PDF 没有可提取文本，可能是扫描件。可查看原页，但当前不支持 OCR，不能生成空白译文。";
            }
            catch (OperationCanceledException) { status.Text = "已取消导入，之前的文献未被替换。"; }
            catch (Exception error) { status.Text = "导入失败：" + error.Message + " 请检查 PDF 是否加密或损坏，或换一份文本型 PDF。"; }
            finally
            {
                if (candidate != null && Directory.Exists(candidate)) { try { Directory.Delete(candidate, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
                cancellation.Dispose(); cancellation = null; busy = false; SetControls();
            }
        }
        private async void Navigate(int direction)
        {
            if (busy || document == null) return;
            if (!CommitReview(false)) return;
            pageIndex = Math.Max(0, Math.Min(document.Pages.Count - 1, pageIndex + direction)); busy = true; SetControls();
            try { await RenderPage(); } catch (Exception error) { status.Text = "页面显示失败：" + error.Message; }
            finally { busy = false; SetControls(); }
        }
        private async Task RenderPage()
        {
            if (document == null) return;
            var image = Path.Combine(workFolder, "page-" + pageIndex + ".png");
            if (!File.Exists(image)) await Worker().RunAsync(new[] { "render", sourcePath, pageIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), image }, null, cancellation == null ? CancellationToken.None : cancellation.Token);
            pageImage.Source = ReadBitmap(image); ShowTexts();
        }
        private static BitmapImage ReadBitmap(string path)
        { var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(Path.GetFullPath(path)); bitmap.EndInit(); bitmap.Freeze(); return bitmap; }
        private void ShowTexts()
        {
            if (document == null) return;
            refreshingReview = true;
            var page = document.Pages[pageIndex]; original.Text = page.OriginalText ?? ""; chinese.Text = string.IsNullOrWhiteSpace(page.ChineseText) ? "此页尚未翻译。点击“翻译当前页”或“翻译全文”。无文本页面不支持 OCR。" : page.ChineseText;
            pageLabel.Text = "第 " + (pageIndex + 1) + "/" + document.Pages.Count + " 页 · " + page.Status + (page.NumericReview ? " · 数字标记与原文不同，必须核对" : "");
            reviewDraft.Text = page.Review == null ? page.ChineseText ?? "" : page.Review.Draft ?? "";
            reviewState.SelectedIndex = page.Review == null ? 0 : (int)page.Review.State;
            reviewLabel.Text = LiteratureReview.IsStale(page) ? "机器译文已变化：旧稿仅供对照，请重新核对。" : page.Review == null ? "尚无核对记录。修改后请保存本页核对。" : LiteratureReview.StateLabel(page.Review.State) + " · " + DateTimeOffset.Parse(page.Review.SavedAt, System.Globalization.CultureInfo.InvariantCulture).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            termNotes.Text = document.TermNotes ?? "";
            revisionHistory.Text = page.Review == null || page.Review.History == null ? "尚无修订记录。" : string.Join("\r\n\r\n", page.Review.History.Select(x => x.SavedAt + " · " + LiteratureReview.StateLabel(x.State) + "\r\n[当时机器译文]\r\n" + x.MachineText + "\r\n[人工稿]\r\n" + x.Draft));
            reviewPending = false; refreshingReview = false;
        }
        private bool CommitReview(bool explicitSave)
        {
            if (document == null || (!reviewPending && !explicitSave)) return true;
            try {
                LiteratureReview.Save(document.Pages[pageIndex], reviewDraft.Text, (LiteratureReviewState)reviewState.SelectedIndex, DateTimeOffset.Now);
                dirty = true; ShowTexts(); return true;
            }
            catch (Exception error) { status.Text = "核对未保存：" + error.Message; return false; }
        }
        private async void Translate(bool all) { await TranslatePages(all); }
        internal async Task TranslatePages(bool all)
        {
            if (busy || document == null) return;
            if (!CommitReview(false)) return;
            busy = true; cancellation = new CancellationTokenSource(); SetControls();
            var checkpointReady = false;
            try
            {
                await Task.Run(delegate { LiteratureWorker.WriteDocument(documentPath, document); }); checkpointReady = true;
                await Worker().RunAsync(new[] { "translate", sourcePath, documentPath, all ? "all" : pageIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), componentRoot }, Progress("离线翻译"), cancellation.Token);
                document = LiteratureWorker.ReadDocument(documentPath); dirty = true; ShowTexts(); status.Text = "机器翻译完成。请对照原文逐页核对，尤其是术语、否定关系、数字和单位。";
            }
            catch (OperationCanceledException)
            {
                document = LiteratureWorker.ReadDocument(documentPath); dirty = document.Pages.Any(x => !string.IsNullOrWhiteSpace(x.ChineseText)); ShowTexts(); status.Text = "已取消。已完成页面保留，未完成页面明确标注未翻译，可导出部分结果。";
            }
            catch (Exception error)
            {
                try { if (checkpointReady) document = LiteratureWorker.ReadDocument(documentPath); dirty = true; ShowTexts(); }
                catch (Exception) { dirty = true; }
                status.Text = "翻译失败：" + error.Message + " 已完成的页面仍可导出，未完成内容不做补写。";
            }
            finally { cancellation.Dispose(); cancellation = null; busy = false; SetControls(); }
        }
        private async void Export()
        {
            if (busy || document == null) return;
            if (!CommitReview(false)) return;
            var picker = new SaveFileDialog { Title = "导出逐页中英对照", Filter = "HTML 中英对照 (*.html)|*.html|UTF-8 文本 (*.txt)|*.txt", DefaultExt = ".html", AddExtension = true, OverwritePrompt = true,
                FileName = Path.GetFileNameWithoutExtension(document.FileName) + "_中英对照.html" };
            if (picker.ShowDialog(this) != true) return;
            busy = true; SetControls();
            try
            {
                var snapshot = document; var html = picker.FilterIndex == 1;
                await Task.Run(delegate { AtomicOutput.Write(picker.FileName, true, temp => File.WriteAllText(temp, html ? LiteratureReport.Html(snapshot) : LiteratureReport.Text(snapshot), new UTF8Encoding(true))); });
                dirty = false; status.Text = "已导出原文、原始机器译文、人工稿、核对状态、全部修订和术语备注，按页可追溯。不含源文件绝对路径。";
            }
            catch (Exception error) { status.Text = "导出失败：" + error.Message + " 请关闭占用文件或选择其他位置重试。"; }
            finally { busy = false; SetControls(); }
        }
        private void SetControls()
        {
            installButton.IsEnabled = !busy; importButton.IsEnabled = !busy && verified;
            previousButton.IsEnabled = !busy && document != null && pageIndex > 0; nextButton.IsEnabled = !busy && document != null && pageIndex + 1 < document.Pages.Count;
            pageButton.IsEnabled = !busy && document != null && !string.IsNullOrWhiteSpace(document.Pages[pageIndex].OriginalText);
            allButton.IsEnabled = !busy && document != null && document.Pages.Any(x => !string.IsNullOrWhiteSpace(x.OriginalText)); cancelButton.IsEnabled = busy && cancellation != null; exportButton.IsEnabled = !busy && document != null;
            reviewDraft.IsEnabled = !busy && document != null; reviewState.IsEnabled = !busy && document != null; saveReview.IsEnabled = !busy && document != null; termNotes.IsEnabled = !busy && document != null;
        }
        private bool ConfirmDiscard()
        {
            if (!dirty) return true;
            if (MessageBox.Show(this, "有未导出的译文、人工核对或术语备注。请先点击“导出中英对照”保存；本版不恢复阅读工程。仍要放弃并继续吗？", "未导出阅读记录", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return false;
            dirty = false; return true;
        }
        internal bool CanClose()
        {
            if (busy) { status.Text = "任务进行中，请先取消或等待完成，再关闭窗口。"; return false; }
            return qaMode || ConfirmDiscard();
        }
        private void BeforeClose(object sender, CancelEventArgs args)
        {
            if (!CanClose()) args.Cancel = true;
        }
        private void CleanupWorkFolder()
        {
            if (workFolder == null) return;
            var folder = workFolder; workFolder = null;
            try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        internal void UseComponentForQa(string root) { qaMode = true; componentRoot = root; verified = true; componentLabel.Text = "模拟文献验证 · 离线组件已就绪"; SetControls(); }
        internal void LoadFixedDemoForQa(string imagePath)
        {
            qaMode = true; verified = true;
            document = new LiteratureDocument { FileName = "模拟英文文章.pdf", Pages = new List<LiteraturePage> { new LiteraturePage { Number = 1, OriginalText = "Simulated text for interface verification.\nFluorescence intensity was measured at 600 nm.", ChineseText = "用于界面检查的固定模拟译文。\n在 600 nm 下测量荧光强度。\n此状态尚未调用推理引擎。", Status = "固定模拟界面" } } };
            documentLabel.Text = "模拟英文文章.pdf · 固定数据界面检查，不是实验文献"; componentLabel.Text = "模拟数据 · 仅验证布局，真实运行须安装配套离线组件";
            document.TermNotes = "【模拟术语备注，非术语规范】\nfluorescence → 荧光\nprobe → 探针（需按上下文核对）";
            LiteratureReview.Save(document.Pages[0], "【模拟人工稿】\n用于界面检查的固定模拟内容。\n在 600 nm 下测量荧光强度。\n仍需核对原文语义。", LiteratureReviewState.Questionable, DateTimeOffset.Now);
            texts.SelectedIndex = 2;
            if (File.Exists(imagePath)) { var bitmap = new BitmapImage(new Uri(Path.GetFullPath(imagePath))); pageImage.Source = bitmap; }
            ShowTexts(); status.Text = "固定模拟页面。真实翻译质量须人工核对。"; SetControls();
        }
        private static TextBox ReadingBox() { return new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontSize = 15 }; }
        private static TextBlock Note(string text) { return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Theme.SecondaryTextBrush, Margin = new Thickness(0, 6, 0, 6) }; }
        private static Button Button(string text, Action action) { var button = new Button { Content = text, Margin = new Thickness(0, 0, 6, 6) }; button.Click += delegate { action(); }; return button; }
    }
}
