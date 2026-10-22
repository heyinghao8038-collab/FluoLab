using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluoLab.Core;

namespace FluoLab.App
{
    internal sealed class BatchDashboard : Border
    {
        private readonly StackPanel body = new StackPanel();
        private readonly TextBlock preview = Text("勾选仅影响曲线预览", 12, Theme.SecondaryTextBrush);
        private readonly Action<string> failureAction;
        private readonly Action coverageAction;
        internal BatchCompletenessReport Report { get; private set; }

        internal BatchDashboard(Action<string> failures, Action coverage)
        {
            failureAction = failures; coverageAction = coverage;
            Background = Theme.SurfaceBrush; CornerRadius = new CornerRadius(14); Padding = new Thickness(16);
            BorderBrush = Theme.DividerBrush; BorderThickness = new Thickness(1);
            var layout = new Grid(); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            header.Children.Add(Text("当前批次 · 文件与波长", 10, Theme.AccentBrush));
            var title = Text("本批数据概况", 18, Theme.PrimaryTextBrush); title.FontWeight = FontWeights.SemiBold; title.Margin = new Thickness(0, 3, 0, 0); header.Children.Add(title); layout.Children.Add(header);
            var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 3, 0) };
            Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
            var footer = new StackPanel { Margin = new Thickness(0, 12, 0, 0) }; footer.Children.Add(preview);
            footer.Children.Add(Text("统计覆盖本批全部 ASC，不随勾选变化。", 11, Theme.SecondaryTextBrush));
            Grid.SetRow(footer, 2); layout.Children.Add(footer); Child = layout;
            ShowStatus("尚未导入 ASC", "选择或拖入文件夹后，自动汇总文件状态和波长覆盖。", false);
        }

        internal void ShowStatus(string title, string explanation, bool loading)
        {
            Report = null; body.Children.Clear();
            if (loading) body.Children.Add(new ProgressBar { IsIndeterminate = true, Height = 3, Foreground = Theme.AccentBrush, Margin = new Thickness(0, 2, 0, 16) });
            var heading = Text(title, 15, Theme.PrimaryTextBrush); heading.FontWeight = FontWeights.SemiBold; body.Children.Add(heading);
            body.Children.Add(Text(explanation, 12, Theme.SecondaryTextBrush)); preview.Text = "勾选仅影响曲线预览";
        }

        internal void Present(BatchCompletenessReport report, int previewCount)
        {
            Report = report; body.Children.Clear();
            var batch = Text(report.BatchName, 11, Theme.SecondaryTextBrush); batch.TextTrimming = TextTrimming.CharacterEllipsis; batch.TextWrapping = TextWrapping.NoWrap; batch.ToolTip = report.BatchName; batch.Margin = new Thickness(0, 0, 0, 8); body.Children.Add(batch);
            var metrics = new Grid(); for (var i = 0; i < 3; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
            Metric(metrics, 0, "发现 ASC", report.Files.Length, Theme.PrimaryTextBrush, null);
            Metric(metrics, 1, "有效", report.ValidCount, Theme.SuccessBrush, null);
            var failed = report.Files.Length - report.ValidCount;
            Metric(metrics, 2, "解析失败", failed, failed > 0 ? Theme.WarningBrush : Theme.SecondaryTextBrush, failed > 0 ? (Action)delegate { failureAction(null); } : null);
            body.Children.Add(metrics);
            var statusText = report.Files.Length == 0 ? "当前文件夹未发现 ASC" : report.ValidCount == 0 ? "暂无可用光谱" : failed == 0 ? "所有 ASC 均解析成功" : "部分文件无法读取";
            var badge = new Border { CornerRadius = new CornerRadius(8), Background = Brush(failed > 0 ? "#FFF5E6" : report.ValidCount == 0 ? "#F5F7FA" : "#EDF7F1"), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 10, 0, 0), Child = Text(statusText, 12, failed > 0 ? Theme.WarningBrush : report.ValidCount == 0 ? Theme.SecondaryTextBrush : Theme.SuccessBrush) }; body.Children.Add(badge);
            Section("失败原因");
            if (failed == 0) body.Children.Add(Text("本批未发现解析失败文件。", 12, Theme.SecondaryTextBrush));
            foreach (var group in report.Files.Where(row => row.Status == "解析失败").GroupBy(row => row.FailureCategory).OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal)) {
                var category = group.Key; body.Children.Add(Link(category + "  ·  " + group.Count() + " 份  ›", delegate { failureAction(category); }));
            }
            Section("数据规模"); Detail("测量点总数", report.TotalMeasurementCount.ToString("N0", CultureInfo.InvariantCulture));
            Detail("波长并集范围", report.Union.Length == 0 ? "—" : Number(report.Union[0]) + "–" + Number(report.Union[report.Union.Length - 1]) + " nm");
            Section("波长覆盖");
            body.Children.Add(Text(report.ValidCount == 0 ? "没有有效光谱，无法比较" : report.CoverageConsistent ? "有效文件的波长覆盖一致" : "有效文件的波长覆盖不一致", 12, report.ValidCount == 0 ? Theme.SecondaryTextBrush : report.CoverageConsistent ? Theme.SuccessBrush : Theme.WarningBrush));
            Detail("共同测量点", report.ValidCount == 0 ? "—" : report.Intersection.Length.ToString("N0", CultureInfo.InvariantCulture));
            if (report.ValidCount > 0) {
                body.Children.Add(Link("查看波长覆盖详情  ›", coverageAction));
            }
            body.Children.Add(Text("仅跳过解析失败文件；源文件不删除。不据此判断实验质量或是否漏测。", 11, Theme.SecondaryTextBrush)); SetPreviewCount(previewCount);
        }
        internal void SetPreviewCount(int count) { preview.Text = "当前预览 " + count + " 条 · 勾选仅影响预览"; }
        private static void Metric(Grid grid, int column, string title, int value, Brush color, Action action)
        {
            var panel = new StackPanel(); var number = Text(value.ToString(CultureInfo.InvariantCulture), 22, color); number.FontWeight = FontWeights.SemiBold; panel.Children.Add(number); panel.Children.Add(Text(title, 11, Theme.SecondaryTextBrush));
            var tile = new Border { Background = Brush("#F5F7FA"), CornerRadius = new CornerRadius(9), Padding = new Thickness(8, 7, 5, 7), Margin = new Thickness(0, 0, column < 2 ? 5 : 0, 0), Child = panel };
            UIElement content = tile;
            if (action != null) { var button = new Button { Content = tile, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, ToolTip = "查看失败文件名和具体原因" }; button.Click += delegate { action(); }; content = button; }
            Grid.SetColumn(content, column); grid.Children.Add(content);
        }
        private void Section(string name) { body.Children.Add(new Border { Height = 1, Background = Brush("#E9EBEF"), Margin = new Thickness(0, 11, 0, 8) }); var text = Text(name, 13, Theme.PrimaryTextBrush); text.FontWeight = FontWeights.SemiBold; text.Margin = new Thickness(0, 0, 0, 4); body.Children.Add(text); }
        private void Detail(string label, string value) { var row = new Grid { Margin = new Thickness(0, 3, 0, 3) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(85) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.Children.Add(Text(label, 11, Theme.SecondaryTextBrush)); var text = Text(value, 13, Theme.PrimaryTextBrush); Grid.SetColumn(text, 1); row.Children.Add(text); body.Children.Add(row); }
        private static Button Link(string label, Action action) { var button = new Button { Content = label, HorizontalContentAlignment = HorizontalAlignment.Left, FontSize = 12, Foreground = Theme.AccentBrush, Padding = new Thickness(0, 7, 0, 7), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Margin = new Thickness(0, 3, 0, 0) }; button.Click += delegate { action(); }; return button; }
        private static TextBlock Text(string value, double size, Brush color) { return new TextBlock { Text = value, FontSize = size, Foreground = color, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) }; }
        private static Brush Brush(string color) { return (Brush)new BrushConverter().ConvertFromString(color); }
        private static string Number(double value) { return value.ToString("G10", CultureInfo.InvariantCulture); }
    }
}
