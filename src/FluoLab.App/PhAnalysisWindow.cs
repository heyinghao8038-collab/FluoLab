using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FluoLab.Core;
using FluoLab.Export;
using Microsoft.Win32;

namespace FluoLab.App
{
    internal sealed partial class PhAnalysisWindow : Window
    {
        private readonly PhAnalysisSession session;
        private readonly bool synthetic;
        private readonly List<MetadataRow> rows;
        private readonly TabControl steps = new TabControl();
        private readonly TextBlock status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        private DataGrid groupingGrid;
        private CheckBox affirmation;
        private TextBox wavelength;
        private TextBox unit;
        private TextBlock matchText;
        private TextBox missingText;
        private Grid resultPanel;
        private StackPanel resultActions;
        private Button analyzeButton;
        private PhResponseChart chart;
        private bool busy;
        private bool initializing = true;

        public PhAnalysisWindow(SpectrumBatch batch, bool syntheticDemo)
        {
            synthetic = syntheticDemo;
            session = new PhAnalysisSession(batch);
            rows = session.Samples.Select(x => new MetadataRow(x, GroupingEdited)).ToList();
            Title = synthetic ? "FluoLab pH 响应分析（模拟数据）" : "FluoLab pH 响应分析";
            Width = 1260; Height = 840; MinWidth = 760; MinHeight = 440;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sourceBatch = batch;
            var header = new StackPanel();
            header.Children.Add(new TextBlock { Text = (synthetic ? "模拟数据演示 · " : "") + "pH 响应分析    " + batch.SuccessCount + " 份有效光谱，" + batch.SkippedCount + " 份解析失败未纳入",
                FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            var projects = new WrapPanel();
            projects.Children.Add(ActionButton("保存工作项目", delegate { SaveProject(); }));
            projects.Children.Add(ActionButton("打开工作项目", delegate { OpenProject(); }));
            projectLabel = Note("工作进度尚未保存"); projects.Children.Add(projectLabel);
            header.Children.Add(projects); root.Children.Add(header);
            steps.Items.Add(new TabItem { Header = "1  样品分组", Content = BuildGrouping() });
            steps.Items.Add(new TabItem { Header = "2  波长选择", Content = BuildWavelength() });
            steps.Items.Add(new TabItem { Header = "3  分析结果", Content = BuildResults() });
            Grid.SetRow(steps, 1); root.Children.Add(steps);
            status.Margin = new Thickness(0, 12, 0, 0);
            Grid.SetRow(status, 2); root.Children.Add(status);
            Content = root;
            initializing = false;
            SetStatus("文件名仅提供 pH 和编号建议。请填写系列、检查所有行，并明确确认分组。", false);
            RefreshValidation();
            Closing += OnDraftClosing;
        }

        private UIElement BuildGrouping()
        {
            var root = new DockPanel { Margin = new Thickness(10) };
            var top = new StackPanel();
            top.Children.Add(BuildGroupingTools());
            var bulk = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
            var seriesInput = new TextBox { Width = 180, ToolTip = "批量系列名称", Margin = new Thickness(0, 0, 8, 6) };
            bulk.Children.Add(seriesInput);
            bulk.Children.Add(ActionButton("设置所选系列", delegate { ApplySelected(x => x.Series = seriesInput.Text); }));
            var typeInput = new ComboBox { Width = 140, ItemsSource = MetadataRow.RepeatLabels, SelectedIndex = 0, Margin = new Thickness(8, 0, 8, 6) };
            bulk.Children.Add(typeInput);
            bulk.Children.Add(ActionButton("设置所选重复类型", delegate { ApplySelected(x => x.RepeatLabel = (string)typeInput.SelectedItem); }));
            var batchPanel = new StackPanel();
            batchPanel.Children.Add(Note("系列表示除 pH 外条件一致的样品，不自动推断。选中多行后可批量设置。"));
            var transfer = new WrapPanel();
            transfer.Children.Add(ActionButton("复制分组模板", CopyGrouping));
            transfer.Children.Add(ActionButton("粘贴分组表", PasteGrouping));
            batchPanel.Children.Add(transfer);
            batchPanel.Children.Add(bulk);
            var participation = new WrapPanel();
            var reason = new TextBox { Width = 270, ToolTip = "排除原因（必填）", Margin = new Thickness(0, 0, 8, 6) };
            participation.Children.Add(reason);
            participation.Children.Add(ActionButton("排除所选（须填原因）", delegate
            {
                if (string.IsNullOrWhiteSpace(reason.Text)) { SetStatus("请先填写排除原因。", true); return; }
                ApplySelected(x => { x.Reason = reason.Text; x.Included = false; });
            }));
            participation.Children.Add(ActionButton("纳入所选", delegate { ApplySelected(x => { x.Included = true; x.Reason = ""; }); }));
            batchPanel.Children.Add(participation);
            top.Children.Add(new Expander { Header = "批量设置 / Excel、WPS 粘贴（Ctrl/Shift 多选）", Content = new ScrollViewer { Content = batchPanel, MaxHeight = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, IsExpanded = false, Margin = new Thickness(0, 2, 0, 4) });
            DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
            var bottom = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            affirmation = new CheckBox { Content = new TextBlock { Text = "我已核对：同系列除 pH 外条件一致，重复为单层设计，技术测量未作为独立实验。", TextWrapping = TextWrapping.Wrap }, Margin = new Thickness(0, 0, 0, 4) };
            affirmation.Unchecked += delegate { if (!initializing) { session.RevokeGrouping(); InvalidateResult(); RefreshValidation(); SetStatus("分组确认已撤销，请重新核对后确认。", true); } };
            bottom.Children.Add(affirmation);
            bottom.Children.Add(ActionButton("确认分组，进入波长选择", ConfirmGrouping));
            DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
            groupingGrid = NewGrid();
            groupingGrid.FrozenColumnCount = 2;
            groupingGrid.IsReadOnly = false;
            groupingGrid.SelectionMode = DataGridSelectionMode.Extended;
            groupingGrid.SelectionUnit = DataGridSelectionUnit.FullRow;
            AddTextColumn(groupingGrid, "源文件名", "FileName", 300, true);
            AddTextColumn(groupingGrid, "校验状态（双击定位）", "Validation", 190, true);
            AddTextColumn(groupingGrid, "分析系列", "Series", 170, false);
            AddTextColumn(groupingGrid, "pH", "PhText", 85, false);
            AddTextColumn(groupingGrid, "重复编号", "RepeatId", 85, false);
            groupingGrid.Columns.Add(new DataGridComboBoxColumn { Header = "重复类型", Width = 135, ItemsSource = MetadataRow.RepeatLabels,
                SortMemberPath = "RepeatLabel",
                SelectedItemBinding = new Binding("RepeatLabel") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged } });
            groupingGrid.Columns.Add(new DataGridCheckBoxColumn { Header = "参与分析", Width = 80,
                SortMemberPath = "Included",
                Binding = new Binding("Included") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged } });
            AddTextColumn(groupingGrid, "排除原因", "Reason", 230, false);
            groupingGrid.ItemsSource = rows;
            groupingGrid.MouseDoubleClick += delegate { if (groupingGrid.CurrentColumn != null && groupingGrid.CurrentColumn.SortMemberPath == "Validation") FocusIssue(groupingGrid.SelectedItem as MetadataRow); };
            root.Children.Add(groupingGrid);
            // A caption strip must not squeeze the editable grid to zero rows at high DPI.
            var viewport = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            viewport.SizeChanged += delegate { root.Height = Math.Max(420, viewport.ActualHeight - 20); };
            return viewport;
        }

        private UIElement BuildWavelength()
        {
            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(Note("只匹配原始数据中数值相等的波长。不取最近点、不插值。参数改变会立即使旧结果失效。"));
            panel.Children.Add(new TextBlock { Text = "分析波长 (nm)", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 20, 0, 6) });
            wavelength = new TextBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, ToolTip = "例如 600 或 600.0，按原始数值精确匹配" };
            wavelength.TextChanged += delegate { ParametersEdited(); };
            panel.Children.Add(wavelength);
            panel.Children.Add(new TextBlock { Text = "已确认的荧光强度单位（可留空）", Margin = new Thickness(0, 16, 0, 6) });
            unit = new TextBox { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, MaxLength = 80 };
            unit.TextChanged += delegate { ParametersEdited(); };
            panel.Children.Add(unit);
            panel.Children.Add(ActionButton("检查精确匹配数量", UpdateMatchPreview));
            matchText = Note("请先确认分组并填写目标波长。"); panel.Children.Add(matchText);
            missingText = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Height = 150, Margin = new Thickness(0, 12, 0, 12), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            panel.Children.Add(missingText);
            analyzeButton = ActionButton("生成 pH 响应结果", Analyze);
            panel.Children.Add(analyzeButton);
            return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private UIElement BuildResults()
        {
            var dock = new DockPanel { Margin = new Thickness(16) };
            resultActions = new StackPanel { IsEnabled = false };
            var exports = new WrapPanel();
            exports.Children.Add(ActionButton("导出完整分析包", delegate { ExportBundle(); }));
            exports.Children.Add(ActionButton("分析 Excel", delegate { Export("xlsx"); }));
            exports.Children.Add(ActionButton("PNG", delegate { Export("png"); }));
            exports.Children.Add(ActionButton("SVG", delegate { Export("svg"); }));
            includeRaw = new CheckBox { Content = "分析包包含原始 ASC", Margin = new Thickness(6, 12, 0, 6), ToolTip = "默认不包含。包含前将核对源文件内容指纹。" };
            if (synthetic) { includeRaw.IsEnabled = false; includeRaw.ToolTip = "内存模拟演示不附带源文件；导入实际模拟 ASC 文件夹后可以使用此选项。"; }
            exports.Children.Add(includeRaw); resultActions.Children.Add(exports);
            DockPanel.SetDock(resultActions, Dock.Top); dock.Children.Add(resultActions);
            resultPanel = new Grid();
            resultPanel.Children.Add(Note("结果尚未生成。请按“样品分组 → 波长选择 → 分析结果”完成分析。"));
            dock.Children.Add(resultPanel);
            return dock;
        }

