using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using FluoLab.Core;
using FluoLab.Export;
using Microsoft.Win32;

namespace FluoLab.App
{
    internal sealed partial class PhAnalysisWindow
    {
        private readonly SpectrumBatch sourceBatch;
        private TextBox groupingSearch;
        private ComboBox groupingFilter;
        private TextBlock completionText;
        private TextBlock projectLabel;
        private CheckBox includeRaw;
        private string projectPath;
        private bool dirty;
        private bool closePending;
        private bool batchingEdits;
        internal Func<MessageBoxResult> CloseDecision { get; set; }

        private UIElement BuildGroupingTools()
        {
            var tools = new StackPanel(); var bar = new WrapPanel();
            bar.Children.Add(new TextBlock { Text = "文件名", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            groupingSearch = new TextBox { Width = 180, ToolTip = "按源文件名搜索", Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 8, 4) };
            groupingSearch.TextChanged += delegate { ApplyGroupingFilter(); }; bar.Children.Add(groupingSearch);
            groupingFilter = new ComboBox { Width = 150, ItemsSource = new[] { "全部文件", "待填写", "有问题", "待确认" }, SelectedIndex = 0, Margin = new Thickness(0, 0, 8, 4), Padding = new Thickness(6) };
            groupingFilter.SelectionChanged += delegate { ApplyGroupingFilter(); }; bar.Children.Add(groupingFilter);
            bar.Children.Add(ActionButton("定位下一问题", FocusNextIssue));
            tools.Children.Add(bar); completionText = Note(""); tools.Children.Add(completionText); return tools;
        }
        private void RefreshGroupingTools()
        {
            if (completionText == null) return;
            completionText.Text = string.Format("字段完整 {0}/{1} 份 · 有问题 {2} 份 · {3}。筛选只影响显示，不改变参与状态。",
                rows.Count(x => !x.HasIssue), rows.Count, rows.Count(x => x.HasIssue), session.GroupingConfirmed ? "分组已确认" : "尚需人工确认");
            ApplyGroupingFilter();
        }
        private void ApplyGroupingFilter()
        {
            if (groupingGrid == null || groupingSearch == null || groupingFilter == null) return;
            // Defer collection refresh while a cell is actively committing its binding.
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (!groupingGrid.CommitEdit(DataGridEditingUnit.Row, true)) return;
                var query = groupingSearch.Text.Trim(); var filter = groupingFilter.SelectedIndex;
                CollectionViewSource.GetDefaultView(rows).Filter = value =>
                {
                    var row = (MetadataRow)value;
                    if (query.Length > 0 && row.FileName.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) return false;
                    if (filter == 1) return row.Included && (string.IsNullOrWhiteSpace(row.Series) || string.IsNullOrWhiteSpace(row.PhText) || string.IsNullOrWhiteSpace(row.RepeatId)) || !row.Included && string.IsNullOrWhiteSpace(row.Reason);
                    if (filter == 2) return row.HasIssue;
                    return filter != 3 || !session.GroupingConfirmed;
                };
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        private void AddDesignIssues(MetadataRow row, List<string> errors)
        {
            if (!row.Included) return;
            var sameSeries = rows.Where(x => x.Included && (x.Series ?? "").Trim() == (row.Series ?? "").Trim()).ToArray();
            if (sameSeries.Select(x => x.RepeatLabel).Distinct().Count() > 1) errors.Add("同系列重复类型混用");
            var ph = PhAnalysisService.ParseNullable(row.PhText);
            if (ph.HasValue && sameSeries.Count(x => PhAnalysisService.ParseNullable(x.PhText) == ph && (x.RepeatId ?? "").Trim() == (row.RepeatId ?? "").Trim()) > 1) errors.Add("重复编号冲突");
            if (System.Text.RegularExpressions.Regex.IsMatch(row.RepeatId ?? "", @"[/\\:;]|\d[._-]\d")) errors.Add("不支持嵌套重复编号");
        }
        private void FocusNextIssue()
        {
            CommitEdits(); RefreshValidation();
            if (rows.Count == 0) { SetStatus("没有有效光谱，请先导入 ASC。", true); return; }
            var start = rows.IndexOf(groupingGrid.SelectedItem as MetadataRow);
            var row = Enumerable.Range(1, rows.Count).Select(i => rows[(Math.Max(-1, start) + i) % rows.Count]).FirstOrDefault(x => x.HasIssue);
            if (row == null) { SetStatus("字段校验通过，请核对实验含义并确认分组。", false); return; }
            FocusIssue(row);
        }
        private void FocusIssue(MetadataRow row)
        {
            if (row == null) return;
            groupingSearch.Text = ""; groupingFilter.SelectedIndex = 0;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                var field = !row.Included ? "Reason" : string.IsNullOrWhiteSpace(row.Series) ? "Series" :
                    !PhAnalysisService.ParseNullable(row.PhText).HasValue ? "PhText" : row.Validation.Contains("类型") ? "RepeatLabel" : "RepeatId";
                var column = groupingGrid.Columns.FirstOrDefault(x => x.SortMemberPath == field);
                if (column == null) column = groupingGrid.Columns[2];
                groupingGrid.SelectedItem = row; groupingGrid.CurrentCell = new DataGridCellInfo(row, column);
                groupingGrid.ScrollIntoView(row, column); groupingGrid.Focus(); groupingGrid.BeginEdit();
                SetStatus(row.FileName + "：" + row.Validation, true);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        private void CopyGrouping()
        {
            try { CommitEdits(); Clipboard.SetText(GroupingTransfer.Copy(rows.Select(x => x.Metadata))); SetStatus("已复制七列分组模板，可在 Excel/WPS 编辑后连表头粘贴。源文件名用于精确对应。", false); }
            catch (InvalidOperationException exception) { SetStatus(exception.Message, true); }
            catch (Exception) { SetStatus("无法访问剪贴板，请关闭占用剪贴板的软件后重试。", true); }
        }
        private void PasteGrouping()
        {
            try { ApplyGroupingText(Clipboard.GetText()); }
            catch (Exception exception) { SetStatus("粘贴未应用：" + exception.Message, true); }
        }
        internal void ApplyGroupingText(string text)
        {
            CommitEdits(); var updates = GroupingTransfer.Parse(text, session.Samples);
            batchingEdits = true;
            try { foreach (var update in updates) WorkProjectStore.Apply(update, session.Samples.Single(x => x.SourcePath == update.SourcePath)); }
            finally { batchingEdits = false; }
            groupingGrid.Items.Refresh(); GroupingEdited();
            SetStatus("已按文件名更新 " + updates.Length + " 份。请核对实验条件并重新确认分组。", false);
        }

        private UIElement BuildChartView(PhAnalysisResult result)
        {
            var dock = new DockPanel(); var toolbar = new WrapPanel();
            var fit = new CheckBox { Content = "适应窗口（完整图）", IsChecked = true, Margin = new Thickness(8, 8, 16, 8) };
            toolbar.Children.Add(fit); toolbar.Children.Add(Note("悬停查看原始值，点击测量点或双击明细查看源光谱"));
            DockPanel.SetDock(toolbar, Dock.Top); dock.Children.Add(toolbar);
            var host = new Grid(); dock.Children.Add(host);
            Action refresh = delegate
            {
                host.Children.Clear(); chart = new PhResponseChart(result, synthetic, fit.IsChecked == true);
                chart.PointSelected += OpenTrace;
                if (fit.IsChecked == true) host.Children.Add(new Viewbox { Child = chart, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly });
                else host.Children.Add(new ScrollViewer { Content = chart, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            };
            fit.Checked += delegate { refresh(); }; fit.Unchecked += delegate { refresh(); }; refresh(); return dock;
        }
        private void OpenTrace(string fileName)
        {
            var dataset = sourceBatch.ValidDatasets.SingleOrDefault(x => string.Equals(Path.GetFileName(x.SourcePath), fileName, StringComparison.OrdinalIgnoreCase));
            if (dataset == null) return;
            var dock = new DockPanel { Margin = new Thickness(20) };
            var label = Note(fileName + "\n原始完整光谱；仅查看，不改变预览勾选和分析参与状态。\n内容指纹：" + SourceIdentity.ForDataset(dataset));
            DockPanel.SetDock(label, Dock.Top); dock.Children.Add(label);
            var spectrum = new SpectrumPlot(); spectrum.SetSeries(new[] { new PlotSeries(dataset, Theme.AccentBrush) }); dock.Children.Add(spectrum);
            new Window { Owner = this, Title = "源文件溯源", Width = 960, Height = 600, MinWidth = 640, MinHeight = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = dock }.Show();
        }
        private void MarkDirty()
        {
            if (initializing || batchingEdits) return;
            dirty = true; projectLabel.Text = "有未保存的工作进度";
        }
        internal void ClearDirtyForQa() { dirty = false; }
        private async void SaveProject() { await SaveProjectAsync(); }
        private async Task<bool> SaveProjectAsync()
        {
            if (busy) return false;
            CommitEdits();
            var dialog = new SaveFileDialog { Title = "保存本地工作项目", Filter = "FluoLab 工作项目 (*.fluolab)|*.fluolab", DefaultExt = ".fluolab", AddExtension = true,
                OverwritePrompt = true, FileName = projectPath == null ? (synthetic ? "模拟数据_" : "") + "pH工作项目.fluolab" : Path.GetFileName(projectPath) };
            if (projectPath != null) dialog.InitialDirectory = Path.GetDirectoryName(projectPath);
            if (dialog.ShowDialog(this) != true) return false;
            try
            {
                var snapshot = WorkProjectStore.Capture(sourceBatch, session, dialog.FileName, synthetic);
                busy = true; steps.IsEnabled = false;
                await Task.Run(delegate { var bytes = WorkProjectStore.Serialize(snapshot); AtomicOutput.Write(dialog.FileName, true, temp => File.WriteAllBytes(temp, bytes)); });
                projectPath = dialog.FileName; dirty = false; projectLabel.Text = "已保存：" + Path.GetFileName(projectPath);
                SetStatus("工作进度已保存；不保存旧结果或确认授权，恢复后需重新核对分组。", false); return true;
            }
            catch (Exception exception) { SetStatus("保存失败：" + exception.Message + " 请检查文件占用或选择其他位置。", true); return false; }
            finally { busy = false; steps.IsEnabled = true; }
        }
        private async void OpenProject()
        {
            if (busy) return;
            var dialog = new OpenFileDialog { Title = "在当前批次恢复工作项目", Filter = "FluoLab 工作项目 (*.fluolab)|*.fluolab" };
            var hadUnsavedDraft = dirty;
            if (dialog.ShowDialog(this) != true || !await TryCloseDraftAsync()) return;
            try
            {
                busy = true; steps.IsEnabled = false;
                var project = await Task.Run(delegate { return WorkProjectStore.Read(dialog.FileName); });
                if (project.Synthetic != synthetic) throw new InvalidOperationException("模拟与实际批次不能交叉恢复，请从主窗口打开该工作项目。");
                var restored = await Task.Run(delegate { return WorkProjectStore.Restore(project, sourceBatch); });
                RestoreDraft(restored, dialog.FileName);
            }
            catch (Exception exception) { if (hadUnsavedDraft) MarkDirty(); SetStatus("恢复失败：" + exception.Message + " 当前分组未被替换。", true); }
            finally { busy = false; steps.IsEnabled = true; }
        }
        internal void RestoreDraft(PhAnalysisSession restored, string path)
        {
            initializing = true;
            for (var i = 0; i < rows.Count; i++) WorkProjectStore.Apply(restored.Samples[i], session.Samples[i]);
            wavelength.Text = restored.Parameters.WavelengthText; unit.Text = restored.Parameters.IntensityUnit;
            session.Parameters.WavelengthText = wavelength.Text; session.Parameters.IntensityUnit = unit.Text;
            session.RevokeGrouping(); affirmation.IsChecked = false; initializing = false;
            projectPath = path; dirty = false; projectLabel.Text = "已恢复：" + Path.GetFileName(path);
            groupingGrid.Items.Refresh(); InvalidateResult(); RefreshValidation(); steps.SelectedIndex = 0;
            SetStatus("源文件内容指纹核对通过。已恢复工作进度，请重新核对并确认分组后生成结果。", false);
        }
        internal async Task<bool> TryCloseDraftAsync()
        {
            if (busy) { SetStatus("任务进行中，请等待完成后再关闭或更换批次。", true); return false; }
            if (!dirty) return true;
            var answer = CloseDecision == null ? MessageBox.Show(this, "工作进度尚未保存。是否保存后再离开？\n“否”将放弃未保存的分组和参数。", "保存工作进度", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) : CloseDecision();
            if (answer == MessageBoxResult.Cancel) return false;
            if (answer == MessageBoxResult.Yes) return await SaveProjectAsync();
            dirty = false; return true;
        }
        private async void OnDraftClosing(object sender, CancelEventArgs args)
        {
            if (busy || closePending) { args.Cancel = true; return; }
            if (!dirty) return;
            args.Cancel = true; closePending = true;
            var close = await TryCloseDraftAsync(); closePending = false;
            if (close) await Dispatcher.BeginInvoke(new Action(Close));
        }
        private async void ExportBundle()
        {
            if (busy) return;
            try
            {
                var result = session.RequireCurrentResult();
                var dialog = new SaveFileDialog { Title = "导出完整分析包", Filter = "ZIP 分析包 (*.zip)|*.zip", DefaultExt = ".zip", AddExtension = true,
                    OverwritePrompt = true, FileName = (synthetic ? "模拟数据_" : "") + "pH分析包_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip" };
                if (dialog.ShowDialog(this) != true) return;
                var raw = includeRaw.IsChecked == true;
                busy = true; steps.IsEnabled = false; SetStatus("正在打包分析表、完整图像及指纹记录…", false);
                await ExportBundleOnWorker(result, sourceBatch, dialog.FileName, raw, synthetic);
                SetStatus("完整分析包已保存：" + Path.GetFileName(dialog.FileName), false);
            }
            catch (Exception exception) { SetStatus("打包失败：" + exception.Message + " 请核对源文件、文件占用和保存位置后重试。", true); }
            finally { busy = false; steps.IsEnabled = true; }
        }
        internal static Task ExportBundleOnWorker(PhAnalysisResult result, SpectrumBatch batch, string path, bool raw, bool synthetic)
        {
            var completion = new TaskCompletionSource<bool>();
            var worker = new System.Threading.Thread(delegate()
            {
                try
                {
                    AnalysisBundleExporter.Export(result, batch, path, true, raw, synthetic, delegate(string png, string svg)
                    { var output = new PhResponseChart(result, synthetic); output.ExportPng(png, false); output.ExportSvg(svg, false); });
                    completion.SetResult(true);
                }
                catch (Exception exception) { completion.SetException(exception); }
            });
            worker.SetApartmentState(System.Threading.ApartmentState.STA); worker.IsBackground = true; worker.Start(); return completion.Task;
        }
    }
}
