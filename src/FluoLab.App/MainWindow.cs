using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using FluoLab.Core;
using FluoLab.Export;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace FluoLab.App
{
    internal sealed partial class MainWindow : Window
    {
        private const int MaximumPreviewSeries = 60;
        private static readonly Color[] SeriesColors =
        {
            Color.FromRgb(0x00, 0x71, 0xE3),
            Color.FromRgb(0xE8, 0x45, 0x45),
            Color.FromRgb(0x2E, 0x9E, 0x5B),
            Color.FromRgb(0x8E, 0x44, 0xAD),
            Color.FromRgb(0xF5, 0xA6, 0x23),
            Color.FromRgb(0x00, 0x9C, 0xA6),
            Color.FromRgb(0xD4, 0x50, 0x87),
            Color.FromRgb(0x5A, 0x67, 0xD8),
            Color.FromRgb(0x7A, 0x7A, 0x7A),
            Color.FromRgb(0x00, 0x83, 0x6A)
        };

        private readonly BatchProcessor processor = new BatchProcessor();
        private readonly WorkbookExporter exporter = new WorkbookExporter();
        private readonly List<FileRowView> fileRows = new List<FileRowView>();
        private readonly DispatcherTimer plotUpdateTimer;
        private readonly Stopwatch loadingWatch = new Stopwatch();
        private Border importCard;
        private TextBlock folderPathText;
        private TextBlock importSummaryText;
        private ProgressBar importProgress;
        private TextBlock progressText;
        private Button cancelButton;
        private Button chooseFolderButton;
        private Button exportButton;
        private Button phButton;
        private PhAnalysisWindow phWindow;
        private Button openOutputButton;
        private Button logButton;
        private TextBox searchBox;
        private TextBlock fileCountText;
        private StackPanel fileRowsPanel;
        private SpectrumPlot plot;
        private TextBlock plotSummaryText;
        private TextBlock statusText;
        private SpectrumBatch currentBatch;
        private CancellationTokenSource importCancellation;
        private string lastOutputPath;
        private bool isLoading;
        private bool isExporting;

        public MainWindow()
        {
            Title = "FluoLab 荧光数据分析器 · 1.3.1";
            Width = 1440;
            Height = 900;
            MinWidth = 800;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Theme.BackgroundBrush;
            AllowDrop = false;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            Content = BuildLayout();
            Closing += async delegate(object sender, System.ComponentModel.CancelEventArgs args)
            {
                foreach (var modules in OwnedWindows.OfType<LaboratoryModulesWindow>().ToArray())
                { modules.Close(); if (modules.IsVisible) { args.Cancel = true; return; } }
                var analyses = OwnedWindows.OfType<PhAnalysisWindow>().ToArray();
                if (analyses.Length == 0) return;
                args.Cancel = true;
                foreach (var analysis in analyses)
                {
                    if (!await analysis.TryCloseDraftAsync()) return;
                    analysis.Close();
                }
                await Dispatcher.BeginInvoke(new Action(Close));
            };

            plotUpdateTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(80),
                DispatcherPriority.Background,
                delegate
                {
                    plotUpdateTimer.Stop();
                    ApplySelectedSeries();
                },
                Dispatcher);
        }

        public void LoadDemoData()
        {
            var files = new List<FileProcessingResult>();
            for (var fileIndex = 0; fileIndex < 12; fileIndex++)
            {
                var waves = new double[620];
                var intensities = new double[620];
                for (var index = 0; index < waves.Length; index++)
                {
                    waves[index] = 490 + index * 0.5;
                    var peak = 570 + fileIndex * 3.2;
                    var gaussian = 5000 * Math.Exp(-Math.Pow((waves[index] - peak) / 34, 2));
                    intensities[index] = 180 + gaussian * (0.48 + fileIndex * 0.045) + 40 * Math.Sin(index / 23.0);
                }

                var source = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    string.Format("10uM Probe pH={0:0.0}-{1}.ASC", 2.4 + fileIndex * 0.5, fileIndex % 3 + 1));
                files.Add(FileProcessingResult.Succeeded(
                    source,
                    new SpectrumDataset(Path.GetFileNameWithoutExtension(source), source, waves, intensities)));
            }

            var failedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "异常样品.ASC");
            files.Add(FileProcessingResult.Failed(failedPath, "第 58 行包含非数值或无效数值。"));
            ApplyBatch(new SpectrumBatch(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                DateTimeOffset.Now,
                files),
                TimeSpan.FromSeconds(0.82));
        }

        public void LoadFolderForAutomation(string folderPath)
        {
            LoadFolderAsync(folderPath);
        }

        private UIElement BuildLayout()
        {
            var root = new Grid
            {
                Margin = new Thickness(24, 0, 24, 16)
            };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(72) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(116) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = BuildHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            importCard = BuildImportCard();
            Grid.SetRow(importCard, 1);
            root.Children.Add(importCard);

            var workspace = BuildWorkspace();
            Grid.SetRow(workspace, 2);
            root.Children.Add(workspace);

            var footer = BuildFooter();
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);
            return root;
        }

        private UIElement BuildHeader()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titlePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            var mark = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(11),
                Background = Theme.AccentBrush,
                Child = new TextBlock
                {
                    Text = "F",
                    Foreground = Brushes.White,
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            titlePanel.Children.Add(mark);
            titlePanel.Children.Add(new TextBlock
            {
                Text = "FluoLab",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 8, 0)
            });
            titlePanel.Children.Add(new TextBlock
            {
                Text = "荧光数据分析器",
                FontSize = 14,
                Foreground = Theme.SecondaryTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(titlePanel, 0);
            grid.Children.Add(titlePanel);

            var privacyBadge = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromRgb(0xE9, 0xF6, 0xEE)),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "●  数据仅在本机处理",
                    Foreground = Theme.SuccessBrush,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold
                }
            };
            phButton = new Button { Content = "pH 响应分析", IsEnabled = false, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
            phButton.Click += delegate
            {
                if (currentBatch == null || currentBatch.SuccessCount == 0 || isLoading) return;
                if (phWindow != null) { phWindow.Activate(); return; }
                phWindow = new PhAnalysisWindow(currentBatch, false) { Owner = this };
                phWindow.Closed += delegate { phWindow = null; };
                phWindow.Show();
            };
            Grid.SetColumn(phButton, 1); grid.Children.Add(phButton);
            Grid.SetColumn(privacyBadge, 2);
            grid.Children.Add(privacyBadge);
            SizeChanged += delegate { privacyBadge.Visibility = ActualWidth < 1150 ? Visibility.Collapsed : Visibility.Visible; };

            exportButton = new Button
            {
                Content = "导出 Excel",
                Style = (Style)FindResource("PrimaryButton"),
                MinWidth = 120,
                IsEnabled = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            exportButton.Click += ExportButtonClick;
            Grid.SetColumn(exportButton, 3);
            grid.Children.Add(exportButton);
            return grid;
        }

        private Border BuildImportCard()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF2, 0xFD)),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "↳",
                    FontSize = 25,
                    Foreground = Theme.AccentBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            var textPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 18, 0)
            };
            folderPathText = new TextBlock
            {
                Text = "拖入包含 ASC 文件的文件夹",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            importSummaryText = new TextBlock
            {
                Text = "只读取当前文件夹，不扫描子文件夹",
                FontSize = 12,
                Foreground = Theme.SecondaryTextBrush,
                Margin = new Thickness(0, 5, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            progressText = new TextBlock
            {
                Text = string.Empty,
                FontSize = 11,
                Foreground = Theme.SecondaryTextBrush,
                Margin = new Thickness(0, 6, 0, 0),
                Visibility = Visibility.Collapsed
            };
            importProgress = new ProgressBar
            {
                Height = 4,
                Minimum = 0,
                Maximum = 1,
                Margin = new Thickness(0, 7, 0, 0),
                Visibility = Visibility.Collapsed,
                Foreground = Theme.AccentBrush,
                Background = Theme.SecondarySurfaceBrush
            };
            textPanel.Children.Add(folderPathText);
            textPanel.Children.Add(importSummaryText);
            textPanel.Children.Add(progressText);
            textPanel.Children.Add(importProgress);
            Grid.SetColumn(textPanel, 1);
            grid.Children.Add(textPanel);

            var actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            cancelButton = new Button
            {
                Content = "取消导入",
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 0, 8, 0)
            };
            cancelButton.Click += delegate
            {
                if (importCancellation != null)
                {
                    importCancellation.Cancel();
                }
            };
            chooseFolderButton = new Button
            {
                Content = "选择文件夹",
                MinWidth = 112
            };
            chooseFolderButton.Click += ChooseFolderButtonClick;
            actionPanel.Children.Add(cancelButton);
            actionPanel.Children.Add(chooseFolderButton);
            Grid.SetColumn(actionPanel, 2);
            grid.Children.Add(actionPanel);

            var card = CreateCard(grid, new Thickness(0, 0, 0, 16));
            card.Padding = new Thickness(18, 14, 18, 14);
            card.AllowDrop = true;
            card.PreviewDragOver += ImportCardDragOver;
            card.DragLeave += delegate { importCard.BorderBrush = Brushes.Transparent; };
            card.Drop += ImportCardDrop;
            return card;
        }

        private UIElement BuildWorkspace()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var filesCard = BuildFilesCard();
            Grid.SetColumn(filesCard, 0);
            grid.Children.Add(filesCard);

            var splitter = new GridSplitter
            {
                Width = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.Transparent,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext
            };
            Grid.SetColumn(splitter, 1);
            grid.Children.Add(splitter);

            var plotCard = BuildPlotCard();
            Grid.SetColumn(plotCard, 2);
            grid.Children.Add(plotCard);
            return grid;
        }

        private Border BuildFilesCard()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleGrid = new Grid { Margin = new Thickness(18, 16, 18, 10) };
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleGrid.Children.Add(new TextBlock
            {
                Text = "文件",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            fileCountText = new TextBlock
            {
                Text = "0 份",
                FontSize = 12,
                Foreground = Theme.SecondaryTextBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(fileCountText, 1);
            titleGrid.Children.Add(fileCountText);
            Grid.SetRow(titleGrid, 0);
            root.Children.Add(titleGrid);

            searchBox = new TextBox
            {
                Margin = new Thickness(18, 0, 18, 10),
                ToolTip = "按文件名筛选",
                Text = string.Empty
            };
            searchBox.TextChanged += delegate { ApplySearchFilter(); };
            Grid.SetRow(searchBox, 1);
            root.Children.Add(searchBox);

            var selectionBar = new Grid { Margin = new Thickness(10, 0, 10, 8) };
            selectionBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            selectionBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            selectionBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var selectAll = new Button
            {
                Content = "全选",
                Style = (Style)FindResource("QuietButton")
            };
            selectAll.Click += delegate { SelectAllVisible(); };
            selectionBar.Children.Add(selectAll);
            var clear = new Button
            {
                Content = "清空",
                Style = (Style)FindResource("QuietButton")
            };
            clear.Click += delegate { ClearSelection(); };
            Grid.SetColumn(clear, 1);
            selectionBar.Children.Add(clear);
            Grid.SetRow(selectionBar, 2);
            root.Children.Add(selectionBar);

            fileRowsPanel = new StackPanel();
            var scroll = new ScrollViewer
            {
                Content = fileRowsPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(8, 0, 8, 10)
            };
            Grid.SetRow(scroll, 3);
            root.Children.Add(scroll);
            return CreateCard(root, new Thickness(0, 0, 0, 0));
        }

        private Border BuildPlotCard()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var toolbar = new Grid { Margin = new Thickness(18, 0, 12, 0) };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titlePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            titlePanel.Children.Add(new TextBlock
            {
                Text = "光谱预览",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold
            });
            plotSummaryText = new TextBlock
            {
                Text = "尚未选择曲线",
                FontSize = 12,
                Foreground = Theme.SecondaryTextBrush,
                Margin = new Thickness(12, 3, 0, 0)
            };
            titlePanel.Children.Add(plotSummaryText);
            toolbar.Children.Add(titlePanel);

            var reset = new Button
            {
                Content = "恢复视图",
                Style = (Style)FindResource("QuietButton"),
                ToolTip = "也可双击图表恢复"
            };
            reset.Click += delegate { plot.ResetView(); };
            Grid.SetColumn(reset, 1);
            toolbar.Children.Add(reset);
            root.Children.Add(toolbar);

            plot = new SpectrumPlot { Margin = new Thickness(8, 0, 8, 8) };
            Grid.SetRow(plot, 1);
            root.Children.Add(plot);
            return CreateCard(root, new Thickness(0, 0, 0, 0));
        }

        private UIElement BuildFooter()
        {
            var grid = new Grid { Margin = new Thickness(2, 8, 2, 0) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var moduleBadge = new Border
            {
                Background = Theme.SecondarySurfaceBrush,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 6, 12, 6),
                Child = new TextBlock
                {
                    Text = "当前：通用整理",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold
                }
            };
            moduleBadge.VerticalAlignment = VerticalAlignment.Center;
            moduleBadge.Margin = new Thickness(0, 3, 14, 3);
            var moduleActions = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            moduleActions.Children.Add(new TextBlock { Text = "快捷功能", FontSize = 13, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) });
            moduleActions.Children.Add(moduleBadge);
            var demoButton = new Button { Content = "模拟 pH 演示", Style = (Style)FindResource("QuickActionButton") };
            demoButton.Click += delegate
            {
                var demo = new PhAnalysisWindow(PhDemo.CreateBatch(), true) { Owner = this };
                demo.PrefillSyntheticDemo(); demo.Show();
            };
            moduleActions.Children.Add(demoButton);
            var openProject = new Button { Content = "打开工作项目", Style = (Style)FindResource("QuickActionButton") };
            openProject.Click += delegate { OpenWorkProject(); };
            moduleActions.Children.Add(openProject);
            var laboratory = new Button { Content = "实验室工具", Style = (Style)FindResource("QuickActionButton"),
                Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF2, 0xFF)), ToolTip = "文献阅读与离线翻译、小鼠管理" };
            laboratory.Click += delegate
            {
                var tools = OwnedWindows.OfType<LaboratoryModulesWindow>().FirstOrDefault();
                if (tools != null) tools.Activate(); else new LaboratoryModulesWindow { Owner = this }.Show();
            };
            moduleActions.Children.Add(laboratory);
            var actionCard = new Border { Background = Theme.SurfaceBrush, BorderBrush = Theme.DividerBrush, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 0, 4), Child = moduleActions };
            Grid.SetColumnSpan(actionCard, 3); grid.Children.Add(actionCard);

            statusText = new TextBlock
            {
                Text = "等待导入 ASC 文件夹",
                FontSize = 12,
                Foreground = Theme.SecondaryTextBrush,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(8, 0, 14, 0)
            };
            Grid.SetRow(statusText, 1);
            grid.Children.Add(statusText);

            openOutputButton = new Button
            {
                Content = "打开所在文件夹",
                Style = (Style)FindResource("QuietButton"),
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 0, 4, 0)
            };
            openOutputButton.Click += delegate
            {
                if (!string.IsNullOrWhiteSpace(lastOutputPath))
                {
                    Process.Start("explorer.exe", string.Format("/select,\"{0}\"", lastOutputPath));
                }
            };
            Grid.SetColumn(openOutputButton, 1); Grid.SetRow(openOutputButton, 1);
            grid.Children.Add(openOutputButton);

            logButton = new Button
            {
                Content = "处理日志",
                Style = (Style)FindResource("QuietButton"),
                IsEnabled = false
            };
            logButton.Click += delegate { ShowProcessingLog(); };
            Grid.SetColumn(logButton, 2); Grid.SetRow(logButton, 1);
            grid.Children.Add(logButton);
            return grid;
        }

        private static Border CreateCard(UIElement child, Thickness margin)
        {
            return new Border
            {
                Background = Theme.SurfaceBrush,
                CornerRadius = new CornerRadius(14),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(2),
                Margin = margin,
                Child = child,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 18,
                    ShadowDepth = 2,
                    Opacity = 0.07
                }
            };
        }

        private void ChooseFolderButtonClick(object sender, RoutedEventArgs eventArgs)
        {
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择存放 ASC 文件的文件夹";
                dialog.ShowNewFolderButton = false;
                if (currentBatch != null && Directory.Exists(currentBatch.SourceFolder))
                {
                    dialog.SelectedPath = currentBatch.SourceFolder;
                }

                if (dialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    LoadFolderAsync(dialog.SelectedPath);
                }
            }
        }

        private void ImportCardDragOver(object sender, DragEventArgs eventArgs)
        {
            var folder = GetDroppedFolder(eventArgs.Data);
            eventArgs.Effects = folder == null ? DragDropEffects.None : DragDropEffects.Copy;
            eventArgs.Handled = true;
            importCard.BorderBrush = folder == null ? Theme.ErrorBrush : Theme.AccentBrush;
        }

        private void ImportCardDrop(object sender, DragEventArgs eventArgs)
        {
            importCard.BorderBrush = Brushes.Transparent;
            var folder = GetDroppedFolder(eventArgs.Data);
            if (folder == null)
            {
                SetStatus("请拖入一个文件夹，而不是单个 ASC 文件。", Theme.WarningBrush);
                return;
            }

            LoadFolderAsync(folder);
        }

        private static string GetDroppedFolder(IDataObject data)
        {
            if (data == null || !data.GetDataPresent(DataFormats.FileDrop))
            {
                return null;
            }

            var paths = data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length != 1 || !Directory.Exists(paths[0]))
            {
                return null;
            }

            return paths[0];
        }

        private async void LoadFolderAsync(string folderPath)
        {
            if (isLoading || isExporting)
            {
                SetStatus("当前任务尚未完成，请稍候或取消导入。", Theme.WarningBrush);
                return;
            }

            if (phWindow != null)
            {
                if (!await phWindow.TryCloseDraftAsync()) return;
                phWindow.Close();
            }
            isLoading = true;
            importCancellation = new CancellationTokenSource();
            loadingWatch.Restart();
            SetLoadingState(folderPath, true);

            var progress = new Progress<ProcessingProgress>(delegate(ProcessingProgress value)
            {
                importProgress.Maximum = Math.Max(1, value.Total);
                importProgress.Value = value.Completed;
                progressText.Text = string.Format(
                    "正在读取 {0}/{1}：{2}",
                    value.Completed,
                    value.Total,
                    value.CurrentFile);
            });

            try
            {
                var batch = await Task.Run(delegate
                {
                    return processor.ProcessFolder(folderPath, progress, importCancellation.Token);
                });
                loadingWatch.Stop();
                ApplyBatch(batch, loadingWatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                loadingWatch.Stop();
                SetStatus("已取消本次导入，原有数据未改变。", Theme.WarningBrush);
                folderPathText.Text = currentBatch == null
                    ? "拖入包含 ASC 文件的文件夹"
                    : currentBatch.SourceFolder;
                importSummaryText.Text = currentBatch == null
                    ? "只读取当前文件夹，不扫描子文件夹"
                    : BuildImportSummary(currentBatch, TimeSpan.Zero, false);
            }
            catch (Exception exception)
            {
                loadingWatch.Stop();
                SetStatus("导入失败：" + exception.Message, Theme.ErrorBrush);
                MessageBox.Show(
                    this,
                    "无法读取该文件夹。\n\n" + exception.Message,
                    "导入失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                isLoading = false;
                SetLoadingState(folderPath, false);
                importCancellation.Dispose();
                importCancellation = null;
            }
        }

        private void SetLoadingState(string folderPath, bool loading)
        {
            chooseFolderButton.IsEnabled = !loading;
            phButton.IsEnabled = !loading && currentBatch != null && currentBatch.SuccessCount > 0;
            exportButton.IsEnabled = !loading && !isExporting && currentBatch != null && currentBatch.SuccessCount > 0;
            cancelButton.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            importProgress.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            progressText.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
            if (loading)
            {
                folderPathText.Text = folderPath;
                importSummaryText.Text = "正在后台解析，界面仍可操作";
                progressText.Text = "正在查找 ASC 文件…";
                importProgress.Value = 0;
                SetStatus("正在导入数据…", Theme.AccentBrush);
            }
        }

        private void ApplyBatch(SpectrumBatch batch, TimeSpan elapsed)
        {
            if (phWindow != null) { phWindow.Close(); if (phWindow != null && phWindow.IsVisible) return; }
            currentBatch = batch;
            phButton.IsEnabled = batch.SuccessCount > 0;
            fileRows.Clear();
            fileRowsPanel.Children.Clear();
            lastOutputPath = null;
            openOutputButton.Visibility = Visibility.Collapsed;
            folderPathText.Text = batch.SourceFolder;
            importSummaryText.Text = BuildImportSummary(batch, elapsed, true);
            fileCountText.Text = string.Format("{0} 份 · {1} 有效", batch.DiscoveredCount, batch.SuccessCount);
            exportButton.IsEnabled = batch.SuccessCount > 0;
            logButton.IsEnabled = true;

            var validIndex = 0;
            foreach (var result in batch.Files)
            {
                var color = new SolidColorBrush(SeriesColors[validIndex % SeriesColors.Length]);
                color.Freeze();
                var row = CreateFileRow(result, color);
                fileRows.Add(row);
                fileRowsPanel.Children.Add(row.Container);
                if (result.Status == ProcessingStatus.Success)
                {
                    row.CheckBox.IsChecked = validIndex < Math.Min(10, MaximumPreviewSeries);
                    validIndex++;
                }
            }

            ApplySearchFilter();
            ApplySelectedSeries();
            SetStatus(
                batch.SkippedCount == 0
                    ? string.Format("导入完成：{0} 份文件全部有效", batch.SuccessCount)
                    : string.Format("导入完成：{0} 份有效，{1} 份已跳过", batch.SuccessCount, batch.SkippedCount),
                batch.SkippedCount == 0 ? Theme.SuccessBrush : Theme.WarningBrush);
        }

        private static string BuildImportSummary(SpectrumBatch batch, TimeSpan elapsed, bool includeElapsed)
        {
            var elapsedPart = includeElapsed
                ? string.Format(" · {0:0.00} 秒", elapsed.TotalSeconds)
                : string.Empty;
            return string.Format(
                "{0} 份 ASC · {1} 份有效 · {2} 份跳过{3}",
                batch.DiscoveredCount,
                batch.SuccessCount,
                batch.SkippedCount,
                elapsedPart);
        }

        private FileRowView CreateFileRow(FileProcessingResult result, Brush color)
        {
            var grid = new Grid
            {
                Margin = new Thickness(4, 2, 4, 2),
                Background = Brushes.Transparent,
                ToolTip = result.Status == ProcessingStatus.Success
                    ? result.SourcePath
                    : result.FileName + "\n" + result.ErrorMessage
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var checkBox = new CheckBox
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = result.Status == ProcessingStatus.Success
            };
            checkBox.Checked += FileSelectionChanged;
            checkBox.Unchecked += FileSelectionChanged;
            grid.Children.Add(checkBox);

            var marker = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = result.Status == ProcessingStatus.Success ? color : Theme.ErrorBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(marker, 1);
            grid.Children.Add(marker);

            var namePanel = new StackPanel
            {
                Margin = new Thickness(7, 7, 8, 7)
            };
            namePanel.Children.Add(new TextBlock
            {
                Text = result.FileName,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = result.Status == ProcessingStatus.Success
                    ? Theme.PrimaryTextBrush
                    : Theme.SecondaryTextBrush
            });
            namePanel.Children.Add(new TextBlock
            {
                Text = result.Status == ProcessingStatus.Success
                    ? string.Format("{0:N0} 个点", result.Dataset.PointCount)
                    : result.ErrorMessage,
                FontSize = 10,
                Foreground = result.Status == ProcessingStatus.Success
                    ? Theme.SecondaryTextBrush
                    : Theme.ErrorBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(namePanel, 2);
            grid.Children.Add(namePanel);

            var status = new TextBlock
            {
                Text = result.Status == ProcessingStatus.Success ? "有效" : "跳过",
                Foreground = result.Status == ProcessingStatus.Success ? Theme.SuccessBrush : Theme.ErrorBrush,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(status, 3);
            grid.Children.Add(status);

            return new FileRowView(result, color, checkBox, grid);
        }

        private void FileSelectionChanged(object sender, RoutedEventArgs eventArgs)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null && checkBox.IsChecked == true)
            {
                var selected = fileRows.Count(row => row.CheckBox.IsChecked == true);
                if (selected > MaximumPreviewSeries)
                {
                    checkBox.IsChecked = false;
                    SetStatus(
                        string.Format("预览最多同时显示 {0} 条曲线；导出仍包含全部有效文件。", MaximumPreviewSeries),
                        Theme.WarningBrush);
                    return;
                }
            }

            SchedulePlotUpdate();
        }

        private void SchedulePlotUpdate()
        {
            if (plotUpdateTimer == null)
            {
                ApplySelectedSeries();
                return;
            }

            plotUpdateTimer.Stop();
            plotUpdateTimer.Start();
        }

        private void ApplySelectedSeries()
        {
            var selected = fileRows
                .Where(row => row.CheckBox.IsChecked == true && row.Result.Dataset != null)
                .Select(row => new PlotSeries(row.Result.Dataset, row.Color))
                .ToArray();
            plot.SetSeries(selected);
            plotSummaryText.Text = selected.Length == 0
                ? "尚未选择曲线"
                : string.Format("已显示 {0} 条", selected.Length);
            plotSummaryText.ToolTip = "滚轮缩放 · 拖动平移 · 悬停查看原始测量点";
        }

        private void SelectAllVisible()
        {
            var available = fileRows
                .Where(row => row.Container.Visibility == Visibility.Visible &&
                              row.Result.Status == ProcessingStatus.Success)
                .ToArray();
            var alreadySelectedOutside = fileRows.Count(
                row => row.Container.Visibility != Visibility.Visible && row.CheckBox.IsChecked == true);
            var remaining = Math.Max(0, MaximumPreviewSeries - alreadySelectedOutside);
            foreach (var row in available.Take(remaining))
            {
                row.CheckBox.IsChecked = true;
            }

            if (available.Length > remaining)
            {
                SetStatus(
                    string.Format("已选择前 {0} 条可见曲线；预览上限为 {1} 条。", remaining, MaximumPreviewSeries),
                    Theme.WarningBrush);
            }
        }

        private void ClearSelection()
        {
            foreach (var row in fileRows)
            {
                row.CheckBox.IsChecked = false;
            }
        }

        private void ApplySearchFilter()
        {
            var filter = searchBox == null ? string.Empty : searchBox.Text.Trim();
            foreach (var row in fileRows)
            {
                row.Container.Visibility = filter.Length == 0 ||
                                           row.Result.FileName.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private async void ExportButtonClick(object sender, RoutedEventArgs eventArgs)
        {
            if (currentBatch == null || currentBatch.SuccessCount == 0 || isExporting)
            {
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "导出 ASC 汇总数据",
                Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
                AddExtension = true,
                DefaultExt = ".xlsx",
                OverwritePrompt = true,
                FileName = BuildDefaultExportName(currentBatch.SourceFolder),
                InitialDirectory = Directory.Exists(currentBatch.SourceFolder)
                    ? currentBatch.SourceFolder
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog(this) != true)
            {
                SetStatus("已取消保存，没有创建文件。", Theme.SecondaryTextBrush);
                return;
            }

            isExporting = true;
            exportButton.IsEnabled = false;
            exportButton.Content = "正在导出…";
            chooseFolderButton.IsEnabled = false;
            openOutputButton.Visibility = Visibility.Collapsed;
            SetStatus("正在生成标准 xlsx 文件…", Theme.AccentBrush);

            try
            {
                var targetPath = dialog.FileName;
                await Task.Run(delegate { exporter.Export(currentBatch, targetPath, true); });
                lastOutputPath = targetPath;
                openOutputButton.Visibility = Visibility.Visible;
                SetStatus("导出完成：" + Path.GetFileName(targetPath), Theme.SuccessBrush);
            }
            catch (Exception exception)
            {
                SetStatus("导出失败：" + exception.Message, Theme.ErrorBrush);
                MessageBox.Show(
                    this,
                    "无法保存工作簿。\n\n" + exception.Message +
                    "\n\n请检查目标文件是否正在 Excel 或 WPS 中打开，并确认文件夹可写。",
                    "导出失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                isExporting = false;
                exportButton.Content = "导出 Excel";
                exportButton.IsEnabled = currentBatch != null && currentBatch.SuccessCount > 0;
                chooseFolderButton.IsEnabled = true;
            }
        }

        private static string BuildDefaultExportName(string sourceFolder)
        {
            var folderName = new DirectoryInfo(sourceFolder).Name;
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            {
                folderName = folderName.Replace(invalidCharacter, '_');
            }

            return string.Format(
                "{0}_ASC汇总_{1}.xlsx",
                folderName,
                DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture));
        }

        private void ShowProcessingLog()
        {
            if (currentBatch == null)
            {
                return;
            }

            var builder = new System.Text.StringBuilder();
            builder.AppendLine("源文件夹");
            builder.AppendLine(currentBatch.SourceFolder);
            builder.AppendLine();
            builder.AppendFormat(
                "发现 {0} 份 · 成功 {1} 份 · 跳过 {2} 份",
                currentBatch.DiscoveredCount,
                currentBatch.SuccessCount,
                currentBatch.SkippedCount);
            builder.AppendLine();
            builder.AppendLine();

            foreach (var result in currentBatch.Files.Where(file => file.Status == ProcessingStatus.Failed))
            {
                builder.AppendLine("• " + result.FileName);
                builder.AppendLine("  " + result.ErrorMessage);
            }

            if (currentBatch.SkippedCount == 0)
            {
                builder.AppendLine("全部文件均已成功读取。");
            }

            var logWindow = new Window
            {
                Title = "处理日志",
                Owner = this,
                Width = 720,
                Height = 500,
                MinWidth = 520,
                MinHeight = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new TextBox
                {
                    Text = builder.ToString(),
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    AcceptsReturn = true,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(20),
                    Padding = new Thickness(14),
                    Background = Theme.SurfaceBrush
                }
            };
            logWindow.Show();
        }

        private void SetStatus(string message, Brush color)
        {
            statusText.Text = message;
            statusText.Foreground = color;
        }

        private sealed class FileRowView
        {
            public FileRowView(
                FileProcessingResult result,
                Brush color,
                CheckBox checkBox,
                FrameworkElement container)
            {
                Result = result;
                Color = color;
                CheckBox = checkBox;
                Container = container;
            }

            public FileProcessingResult Result { get; private set; }
            public Brush Color { get; private set; }
            public CheckBox CheckBox { get; private set; }
            public FrameworkElement Container { get; private set; }
        }
    }
}