        private void ConfirmGrouping()
        {
            CommitEdits();
            if (affirmation.IsChecked != true) { SetStatus("请先勾选分组和单层重复设计的确认声明。", true); return; }
            var errors = session.ConfirmGrouping(); RefreshValidation();
            if (errors.Length > 0) { ShowList("分组尚未完成", errors); SetStatus("请按具体文件提示修正分组。", true); return; }
            SetStatus("分组已确认。n 将标为测量次数；本轮仅做描述统计。", false);
            steps.SelectedIndex = 1; UpdateMatchPreview();
        }

        private void GroupingEdited()
        {
            if (initializing || batchingEdits) return;
            session.RevokeGrouping(); affirmation.IsChecked = false;
            InvalidateResult(); RefreshValidation();
            MarkDirty();
            SetStatus("分组已改变，旧结果已失效。请重新检查并确认。", true);
        }

        private void ParametersEdited()
        {
            if (initializing) return;
            session.Parameters.WavelengthText = wavelength.Text;
            session.Parameters.IntensityUnit = unit == null ? "" : unit.Text;
            session.Invalidate(); InvalidateResult(); UpdateMatchPreview();
            MarkDirty();
        }

        private void InvalidateResult()
        {
            chart = null; resultActions.IsEnabled = false;
            resultPanel.Children.Clear();
            resultPanel.Children.Add(Note("当前没有有效结果。分组、参与状态、单位或波长修改后必须重新分析。"));
        }

