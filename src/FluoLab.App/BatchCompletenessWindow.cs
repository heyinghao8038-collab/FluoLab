using System;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using FluoLab.Core;
using FluoLab.Export;
using Microsoft.Win32;

namespace FluoLab.App
{
    internal sealed class BatchCompletenessWindow : Window
    {
        private readonly BatchCompletenessReport report;
        private readonly TextBox search = new TextBox { MinWidth = 180, Margin = new Thickness(0, 8, 0, 10), ToolTip = "按源文件名筛选，仅影响此表显示" };
        private readonly DataGrid grid = new DataGrid { IsReadOnly = true, AutoGenerateColumns = false, CanUserAddRows = false, EnableRowVirtualization = true, EnableColumnVirtualization = true, FrozenColumnCount = 1, HeadersVisibility = DataGridHeadersVisibility.Column, RowHeaderWidth = 0, MinHeight = 120 };
        private readonly TextBlock status = Note("");
        private readonly Button export = new Button { Content = "导出完整性摘要", Margin = new Thickness(0, 4, 0, 4) };

        internal BatchCompletenessWindow(BatchCompletenessReport value)
        {
            report = value;
            Title = "FluoLab 批次完整性"; Width = 1140; Height = 720; MinWidth = 720; MinHeight = 440;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new StackPanel();
            header.Children.Add(new TextBlock { Text = "批次完整性 · " + report.BatchName, TextWrapping = TextWrapping.Wrap, FontSize = 20, FontWeight = FontWeights.SemiBold });
            header.Children.Add(Note("发现 " + report.Files.Length + " 份  ·  有效 " + report.ValidCount + " 份  ·  解析失败 " + (report.Files.Length - report.ValidCount) + " 份"));
            header.Children.Add(new TextBlock { Text = "波长并集 " + report.Union.Length + " 点  ·  共同测量波长 " + report.Intersection.Length + " 点", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Theme.AccentBrush, TextWrapping = TextWrapping.Wrap });
            header.Children.Add(Note(report.ValidCount == 0 ? "没有有效光谱；请检查下方解析原因，再重新导入。" : report.Intersection.Length == 0 ? "有效文件没有共同测量点；范围看似重叠不代表数值相同。指定波长分析需逐文件检查。" : "并集范围：" + Range(report.Union) + " nm；共同点范围：" + Range(report.Intersection) + " nm（范围内不保证连续）"));
            header.Children.Add(Note(BatchCompleteness.Explanation)); root.Children.Add(header);
            search.TextChanged += delegate { ApplyFilter(); }; Grid.SetRow(search, 1); root.Children.Add(search);
            Column("源文件名", "FileName", 340, null); Column("解析状态", "Status", 95, null); Column("测量点数", "PointCount", 90, null);
            Column("最小波长 / nm", "Minimum", 120, "G17"); Column("最大波长 / nm", "Maximum", 120, "G17"); Column("缺点数", "MissingCount", 80, null); Column("解析失败原因", "Reason", 360, null);
            var tooltip = new Style(typeof(DataGridRow)); tooltip.Setters.Add(new Setter(ToolTipProperty, new Binding("FileName"))); grid.RowStyle = tooltip;
            Grid.SetRow(grid, 2); root.Children.Add(grid);
            var footer = new StackPanel(); footer.Children.Add(status); footer.Children.Add(export); Grid.SetRow(footer, 3); root.Children.Add(footer);
            export.Click += async delegate { await Export(); }; Content = root; ApplyFilter();
        }
        private void Column(string name, string property, double width, string format)
        {
            var binding = new Binding(property) { TargetNullValue = "—" };
            if (format != null) binding.StringFormat = "{0:" + format + "}";
            grid.Columns.Add(new DataGridTextColumn { Header = name, Binding = binding, Width = width });
        }
        private void ApplyFilter()
        {
            var text = search.Text.Trim();
            var rows = report.Files.Where(row => row.FileName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            grid.ItemsSource = rows; status.Text = "显示 " + rows.Length + "/" + report.Files.Length + " 份。筛选不影响摘要导出，摘要始终包含本批全部文件。";
        }
        private async Task Export()
        {
            var picker = new SaveFileDialog { Title = "导出批次完整性摘要", Filter = "UTF-8 文本 (*.txt)|*.txt", DefaultExt = ".txt", AddExtension = true, OverwritePrompt = true, FileName = report.BatchName + "_完整性摘要.txt" };
            if (picker.ShowDialog(this) != true) return;
            export.IsEnabled = false; status.Text = "正在导出摘要…";
            try { await Task.Run(delegate { AtomicOutput.Write(picker.FileName, true, temp => File.WriteAllText(temp, BatchCompleteness.Text(report), new UTF8Encoding(true))); }); status.Text = "已导出完整摘要，不含原始荧光强度或源文件绝对路径。"; }
            catch (Exception error) { status.Text = "导出失败：" + error.Message + " 请关闭占用文件或换位置重试。"; }
            finally { export.IsEnabled = true; }
        }
        private static string Range(double[] values) { return values[0].ToString("G17", CultureInfo.InvariantCulture) + "–" + values[values.Length - 1].ToString("G17", CultureInfo.InvariantCulture); }
        private static TextBlock Note(string text) { return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Theme.SecondaryTextBrush, Margin = new Thickness(0, 6, 0, 6), FontSize = 12 }; }
        internal static BatchCompletenessWindow FixedDemo()
        { return FixedState("ready"); }
        internal static BatchCompletenessWindow FixedState(string state)
        {
            var batch = PhDemo.CreateLongNameBatch();
            if (state == "empty") return new BatchCompletenessWindow(BatchCompleteness.Create(new SpectrumBatch(batch.SourceFolder, DateTimeOffset.Now, new FileProcessingResult[0]), CancellationToken.None));
            var files = batch.Files.ToList();
            files.Add(FileProcessingResult.Failed(Path.Combine(batch.SourceFolder, "模拟异常文件.ASC"), "未找到 #DATA 标记，请检查仪器导出格式。"));
            if (state == "failed") files = files.Where(file => file.Status == ProcessingStatus.Failed).ToList();
            return new BatchCompletenessWindow(BatchCompleteness.Create(new SpectrumBatch(batch.SourceFolder, DateTimeOffset.Now, files), CancellationToken.None));
        }
    }
}
