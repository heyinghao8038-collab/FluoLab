using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using FluoLab.Core;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace FluoLab.App
{
    internal sealed partial class MainWindow
    {
        private async void OpenWorkProject()
        {
            if (isLoading || isExporting) { SetStatus("请等待当前任务完成后再打开工作项目。", Theme.WarningBrush); return; }
            var dialog = new OpenFileDialog { Title = "打开本地工作项目", Filter = "FluoLab 工作项目 (*.fluolab)|*.fluolab" };
            if (dialog.ShowDialog(this) != true) return;
            if (phWindow != null)
            {
                if (!await phWindow.TryCloseDraftAsync()) return;
                phWindow.Close();
            }
            var applied = false;
            try
            {
                isLoading = true; importCancellation = new System.Threading.CancellationTokenSource(); SetLoadingState("工作项目", true);
                var project = await Task.Run(delegate { return WorkProjectStore.Read(dialog.FileName); });
                string folder = null;
                var virtualSynthetic = project.Synthetic && string.IsNullOrWhiteSpace(project.SourceFolder);
                if (!virtualSynthetic)
                {
                    try { folder = WorkProjectStore.ResolveFolder(project, dialog.FileName); }
                    catch (InvalidOperationException) { }
                    var answer = Directory.Exists(folder) ? MessageBox.Show(this, "工作项目对应的数据文件夹：\n" + folder + "\n\n是否读取并核对文件指纹？选择“否”可重新定位文件夹。",
                        "确认源数据位置", MessageBoxButton.YesNoCancel, MessageBoxImage.Question) : MessageBoxResult.No;
                    if (answer == MessageBoxResult.Cancel) return;
                    if (answer == MessageBoxResult.No)
                    {
                        using (var picker = new Forms.FolderBrowserDialog { Description = "定位工作项目对应的 ASC 文件夹（内容必须一致）" })
                        {
                            if (picker.ShowDialog() != Forms.DialogResult.OK) return;
                            folder = picker.SelectedPath;
                        }
                    }
                }
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var progress = new Progress<ProcessingProgress>(value =>
                {
                    importProgress.Maximum = Math.Max(1, value.Total); importProgress.Value = value.Completed;
                    progressText.Text = "正在核对源文件 " + value.Completed + "/" + value.Total;
                });
                var batch = await Task.Run(delegate { return virtualSynthetic ? PhDemo.CreateBatch() : processor.ProcessFolder(folder, progress, importCancellation.Token); });
                watch.Stop(); var parseElapsed = watch.Elapsed;
                importCancellation.Token.ThrowIfCancellationRequested();
                var restored = await Task.Run(delegate { return WorkProjectStore.Restore(project, batch); });
                importCancellation.Token.ThrowIfCancellationRequested();
                ApplyBatch(batch, parseElapsed);
                applied = true;
                phWindow = new PhAnalysisWindow(batch, project.Synthetic) { Owner = this };
                phWindow.RestoreDraft(restored, dialog.FileName); phWindow.Closed += delegate { phWindow = null; }; phWindow.Show();
                SetStatus("工作项目已恢复，源文件内容一致；请在分析窗口重新确认分组。", Theme.SuccessBrush);
            }
            catch (OperationCanceledException) { SetStatus("已取消打开工作项目，原有数据未改变。", Theme.WarningBrush); }
            catch (Exception exception)
            {
                SetStatus("工作项目未恢复：" + exception.Message, Theme.ErrorBrush);
                MessageBox.Show(this, exception.Message + "\n\n原始数据未被修改。请核对文件夹或重新导入。", "无法恢复工作项目", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                isLoading = false; SetLoadingState("", false);
                if (importCancellation != null) { importCancellation.Dispose(); importCancellation = null; }
                if (!applied)
                {
                    folderPathText.Text = currentBatch == null ? "拖入包含 ASC 文件的文件夹" : currentBatch.SourceFolder;
                    importSummaryText.Text = currentBatch == null ? "只读取当前文件夹，不扫描子文件夹" : BuildImportSummary(currentBatch, TimeSpan.Zero, false);
                }
            }
        }
    }
}