        private void RefreshValidation()
        {
            foreach (var row in rows)
            {
                double number;
                var errors = new List<string>();
                if (!string.IsNullOrWhiteSpace(row.PhText) && !AscParser.TryParseFinite(row.PhText, out number)) errors.Add("pH 必须是有限数值");
                if (!row.Included) { if (string.IsNullOrWhiteSpace(row.Reason)) errors.Add("请填写排除原因"); }
                else
                {
                    if (string.IsNullOrWhiteSpace(row.Series)) errors.Add("缺少系列");
                    if (!AscParser.TryParseFinite(row.PhText, out number)) errors.Add("pH 无效/缺失");
                    if (string.IsNullOrWhiteSpace(row.RepeatId)) errors.Add("缺少重复编号");
                }
                AddDesignIssues(row, errors);
                row.HasIssue = errors.Count > 0;
                row.Validation = errors.Count > 0 ? string.Join("；", errors) : session.GroupingConfirmed ? "已确认" : "待明确确认";
            }
            RefreshGroupingTools();
        }

        private void UpdateMatchPreview()
        {
            if (matchText == null || missingText == null) return;
            try
            {
                var preview = session.Preview();
                matchText.Text = string.Format("精确匹配：{0} / {1} 份参与文件；缺失 {2} 份。", preview.MatchedCount, preview.ParticipatingCount, preview.MissingFiles.Length);
                missingText.Text = preview.MissingFiles.Length == 0 ? "全部参与文件具有该精确波长。" : "以下文件没有该波长：\n" + string.Join("\n", preview.MissingFiles);
            }
            catch (InvalidOperationException exception) { matchText.Text = exception.Message; missingText.Text = ""; }
        }

