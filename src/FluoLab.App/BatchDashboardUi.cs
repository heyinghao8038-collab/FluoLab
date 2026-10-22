using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FluoLab.Core;

namespace FluoLab.App
{
    internal sealed partial class MainWindow
    {
        private BatchDashboard dashboard;
        private ColumnDefinition dashboardColumn;
        private Button dashboardToggle;
        private bool dashboardExpanded = true;
        private Window dashboardWindow;
        private BatchDashboard detachedDashboard;
        private CancellationTokenSource dashboardCancellation;
        private SpectrumBatch dashboardReportBatch;
        private int dashboardRevision;
        private bool dashboardBusy, dashboardClosed;

        private BatchDashboard CreateDashboardView()
        {
            return new BatchDashboard(category => ShowProcessingLog(category), delegate { completenessButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); });
        }
        private void UpdateDashboardLayout()
        {
            if (dashboardColumn == null) return;
            var expanded = DashboardViewportWidth() >= 1200 && dashboardExpanded;
            dashboardColumn.Width = new GridLength(expanded ? 292 : 0);
            dashboard.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            if (expanded && dashboardWindow != null) dashboardWindow.Close();
            dashboardToggle.Content = expanded ? "收起概况" : "数据概况";
        }
        private void ToggleDashboard()
        {
            if (DashboardViewportWidth() >= 1200) { dashboardExpanded = !dashboardExpanded; UpdateDashboardLayout(); return; }
            if (dashboardWindow != null) { dashboardWindow.Activate(); return; }
            detachedDashboard = CreateDashboardView();
            if (dashboard.Report != null) detachedDashboard.Present(dashboard.Report, SelectedPreviewCount());
            else detachedDashboard.ShowStatus(dashboardBusy || isLoading ? "正在处理…" : "暂无统计", dashboardBusy || isLoading ? "请等待当前批次汇总完成。" : "导入 ASC 后自动汇总本批文件。", dashboardBusy || isLoading);
            dashboardWindow = new Window { Title = "FluoLab 本批数据概况", Width = 350, Height = 730, MinWidth = 310, MinHeight = 420, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = detachedDashboard, Padding = new Thickness(10) };
            dashboardWindow.Closed += delegate { dashboardWindow = null; detachedDashboard = null; }; dashboardWindow.Show();
        }
        private double DashboardViewportWidth()
        {
            var viewport = Content as FrameworkElement;
            return viewport == null ? ActualWidth : viewport.ActualWidth;
        }
        private int SelectedPreviewCount() { return fileRows.Count(row => row.CheckBox.IsChecked == true && row.Result.Dataset != null); }
        private void HideDashboardForImport()
        {
            dashboardRevision++; if (dashboardCancellation != null) dashboardCancellation.Cancel(); dashboardBusy = false; dashboardReportBatch = null;
            dashboard.ShowStatus("正在导入…", "上一批统计已隐藏，完成后显示当前批次。", true);
            if (dashboardWindow != null) dashboardWindow.Close();
        }
        private async void RefreshDashboard() { if (currentBatch != null) await RefreshDashboardAsync(currentBatch); }
        private async Task RefreshDashboardAsync(SpectrumBatch batch)
        {
            if (dashboardClosed) return;
            if (dashboardCancellation != null) dashboardCancellation.Cancel();
            var cancellation = new CancellationTokenSource(); dashboardCancellation = cancellation;
            var revision = ++dashboardRevision; dashboardBusy = true; dashboardReportBatch = null;
            dashboard.ShowStatus("正在汇总…", "统计覆盖当前批次的全部 ASC。", true);
            try {
                var report = await Task.Run(delegate { return BatchCompleteness.Create(batch, cancellation.Token); });
                if (dashboardClosed || cancellation.IsCancellationRequested || revision != dashboardRevision || !ReferenceEquals(currentBatch, batch)) return;
                dashboardReportBatch = batch; dashboard.Present(report, SelectedPreviewCount());
                if (detachedDashboard != null) detachedDashboard.Present(report, SelectedPreviewCount());
            }
            catch (OperationCanceledException) { }
            catch (Exception) {
                if (revision == dashboardRevision && !dashboardClosed) {
                    dashboard.ShowStatus("概况暂不可用", "汇总失败。原始数据未修改，可重新导入或查看处理日志。", false);
                    if (detachedDashboard != null) detachedDashboard.ShowStatus("概况暂不可用", "请重新导入后重试。", false);
                }
            }
            finally { if (ReferenceEquals(dashboardCancellation, cancellation)) { dashboardCancellation = null; dashboardBusy = false; } cancellation.Dispose(); }
        }
        internal void LoadDashboardDemoForQa(string state)
        {
            var batch = PhDemo.CreateBatch();
            if (state == "empty") batch = new SpectrumBatch(batch.SourceFolder, DateTimeOffset.Now, new FileProcessingResult[0]);
            if (state == "failed") batch = new SpectrumBatch(batch.SourceFolder, DateTimeOffset.Now, batch.Files.Where(file => file.Status == ProcessingStatus.Failed).ToArray());
            ApplyBatch(batch, TimeSpan.FromMilliseconds(250));
            dashboardRevision++; if (dashboardCancellation != null) dashboardCancellation.Cancel(); dashboardBusy = false;
            folderPathText.Text = "模拟批次 · SyntheticDemo（非实验数据）";
            dashboard.Present(BatchCompleteness.Create(batch, CancellationToken.None), SelectedPreviewCount());
            if (state == "loading") dashboard.ShowStatus("正在导入…", "上一批统计已隐藏，完成后显示当前批次。", true);
        }
    }
}
