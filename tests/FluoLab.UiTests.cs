using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Interop;
using FluoLab.Core;

namespace FluoLab.UiTests
{
    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            try
            {
                var assembly = Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FluoLab.exe"));
                var theme = assembly.GetType("FluoLab.App.Theme");
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown,
                    Resources = (ResourceDictionary)theme.GetMethod("CreateResources").Invoke(null, null) };
                var windowType = assembly.GetType("FluoLab.App.PhAnalysisWindow");
                var batch = PhDemo.CreateBatch();
                var window = (Window)Activator.CreateInstance(windowType, batch, true);
                var originalContent = window.Content;
                window.Left = -20000; window.Top = -20000; window.ShowInTaskbar = false; window.Show();
                Drain(window); CheckNativeFrame(window, originalContent);
                Call(window, "LoadSyntheticResultForQa", 2);
                Drain(window);
                var session = (PhAnalysisSession)Field(window, "session");
                var actions = (StackPanel)Field(window, "resultActions");
                Check(session.HasCurrentResult && actions.IsEnabled, "current result enables exports");
                var sourceRows = ((IEnumerable)Field(window, "rows")).Cast<object>().ToArray();
                ((TextBox)Field(window, "groupingSearch")).Text = "pH=10.4"; Drain(window);
                var groupingGrid = (DataGrid)Field(window, "groupingGrid");
                Check(groupingGrid.Items.Count == 2 && session.Samples.Count == 21, "filename filter changes display only");
                ((TextBox)Field(window, "groupingSearch")).Text = ""; Drain(window);
                var editedRows = session.Samples.Select(x => x.Copy()).ToArray();
                foreach (var editedRow in editedRows.Where(x => x.Series == "模拟条件 A")) editedRow.Series = "粘贴手动 A";
                var copied = GroupingTransfer.Copy(editedRows);
                Call(window, "ApplyGroupingText", copied); Drain(window);
                Check(session.Samples[0].Series == "粘贴手动 A" && !session.GroupingConfirmed && !actions.IsEnabled, "paste updates by filename and invalidates result");
                sourceRows[0].GetType().GetProperty("Series").SetValue(sourceRows[0], "", null); Drain(window);
                ((ComboBox)Field(window, "groupingFilter")).SelectedIndex = 2; Drain(window);
                Check(groupingGrid.Items.Count == 1, "problem filter selects only invalid row");
                Call(window, "FocusNextIssue"); Drain(window);
                Check(groupingGrid.CurrentColumn.SortMemberPath == "Series", "problem action focuses missing series cell");
                groupingGrid.CancelEdit(); groupingGrid.CancelEdit(DataGridEditingUnit.Row);
                ((ComboBox)Field(window, "groupingFilter")).SelectedIndex = 0; Drain(window);
                windowType.GetProperty("CloseDecision", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, new Func<MessageBoxResult>(() => MessageBoxResult.Cancel), null);
                window.Close(); Drain(window);
                Check(window.IsVisible && (bool)Field(window, "dirty"), "cancel dirty close preserves draft and window");
                Call(window, "LoadSyntheticResultForQa", 2); Drain(window);
                var chart = Field(window, "chart");
                var hits = ((IEnumerable)Field(chart, "measurementHits")).Cast<object>().ToArray();
                Check(hits.Length == 19, "response hit map includes every raw extracted point");
                var point = (Point)hits[0].GetType().GetField("Position").GetValue(hits[0]);
                var matching = (Array)Call(chart, "HitMeasurements", point);
                Check(matching.Length >= 1, "response hit test reaches exact original point");
                var capturePath = Path.Combine(Path.GetTempPath(), "synthetic-ui-work.fluolab");
                var stored = WorkProjectStore.Capture(batch, session, capturePath, true);
                var resumed = WorkProjectStore.Restore(stored, batch);
                Call(window, "RestoreDraft", resumed, capturePath); Drain(window);
                Check(!session.GroupingConfirmed && !actions.IsEnabled && !(bool)Field(window, "dirty"), "restored UI draft needs confirmation and has no cached result");
                Call(window, "LoadSyntheticResultForQa", 2); Drain(window);
                Call(window, "OpenTrace", ResultFirstName(batch)); Drain(window);
                var trace = app.Windows.Cast<Window>().Single(x => x.Title == "源文件溯源");
                Check(Find<FrameworkElement>(trace).Any(x => x.GetType().Name == "SpectrumPlot"), "trace window reuses raw spectrum renderer"); trace.Close();
                foreach (var accept in new[] { false, true })
                {
                    window.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(delegate
                    {
                        var dialog = app.Windows.Cast<Window>().Single(x => x.Title == "确认缺少波长文件的处理");
                        var content = Find<TextBox>(dialog).Single();
                        Check(content.Text.Contains("Missing-A.ASC") && content.Text.Contains("Missing-B.ASC"), "complete missing list displayed");
                        var button = Find<Button>(dialog).Single(x => x.Content.ToString().StartsWith(accept ? "确认仅使用" : "取消", StringComparison.Ordinal));
                        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }));
                    var accepted = (bool)Call(window, "ConfirmMissing", (object)new[] { "Missing-A.ASC", "Missing-B.ASC" });
                    Check(accepted == accept, "missing-file dialog respects explicit accept/cancel");
                }
                var row = ((IEnumerable)Field(window, "rows")).Cast<object>().First();
                row.GetType().GetProperty("Series").SetValue(row, "手动编辑系列", null);
                Check(!session.GroupingConfirmed && !session.HasCurrentResult && !actions.IsEnabled, "metadata binding invalidates result and export");
                Check(((CheckBox)Field(window, "affirmation")).IsChecked == false, "metadata edits clear explicit confirmation");
                Call(window, "LoadSyntheticResultForQa", 2);
                ((TextBox)Field(window, "wavelength")).Text = "610";
                Check(!session.HasCurrentResult && !actions.IsEnabled, "wavelength text event invalidates exports");
                Call(window, "LoadSyntheticResultForQa", 2);
                ((TextBox)Field(window, "unit")).Text = "已确认单位";
                Check(!session.HasCurrentResult && !actions.IsEnabled, "unit text event invalidates chart and exports");
                Call(window, "LoadSyntheticResultForQa", 2);
                row.GetType().GetProperty("Included").SetValue(row, false, null);
                Check(!session.HasCurrentResult && !actions.IsEnabled, "analysis inclusion independent from preview and revokes result");
                Call(window, "ClearDirtyForQa"); window.Close();
                var mainType = assembly.GetType("FluoLab.App.MainWindow");
                var main = (Window)Activator.CreateInstance(mainType);
                var oldAnalysis = (Window)Activator.CreateInstance(windowType, batch, true);
                oldAnalysis.Left = -20000; oldAnalysis.ShowInTaskbar = false; oldAnalysis.Show();
                SetField(main, "phWindow", oldAnalysis);
                var closed = false; oldAnalysis.Closed += delegate { closed = true; };
                Call(main, "ApplyBatch", new SpectrumBatch(batch.SourceFolder, DateTimeOffset.Now, new FileProcessingResult[0]), TimeSpan.Zero);
                Check(closed, "batch replacement closes old analysis window");
                Check(!((Button)Field(main, "phButton")).IsEnabled, "empty batch disables analysis entry");
                main.Close();
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                RunModuleChecks(assembly);
                app.Shutdown();
                Console.WriteLine("PASS: WPF metadata/inclusion/wavelength/unit edits invalidate exports; new batch closes old analysis.");
                return 0;
            }
            catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        }
        private static void RunModuleChecks(Assembly assembly)
        {
            var fit = assembly.GetType("FluoLab.App.WindowPlacement").GetMethod("Fit", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (var work in new[] { new Rect(0,0,1536,832), new Rect(0,0,1280,680), new Rect(-1280,0,1280,960) })
            {
                var fitted = (Rect)fit.Invoke(null, new object[] { new Rect(work.Left - 100,-53,1440,900), work });
                Check(work.Contains(fitted) && fitted.Top >= work.Top + 8 && fitted.Left >= work.Left + 8,
                    "oversized default window fits monitor work area including negative origins and scaled desktop");
            }
            var root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
            var type = assembly.GetType("FluoLab.App.LiteratureWindow");
            var paper = (Window)Activator.CreateInstance(type);
            var component = Path.Combine(root, "artifacts", "literature-component-v1");
            Call(paper, "UseComponentForQa", component);
            paper.Left = -20000; paper.Top = -20000; paper.ShowInTaskbar = false; paper.Show(); Drain(paper);
            CheckNativeFrame(paper, paper.Content);
            paper.WindowState = WindowState.Minimized;
            Check(paper.WindowState == WindowState.Minimized, "minimize action"); paper.WindowState = WindowState.Normal;
            paper.WindowState = WindowState.Maximized; Check(paper.WindowState == WindowState.Maximized, "maximize action");
            paper.WindowState = WindowState.Normal; Check(paper.WindowState == WindowState.Normal, "restore action");
            var fixedPaper = (Window)Activator.CreateInstance(type);
            Call(fixedPaper, "LoadFixedDemoForQa", ""); fixedPaper.ShowInTaskbar = false; fixedPaper.Left = -20000; fixedPaper.Show(); Drain(fixedPaper);
            var fixedDocument = (LiteratureDocument)Field(fixedPaper, "document");
            var machineText = fixedDocument.Pages[0].ChineseText;
            ((TextBox)Field(fixedPaper, "reviewDraft")).Text = "模拟手动修改 600 nm";
            Check(((ComboBox)Field(fixedPaper, "reviewState")).SelectedIndex == 0 && (bool)Field(fixedPaper, "dirty"), "editing review revokes state and protects unsaved content");
            ((ComboBox)Field(fixedPaper, "reviewState")).SelectedIndex = 1;
            ((Button)Field(fixedPaper, "saveReview")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Drain(fixedPaper);
            Check(fixedDocument.Pages[0].ChineseText == machineText && fixedDocument.Pages[0].Review.Draft == "模拟手动修改 600 nm" && fixedDocument.Pages[0].Review.State == LiteratureReviewState.Reviewed, "WPF explicit review save is separate from original machine text");
            ((TextBox)Field(fixedPaper, "termNotes")).Text = "模拟术语备注";
            Check(fixedDocument.TermNotes == "模拟术语备注", "terminology notes bind to current document");
            SetField(fixedPaper, "busy", true); Call(fixedPaper, "SetControls");
            Check(!((TextBox)Field(fixedPaper, "reviewDraft")).IsEnabled && !((Button)Field(fixedPaper, "saveReview")).IsEnabled, "review disabled during tasks");
            SetField(fixedPaper, "busy", false); fixedPaper.Close();
            var summary = (Window)assembly.GetType("FluoLab.App.BatchCompletenessWindow").GetMethod("FixedDemo", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            summary.ShowInTaskbar = false; summary.Left = -20000; summary.Show(); Drain(summary);
            var summaryGrid = (DataGrid)Field(summary, "grid"); var count = summaryGrid.Items.Count;
            ((TextBox)Field(summary, "search")).Text = "模拟异常文件"; Drain(summary);
            Check(count > 1 && summaryGrid.Items.Count == 1, "completeness filter limits view only"); summary.Close();
            if (Directory.Exists(component))
            {
                WaitTask(paper, (System.Threading.Tasks.Task)Call(paper, "ImportPath", Path.Combine(root, "outputs", "v1.3-synthetic-literature", "Synthetic-English-Article.pdf")));
                var document = (LiteratureDocument)Field(paper, "document");
                Check(document != null && document.Pages.Count == 2 && ((Image)Field(paper, "pageImage")).Source != null, "actual PDF extraction and rendering in WPF");
                WaitTask(paper, (System.Threading.Tasks.Task)Call(paper, "TranslatePages", true));
                document = (LiteratureDocument)Field(paper, "document");
                Console.WriteLine("INFO: " + ((TextBlock)Field(paper, "status")).Text);
                Check(document.Pages.All(x => !string.IsNullOrWhiteSpace(x.ChineseText)), "actual offline translation of both pages");
                var originalMachine = document.Pages[0].ChineseText;
                ((TextBox)Field(paper, "reviewDraft")).Text = "【模拟核对稿，非真实论文】" + originalMachine;
                ((ComboBox)Field(paper, "reviewState")).SelectedIndex = 2;
                ((Button)Field(paper, "saveReview")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ((TextBox)Field(paper, "termNotes")).Text = "【模拟备注】fluorescence → 荧光（需人工核对语境）";
                WaitTask(paper, (System.Threading.Tasks.Task)Call(paper, "TranslatePages", false));
                document = (LiteratureDocument)Field(paper, "document");
                Check(document.Pages[0].ChineseText == originalMachine && document.Pages[0].Review.Draft.StartsWith("【模拟核对稿", StringComparison.Ordinal) && document.Pages[0].Review.State == LiteratureReviewState.Questionable && document.TermNotes.Contains("fluorescence"), "actual worker checkpoint preserves review, history and terms without changing original machine text");
                var output = Path.Combine(root, "outputs", "v1.3-synthetic-literature");
                File.WriteAllText(Path.Combine(output, "Synthetic-Bilingual.html"), LiteratureReport.Html(document), new System.Text.UTF8Encoding(true));
                File.WriteAllText(Path.Combine(output, "Synthetic-Bilingual.txt"), LiteratureReport.Text(document), new System.Text.UTF8Encoding(true));
                Call(paper, "Navigate", 1);
                while ((bool)Field(paper, "busy")) { Drain(paper); Thread.Sleep(20); }
                Check(((TextBox)Field(paper, "original")).Text.Contains("SYNTHETIC ARTICLE - PAGE 2"), "second-page navigation updates source text");
                var renderer = assembly.GetType("FluoLab.App.Program").GetMethod("RenderModuleScreenshot", BindingFlags.Static | BindingFlags.NonPublic);
                renderer.Invoke(null, new object[] { paper, Path.Combine(root, "qa", "v1.3", "literature-actual.png"), 1400, 900, 1.0 });
                SetField(paper, "busy", true); paper.Close(); Check(paper.IsVisible, "active worker prevents unsafe close"); SetField(paper, "busy", false);
                Console.WriteLine("PASS: actual offline PDF extraction/inference/navigation and bilingual export in WPF.");
            }
            else Console.WriteLine("SKIP: optional literature component not built; actual inference UI checks not run.");
            paper.Close();
            Check(!paper.IsVisible, "close caption uses normal close lifecycle");
            var main = (Window)Activator.CreateInstance(assembly.GetType("FluoLab.App.MainWindow"));
            main.ShowInTaskbar = true; main.Show(); Drain(main);
            var handle = new WindowInteropHelper(main).Handle;
            NativeRect actualBounds; Check(GetWindowRect(handle, out actualBounds), "read own native bounds");
            var workArea = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
            Check(actualBounds.Left >= workArea.Left && actualBounds.Top >= workArea.Top && actualBounds.Right <= workArea.Right && actualBounds.Bottom <= workArea.Bottom,
                "default main window including native caption fits the current monitor at actual process DPI");
            Find<Button>(main).Single(x => object.Equals(x.Content, "实验室工具")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Drain(main);
            var modules = main.OwnedWindows.Cast<Window>().Single(x => x.Title == "FluoLab 实验室工具");
            CheckNativeFrame(main, main.Content); CheckNativeFrame(modules, modules.Content);
            foreach (var label in new[] { "模拟 pH 演示", "打开工作项目", "实验室工具" })
            {
                var action = Find<Button>(main).Single(x => object.Equals(x.Content, label));
                Check(action.MinHeight >= 44 && action.FontSize >= 14 && action.BorderThickness.Left > 0, "footer action remains visibly styled: " + label);
            }
            Find<Button>(modules).Single(x => object.Equals(x.Content, "打开文献阅读")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Drain(modules);
            var ownedPaper = modules.OwnedWindows.Cast<Window>().Single();
            SetField(ownedPaper, "busy", true); main.Close(); Drain(main);
            Check(main.IsVisible && modules.IsVisible && ownedPaper.IsVisible, "main exit cannot bypass active literature close guard");
            SetField(ownedPaper, "busy", false); main.Close();
            Check(!main.IsVisible && !ownedPaper.IsVisible, "idle owned tools close normally");
            Console.WriteLine("PASS: caption actions and owned-tool close protection.");
        }
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr window, int index);
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);
        private static void CheckNativeFrame(Window window, object expectedContent)
        {
            var style = GetWindowLong(new WindowInteropHelper(window).Handle, -16);
            const int required = 0x00C00000 | 0x00080000 | 0x00040000 | 0x00020000 | 0x00010000;
            Check(window.WindowStyle == WindowStyle.SingleBorderWindow && window.ResizeMode == ResizeMode.CanResize && (style & required) == required,
                "native visible caption, system menu, minimize/maximize and resizable frame are required");
            Check(object.ReferenceEquals(expectedContent, window.Content), "showing a window must not replace its content with late custom chrome");
            Check(System.Windows.Shell.WindowChrome.GetWindowChrome(window) == null, "no custom non-client frame offsets");
        }
        private static void WaitTask(Window window, System.Threading.Tasks.Task task)
        {
            var deadline = DateTime.UtcNow.AddMinutes(3);
            while (!task.IsCompleted && DateTime.UtcNow < deadline) { Drain(window); Thread.Sleep(20); }
            Check(task.IsCompleted, "background operation completed within test limit"); task.GetAwaiter().GetResult(); Drain(window);
        }
        private static object Field(object instance, string name) { return instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance); }
        private static string ResultFirstName(SpectrumBatch batch) { return Path.GetFileName(batch.ValidDatasets[0].SourcePath); }
        private static void SetField(object instance, string name, object value) { instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, value); }
        private static object Call(object instance, string name, params object[] args) { return instance.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Invoke(instance, args); }
        private static System.Collections.Generic.IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T) yield return (T)child;
                foreach (var nested in Find<T>(child)) yield return nested;
            }
        }
        private static void Drain(Window window) { window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { })); window.UpdateLayout(); }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