        private async void Analyze()
        {
            if (busy) return;
            CommitEdits();
            try
            {
                var preview = session.Preview();
                if (preview.MatchedCount == 0) throw new InvalidOperationException("所有参与文件均不匹配。请修改波长或返回分组调整参与状态。");
                if (preview.MissingFiles.Length > 0)
                {
                    if (!ConfirmMissing(preview.MissingFiles)) { SetStatus("未确认排除缺失文件，分析未执行。", true); return; }
                    session.Parameters.AllowMissing = true;
                }
                busy = true; steps.IsEnabled = false; SetStatus("正在提取原始点并计算描述统计…", false);
                var watch = Stopwatch.StartNew();
                var result = await Task.Run(delegate { return session.Calculate(CancellationToken.None); });
                watch.Stop(); ShowResult(result);
                steps.SelectedIndex = 2;
                SetStatus(string.Format("分析完成：{0} 个 pH 组，纳入 {1} 次测量。分析耗时 {2:0.000} 秒（不含导入和人工确认）。",
                    result.Groups.Count, result.Samples.Count(x => x.Status == ExtractionStatus.Success), watch.Elapsed.TotalSeconds), false);
            }
            catch (Exception exception) { SetStatus(exception.Message, true); }
            finally { busy = false; steps.IsEnabled = true; }
        }

        private void ShowResult(PhAnalysisResult result)
        {
            resultPanel.Children.Clear();
            var views = new TabControl();
            views.Items.Add(new TabItem { Header = "响应图", Content = BuildChartView(result) });
            var groups = NewGrid(); groups.Height = 230;
            AddTextColumn(groups, "分析系列", "Series", 220, true);
            AddTextColumn(groups, "pH", "Ph", 90, true);
            AddTextColumn(groups, "重复类型", "RepeatLabel", 140, true);
            AddTextColumn(groups, "测量次数 n", "N", 100, true);
            AddTextColumn(groups, "均值", "Mean", 180, true);
            AddTextColumn(groups, "样本 SD", "Sd", 180, true);
            groups.ItemsSource = result.Groups.Select(x => new { x.Series, x.Ph, RepeatLabel = PhAnalysisService.RepeatLabel(x.RepeatType), x.N,
                Mean = x.Mean.ToString("G10", CultureInfo.CurrentCulture), Sd = x.Sd.HasValue ? x.Sd.Value.ToString("G10", CultureInfo.CurrentCulture) : "不可计算（n=1）" });
            groups.Height = double.NaN;
            views.Items.Add(new TabItem { Header = "分组统计（n 为测量次数）", Content = groups });
            var details = NewGrid(); details.Height = 230;
            AddTextColumn(details, "源文件名", "FileName", 340, true);
            AddTextColumn(details, "分析系列", "Series", 140, true);
            AddTextColumn(details, "pH", "Ph", 75, true);
            AddTextColumn(details, "原始荧光强度", "Intensity", 150, true);
            AddTextColumn(details, "状态", "Status", 130, true);
            AddTextColumn(details, "原因", "Reason", 380, true);
            details.ItemsSource = result.Samples.Select(x => new { x.FileName, x.Series, x.Ph, x.Intensity, Status = PhAnalysisService.StatusLabel(x.Status), x.Reason });
            details.Height = double.NaN;
            details.MouseDoubleClick += delegate { if (details.SelectedItem != null) OpenTrace((string)details.SelectedItem.GetType().GetProperty("FileName").GetValue(details.SelectedItem, null)); };
            views.Items.Add(new TabItem { Header = "样品明细（双击溯源）", Content = details });
            resultPanel.Children.Add(views); resultActions.IsEnabled = true;
        }

