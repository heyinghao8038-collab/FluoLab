using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FluoLab.Core;
using FluoLab.Export;

namespace FluoLab.Tests
{
    internal static class FinalsTests
    {
        internal static void Run(string root)
        {
            var folder = Path.Combine(root, "模拟覆盖批次");
            var a = new SpectrumDataset("A", Path.Combine(folder, "A.ASC"), new[] { 490.0, 600.0, 800.0 }, new[] { -1.0, 2.0, 3.0 });
            var b = new SpectrumDataset("B", Path.Combine(folder, "B.ASC"), new[] { 490.0, 600.00001, 700.0 }, new[] { 4.0, 5.0, 6.0 });
            var values = a.Intensities.ToArray();
            var batch = new SpectrumBatch(folder, DateTimeOffset.Now, new[] { FileProcessingResult.Succeeded(a.SourcePath, a), FileProcessingResult.Succeeded(b.SourcePath, b), FileProcessingResult.Failed(Path.Combine(folder, "Bad.ASC"), "缺少 #DATA") });
            var report = BatchCompleteness.Create(batch, CancellationToken.None);
            Check(report.ValidCount == 2 && report.Union.Length == 5 && report.Intersection.SequenceEqual(new[] { 490.0 }), "exact union/intersection, near wavelengths separate");
            Check(report.TotalMeasurementCount == 6L && !report.CoverageConsistent && report.Files[2].FailureCategory == "缺少 #DATA", "dashboard totals, unequal coverage and failure category");
            Check(report.Files[0].MissingCount == 2 && report.Files[2].PointCount == null && report.Files[2].MissingCount == null, "missing counts and failed nullable cells");
            Check(a.Intensities.SequenceEqual(values), "negative/raw measurements unchanged");
            Check(!BatchCompleteness.Text(report).Contains(folder), "completeness export omits absolute source path");
            var empty = BatchCompleteness.Create(new SpectrumBatch(folder, DateTimeOffset.Now, new FileProcessingResult[0]), CancellationToken.None);
            Check(empty.Union.Length == 0 && empty.Intersection.Length == 0 && empty.Files.Length == 0, "empty completeness");
            Check(empty.TotalMeasurementCount == 0L && !empty.CoverageConsistent, "empty batch is not complete coverage");
            var failed = BatchCompleteness.Create(new SpectrumBatch(folder, DateTimeOffset.Now, new[] { batch.Files[2] }), CancellationToken.None);
            Check(failed.ValidCount == 0 && failed.Union.Length == 0, "all-failed completeness");
            Check(!failed.CoverageConsistent && failed.TotalMeasurementCount == 0, "all-failed has no invented measurements");
            var equal = BatchCompleteness.Create(new SpectrumBatch(folder, DateTimeOffset.Now, new[] { batch.Files[0], batch.Files[0] }), CancellationToken.None);
            Check(equal.CoverageConsistent && equal.TotalMeasurementCount == 6, "matching full wavelength sets");
            Check(BatchCompleteness.ClassifyFailure("#DATA 后没有可用数据。") == "空数据" && BatchCompleteness.ClassifyFailure("第 8 行出现重复波长 600。") == "重复波长" && BatchCompleteness.ClassifyFailure("第 2 行不是两列数值。") == "数据行格式错误" && BatchCompleteness.ClassifyFailure("文件正被占用或读取失败。") == "文件无法读取" && BatchCompleteness.ClassifyFailure(null) == "其他解析失败", "limited failure categories preserve unknown reasons");
            var noCommon = BatchCompleteness.Create(new SpectrumBatch(folder, DateTimeOffset.Now, new[] { FileProcessingResult.Succeeded(a.SourcePath, a), FileProcessingResult.Succeeded(b.SourcePath, new SpectrumDataset("B", b.SourcePath, new[] { 550.0, 650.0 }, new[] { 1.0, 2.0 })) }), CancellationToken.None);
            Check(noCommon.Intersection.Length == 0, "overlapping ranges are not common points");
            var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Throws<OperationCanceledException>(() => BatchCompleteness.Create(batch, cancelled.Token), "cancellable completeness");

            var page = new LiteraturePage { Number = 1, OriginalText = "Source 600 nm", ChineseText = "<script>machine</script> 600 nm", Status = "已翻译" };
            var document = new LiteratureDocument { FileName = "Synthetic.pdf", Pages = new List<LiteraturePage> { page }, TermNotes = "probe < & > 探针" };
            LiteratureReview.Save(page, "人工稿 < & > 600 nm", LiteratureReviewState.Reviewed, DateTimeOffset.UtcNow);
            Check(page.ChineseText.StartsWith("<script>", StringComparison.Ordinal) && page.Review.State == LiteratureReviewState.Reviewed && page.Review.History.Count == 1, "human review never overwrites machine output");
            LiteratureReview.Save(page, page.Review.Draft, LiteratureReviewState.Reviewed, DateTimeOffset.UtcNow);
            Check(page.Review.History.Count == 1, "unchanged save does not invent revision");
            page.ChineseText = "New machine output";
            LiteratureReview.Refresh(document);
            Check(LiteratureReview.IsStale(page) && page.Review.State == LiteratureReviewState.Unreviewed && page.Review.History[0].MachineText.Contains("<script>"), "changed machine revokes review and retains history");
            Check(LiteratureReport.Html(document).Contains("旧核对状态失效"), "stale draft clearly labeled in export");
            LiteratureReview.Save(page, "Second human draft", LiteratureReviewState.Questionable, DateTimeOffset.UtcNow);
            Check(!LiteratureReview.IsStale(page) && page.Review.History.Count == 2 && page.Review.History[0].Draft.Contains("人工稿"), "re-review binds new output and preserves prior revisions");
            var html = LiteratureReport.Html(document);
            Check(!html.Contains("<script>") && html.Contains("&lt;script&gt;") && html.Contains("probe &lt; &amp; &gt;") && html.Contains("Second human draft"), "all review history/terminology escaped and exported");
            Check(LiteratureReport.Text(document).Contains("New machine output") && LiteratureReport.Text(document).Contains("有疑问"), "text report audit trail");
            var json = Path.Combine(root, "review.json"); LiteratureWorker.WriteDocument(json, document);
            var restored = LiteratureWorker.ReadDocument(json);
            Check(restored.TermNotes == document.TermNotes && restored.Pages[0].Review.History.Count == 2 && restored.Pages[0].Review.Draft == "Second human draft", "review JSON roundtrip");
            var before = File.ReadAllBytes(json);
            using (var locked = File.Open(json, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Throws<IOException>(() => LiteratureWorker.WriteDocument(json, document), "locked checkpoint fails safely");
            Check(File.ReadAllBytes(json).SequenceEqual(before), "locked checkpoint retains prior content");
            File.WriteAllText(json, "{\"Pages\":[{\"Number\":1,\"OriginalText\":\"old\",\"ChineseText\":\"\"}]}");
            Check(LiteratureWorker.ReadDocument(json).Pages[0].Review == null, "legacy JSON without review remains readable");
            Throws<InvalidOperationException>(() => LiteratureReview.Save(new LiteraturePage(), "text", LiteratureReviewState.Reviewed, DateTimeOffset.Now), "untranslated page cannot be reviewed");
            Throws<InvalidOperationException>(() => LiteratureReview.Save(page, new string('x', 100001), LiteratureReviewState.Unreviewed, DateTimeOffset.Now), "bounded review draft");
            Throws<InvalidOperationException>(() => LiteratureReview.Save(page, "x", (LiteratureReviewState)20, DateTimeOffset.Now), "invalid review state rejected");
            var demoFolder = Path.Combine(root, "portable-synthetic", "ASC"); PhDemo.WriteAscFiles(demoFolder);
            var demoBatch = PhDemo.ReadAscFiles(demoFolder); var demoSession = PhDemo.CreateConfirmedSession(demoBatch);
            var demoProjectPath = Path.Combine(root, "portable-synthetic", "Synthetic.fluolab");
            var demoProject = WorkProjectStore.Capture(demoBatch, demoSession, demoProjectPath, true);
            Check(demoProject.Synthetic && WorkProjectStore.ResolveFolder(demoProject, demoProjectPath).TrimEnd(Path.DirectorySeparatorChar) == demoFolder, "file-backed synthetic project retains relative ASC location");
            var demoRestored = WorkProjectStore.Restore(demoProject, demoBatch);
            Check(!demoRestored.GroupingConfirmed && !demoRestored.HasCurrentResult && !demoRestored.Parameters.AllowMissing, "packaged demo restores no consents or results");
            File.AppendAllText(Path.Combine(demoFolder, demoBatch.Files.First(file => file.Dataset != null).FileName), "900 99\n");
            Throws<InvalidOperationException>(() => PhDemo.ReadAscFiles(demoFolder), "modified demo inputs cannot be republished as canonical synthetic data");
            Console.WriteLine("PASS: batch completeness exact coverage, nulls/cancellation; separate human review, stale invalidation, revision audit, legacy JSON and safe export/checkpoint.");
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Throws<T>(Action action, string message) where T : Exception
        { try { action(); } catch (T) { return; } throw new InvalidOperationException(message); }
    }
}
