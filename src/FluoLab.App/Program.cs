using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FluoLab.Core;
using FluoLab.Export;

[assembly: System.Reflection.AssemblyVersion("1.3.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.3.1.0")]

namespace FluoLab.App
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try { return RunApplication(args); }
            catch (Exception exception)
            {
                if (args.Length > 1 && args[0].Contains("screenshot"))
                    File.WriteAllText(args[1] + ".error.txt", exception.ToString());
                else if (args.Length > 0 && args[0].StartsWith("--ph-", StringComparison.Ordinal) && args[0] != "--ph-demo")
                    Console.Error.WriteLine(exception.Message);
                else MessageBox.Show("操作失败：" + exception.Message + "\n请关闭占用文件或重新启动后重试。", "FluoLab", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }

        private static int RunApplication(string[] args)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
            var application = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown,
                Resources = Theme.CreateResources()
            };

            if (args.Length > 0 && args[0].StartsWith("--ph-", StringComparison.Ordinal))
                return RunPhMode(application, args);
            if (args.Length == 1 && args[0] == "--mouse-host")
            {
                LaboratoryModulesWindow.EnsureMouseServer();
                var lifetime = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
                lifetime.Tick += delegate { application.Shutdown(); }; lifetime.Start();
                return application.Run();
            }
            if (args.Length >= 5 && (args[0] == "--literature-screenshot" || args[0] == "--modules-screenshot" || args[0] == "--main-layout-screenshot"))
            {
                Window preview;
                if (args[0] == "--literature-screenshot")
                {
                    var literature = new LiteratureWindow();
                    literature.LoadFixedDemoForQa(args.Length > 5 ? args[5] : ""); preview = literature;
                }
                else if (args[0] == "--main-layout-screenshot") preview = new MainWindow();
                else preview = new LaboratoryModulesWindow();
                RenderModuleScreenshot(preview, args[1], int.Parse(args[2], CultureInfo.InvariantCulture), int.Parse(args[3], CultureInfo.InvariantCulture), double.Parse(args[4], CultureInfo.InvariantCulture));
                preview.Close(); application.Shutdown(); return 0;
            }
            var window = new MainWindow();
            if (args.Length > 0 && string.Equals(args[0], "--screenshot", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    return 1;
                }

                var width = args.Length > 2 ? ParseDimension(args[2], 1440) : 1440;
                var height = args.Length > 3 ? ParseDimension(args[3], 900) : 900;
                RenderScreenshot(application, window, Path.GetFullPath(args[1]), width, height);
                return 0;
            }

            application.ShutdownMode = ShutdownMode.OnMainWindowClose;
            application.MainWindow = window;
            window.Show();
            if (args.Length > 0 && string.Equals(args[0], "--demo", StringComparison.OrdinalIgnoreCase))
            {
                window.LoadDemoData();
            }
            else if (args.Length > 0 && Directory.Exists(args[0]))
            {
                window.LoadFolderForAutomation(args[0]);
            }

            return application.Run();
        }

        private static int RunPhMode(Application application, string[] args)
        {
            var batch = PhDemo.CreateBatch();
            if (args.Length > 6 && args[6] == "long") batch = PhDemo.CreateLongNameBatch();
            if (args.Length > 6 && args[6] == "empty") batch = new SpectrumBatch(batch.SourceFolder, DateTimeOffset.Now, new FileProcessingResult[0]);
            if (args[0] == "--ph-demo-export" && (args.Length == 2 || (args.Length == 3 && args[2] == "--overwrite")))
            {
                var folder = Path.GetFullPath(args[1]); Directory.CreateDirectory(folder);
                var session = PhDemo.CreateConfirmedSession(batch);
                var result = session.Calculate(CancellationToken.None);
                var overwrite = args.Length == 3;
                new WorkbookExporter().ExportAnalysis(result, Path.Combine(folder, "Synthetic-pH-analysis.xlsx"), overwrite);
                var chart = new PhResponseChart(result, true);
                chart.ExportPng(Path.Combine(folder, "Synthetic-pH-response.png"), overwrite);
                chart.ExportSvg(Path.Combine(folder, "Synthetic-pH-response.svg"), overwrite);
                var sourceFolder = Path.Combine(folder, "ASC");
                if (!Directory.Exists(sourceFolder)) PhDemo.WriteAscFiles(sourceFolder);
                var projectPath = Path.Combine(folder, "Synthetic-pH-work.fluolab");
                var project = WorkProjectStore.Capture(batch, session, projectPath, true);
                AtomicOutput.Write(projectPath, overwrite, p => File.WriteAllBytes(p, WorkProjectStore.Serialize(project)));
                AnalysisBundleExporter.Export(result, batch, Path.Combine(folder, "Synthetic-pH-bundle.zip"), overwrite, false, true,
                    delegate(string png, string svg) { chart.ExportPng(png, false); chart.ExportSvg(svg, false); });
                application.Shutdown(); return 0;
            }
            var window = new PhAnalysisWindow(batch, true);
            if (args[0] == "--ph-screenshot" && args.Length >= 5)
            {
                var width = int.Parse(args[2], CultureInfo.InvariantCulture);
                var height = int.Parse(args[3], CultureInfo.InvariantCulture);
                var scale = double.Parse(args[4], CultureInfo.InvariantCulture);
                var step = args.Length > 5 ? int.Parse(args[5], CultureInfo.InvariantCulture) : 2;
                window.Width = width / scale; window.Height = height / scale;
                window.Left = -20000; window.Top = -20000; window.ShowInTaskbar = false;
                window.Show();
                if (args.Length > 6) window.LoadSyntheticStateForQa(args[6]);
                else if (step >= 0) window.LoadSyntheticResultForQa(step);
                window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { }));
                window.UpdateLayout();
                var visual = (FrameworkElement)window.Content;
                window.Content = null;
                var frame = new System.Windows.Controls.Border { Background = Theme.BackgroundBrush, Child = visual,
                    Width = width / scale, Height = height / scale };
                frame.Measure(new Size(width / scale, height / scale));
                frame.Arrange(new Rect(0, 0, width / scale, height / scale));
                frame.UpdateLayout();
                window.Dispatcher.Invoke(DispatcherPriority.Render, new Action(delegate { }));
                var bitmap = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                bitmap.Render(frame);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                var target = Path.GetFullPath(args[1]); Directory.CreateDirectory(Path.GetDirectoryName(target));
                using (var stream = File.Create(target)) encoder.Save(stream);
                window.ClearDirtyForQa(); window.Close(); application.Shutdown(); return 0;
            }
            if (args[0] != "--ph-demo") return 1;
            window.PrefillSyntheticDemo();
            application.MainWindow = window; application.ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show(); return application.Run();
        }

        private static int ParseDimension(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed >= 680
                ? parsed
                : fallback;
        }

        internal static void RenderModuleScreenshot(Window window, string path, int width, int height, double scale)
        {
            window.Width = width / scale; window.Height = height / scale; window.MinWidth = 0; window.MinHeight = 0;
            window.Left = -20000; window.Top = -20000; window.ShowInTaskbar = false; window.Show();
            window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { })); window.UpdateLayout();
            var visual = (FrameworkElement)window.Content; window.Content = null;
            var frame = new System.Windows.Controls.Border { Background = Theme.BackgroundBrush, Child = visual, Width = width / scale, Height = height / scale };
            frame.Measure(new Size(width / scale, height / scale)); frame.Arrange(new Rect(0, 0, width / scale, height / scale)); frame.UpdateLayout();
            var bitmap = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32); bitmap.Render(frame);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using (var output = File.Create(path)) encoder.Save(output);
        }

        private static void RenderScreenshot(
            Application application,
            MainWindow window,
            string outputPath,
            int width,
            int height)
        {
            application.MainWindow = window;
            window.Width = width;
            window.Height = height;
            window.Left = -20000;
            window.Top = -20000;
            window.ShowInTaskbar = false;
            window.Show();
            window.LoadDemoData();
            window.Dispatcher.Invoke(
                DispatcherPriority.ApplicationIdle,
                new Action(delegate { }));
            window.UpdateLayout();

            var dpi = VisualTreeHelper.GetDpi(window);
            var pixelWidth = Math.Max(1, (int)Math.Round(window.ActualWidth * dpi.DpiScaleX));
            var pixelHeight = Math.Max(1, (int)Math.Round(window.ActualHeight * dpi.DpiScaleY));
            var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            bitmap.Render(window);

            var outputFolder = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                encoder.Save(stream);
            }

            window.Close();
            application.Shutdown();
        }
    }
}