        private async void Export(string format)
        {
            if (busy) return;
            try
            {
                var result = session.RequireCurrentResult();
                var dialog = new SaveFileDialog { Title = "导出 pH 分析" + (synthetic ? "（模拟数据）" : ""), DefaultExt = "." + format,
                    Filter = format.ToUpperInvariant() + " 文件|*." + format, AddExtension = true, OverwritePrompt = true,
                    FileName = (synthetic ? "模拟数据_" : "") + "pH响应_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "." + format };
                if (dialog.ShowDialog(this) != true) { SetStatus("已取消导出，没有创建文件。", false); return; }
                var path = dialog.FileName;
                busy = true; steps.IsEnabled = false; SetStatus("正在导出，请稍候…", false);
                if (format == "xlsx") await Task.Run(delegate { new WorkbookExporter().ExportAnalysis(result, path, true); });
                else
                {
                    // WPF rendering is STA-bound, so use a dedicated STA worker rather than blocking the window.
                    await ExportChartOnWorker(result, synthetic, path, format);
                }
                SetStatus("导出完成：" + Path.GetFileName(path), false);
            }
            catch (Exception exception)
            { SetStatus("导出失败：" + exception.Message + " 请关闭占用文件或选择可写位置重试。", true); }
            finally { busy = false; steps.IsEnabled = true; }
        }

        internal static Task ExportChartOnWorker(PhAnalysisResult result, bool synthetic, string path, string format)
        {
            var completion = new TaskCompletionSource<bool>();
            var thread = new Thread(delegate()
            {
                try
                {
                    var outputChart = new PhResponseChart(result, synthetic);
                    if (format == "png") outputChart.ExportPng(path, true); else outputChart.ExportSvg(path, true);
                    completion.SetResult(true);
                }
                catch (Exception exception) { completion.SetException(exception); }
            });
            thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
            return completion.Task;
        }

        private bool ConfirmMissing(string[] files)
        {
            var dialog = new Window { Owner = this, Title = "确认缺少波长文件的处理", Width = 680, Height = 480, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var dock = new DockPanel { Margin = new Thickness(20) };
            var explanation = Note("以下 " + files.Length + " 份文件缺少目标波长，强度将保持缺失。确认后只将匹配成功文件纳入统计，决定会写入分析记录。");
            DockPanel.SetDock(explanation, Dock.Top); dock.Children.Add(explanation);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(ActionButton("取消，返回检查", delegate { dialog.DialogResult = false; }));
            buttons.Children.Add(ActionButton("确认仅使用匹配成功文件", delegate { dialog.DialogResult = true; }));
            DockPanel.SetDock(buttons, Dock.Bottom); dock.Children.Add(buttons);
            dock.Children.Add(new TextBox { Text = string.Join("\n", files), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 12, 0, 12) });
            dialog.Content = dock; return dialog.ShowDialog() == true;
        }
        private void ShowList(string title, string[] messages)
        {
            var window = new Window { Owner = this, Title = title, Width = 760, Height = 460, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new TextBox { IsReadOnly = true, Text = string.Join("\n\n", messages), TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20) } };
            window.ShowDialog();
        }
        private void ApplySelected(Action<MetadataRow> edit)
        {
            CommitEdits();
            var selected = groupingGrid.SelectedItems.Cast<MetadataRow>().ToArray();
            if (selected.Length == 0) { SetStatus("请先选中一行或多行（Ctrl/Shift 可多选）。", true); return; }
            foreach (var row in selected) edit(row);
        }
        private void CommitEdits() { groupingGrid.CommitEdit(DataGridEditingUnit.Cell, true); groupingGrid.CommitEdit(DataGridEditingUnit.Row, true); }
        private void SetStatus(string text, bool warning) { status.Text = text; status.Foreground = warning ? Theme.WarningBrush : Theme.SecondaryTextBrush; }
        private static TextBlock Note(string text) { return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Theme.SecondaryTextBrush, FontSize = 13, Margin = new Thickness(0, 8, 0, 8) }; }
        private static Button ActionButton(string text, Action action)
        {
            var button = new Button { Content = text, Margin = new Thickness(0, 4, 8, 6), HorizontalAlignment = HorizontalAlignment.Left };
            button.Click += delegate { action(); }; return button;
        }
        private static DataGrid NewGrid()
        {
            return new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, IsReadOnly = true,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal, HorizontalGridLinesBrush = Theme.SecondarySurfaceBrush,
                AlternatingRowBackground = Theme.BackgroundBrush, RowHeaderWidth = 0, MinRowHeight = 38, HeadersVisibility = DataGridHeadersVisibility.Column,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
        private static void AddTextColumn(DataGrid grid, string header, string path, double width, bool readOnly)
        {
            var textStyle = new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            textStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(6)));
            grid.Columns.Add(new DataGridTextColumn { Header = header, SortMemberPath = path, Width = width, IsReadOnly = readOnly, ElementStyle = textStyle,
                Binding = new Binding(path) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, Mode = readOnly ? BindingMode.OneWay : BindingMode.TwoWay } });
        }

        internal void LoadSyntheticResultForQa(int step)
        {
            var demo = PhDemo.CreateConfirmedSession(PhDemo.CreateBatch());
            initializing = true;
            for (var i = 0; i < session.Samples.Count; i++)
            {
                session.Samples[i].Series = demo.Samples[i].Series;
                session.Samples[i].Included = demo.Samples[i].Included;
                session.Samples[i].ExclusionReason = demo.Samples[i].ExclusionReason;
            }
            session.ConfirmGrouping(); session.Parameters.WavelengthText = "600";
            session.Parameters.AllowMissing = true; wavelength.Text = "600";
            affirmation.IsChecked = true; initializing = false;
            groupingGrid.Items.Refresh(); RefreshValidation(); UpdateMatchPreview();
            ShowResult(session.Calculate(CancellationToken.None)); steps.SelectedIndex = step;
            SetStatus("模拟数据：已演示人工确认分组、缺失波长排除、n=1 和多系列描述统计。", false);
        }

        internal void LoadSyntheticStateForQa(string state)
        {
            if (state == "incomplete") return;
            if (state == "empty") { SetStatus("没有可分析数据。请返回主窗口导入有效 ASC 文件。", true); return; }
            LoadSyntheticResultForQa(state == "grouping" ? 0 : state == "partial" || state == "all-missing" ? 1 : 2);
            if (state == "long")
            {
                for (var i = 0; i < session.Samples.Count; i++)
                    session.Samples[i].Series = i < 10 ? "模拟系列 A：长名称，10 μM 探针 & 条件 <A>；" + new string('A', 150) : "模拟系列 B：不同实验条件，必须独立分组；" + new string('B', 150);
                session.ConfirmGrouping(); ShowResult(session.Calculate(CancellationToken.None));
            }
            if (state == "all-missing") wavelength.Text = "601";
            if (state == "stale") { wavelength.Text = "610"; SetStatus("波长已改变，旧结果已失效，请重新分析。", true); }
            if (state == "exporting") { steps.IsEnabled = false; SetStatus("正在导出，请稍候…", false); }
            if (state == "failure") SetStatus("导出失败：目标文件被占用。请关闭 Excel/WPS 中的文件，或选择其他保存位置重试。", true);
        }

        internal void PrefillSyntheticDemo()
        {
            var demo = PhDemo.CreateConfirmedSession(PhDemo.CreateBatch());
            initializing = true;
            for (var i = 0; i < session.Samples.Count; i++)
            {
                session.Samples[i].Series = demo.Samples[i].Series;
                session.Samples[i].Included = demo.Samples[i].Included;
                session.Samples[i].ExclusionReason = demo.Samples[i].ExclusionReason;
            }
            initializing = false;
            groupingGrid.Items.Refresh(); RefreshValidation();
            SetStatus("模拟条件已预填（来自固定模拟设计），请核对并勾选确认。分析波长建议输入 600 nm，可演示部分缺失确认。", false);
        }

        private sealed class MetadataRow : INotifyPropertyChanged
        {
            public static readonly string[] RepeatLabels = { "未确认", "技术重复", "独立实验重复" };
            private readonly PhSampleMetadata metadata;
            private readonly Action changed;
            private string validation;
            public MetadataRow(PhSampleMetadata metadata, Action changed) { this.metadata = metadata; this.changed = changed; }
            public PhSampleMetadata Metadata { get { return metadata; } }
            public string FileName { get { return Path.GetFileName(metadata.SourcePath); } }
            public string Series { get { return metadata.Series; } set { metadata.Series = value; Change("Series"); } }
            public string PhText { get { return metadata.PhText; } set { metadata.PhText = value; Change("PhText"); } }
            public string RepeatId { get { return metadata.RepeatId; } set { metadata.RepeatId = value; Change("RepeatId"); } }
            public string RepeatLabel { get { return PhAnalysisService.RepeatLabel(metadata.RepeatType); } set { metadata.RepeatType = value == "技术重复" ? RepeatType.Technical : value == "独立实验重复" ? RepeatType.Independent : RepeatType.Unconfirmed; Change("RepeatLabel"); } }
            public bool Included { get { return metadata.Included; } set { metadata.Included = value; Change("Included"); } }
            public string Reason { get { return metadata.ExclusionReason; } set { metadata.ExclusionReason = value; Change("Reason"); } }
            public string Validation { get { return validation; } set { validation = value; Notify("Validation"); } }
            public bool HasIssue { get; set; }
            public event PropertyChangedEventHandler PropertyChanged;
            private void Change(string name) { Notify(name); changed(); }
            private void Notify(string name) { var handler = PropertyChanged; if (handler != null) handler(this, new PropertyChangedEventArgs(name)); }
        }
    }
}
