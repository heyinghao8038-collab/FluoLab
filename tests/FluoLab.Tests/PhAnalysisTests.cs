using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Xml;
using FluoLab.Core;
using FluoLab.Export;

namespace FluoLab.Tests
{
    internal static class PhAnalysisTests
    {
        public static void Run(string temporary)
        {
            WorkflowTests.Run(temporary);
            SuggestionTests(temporary);
            var files = new List<FileProcessingResult>();
            foreach (var i in new[] { 1, 2, 3 }) files.Add(File(temporary, "Sample-pH=2.4-" + i, new[] { 600.0, 610.0 }, new[] { (double)i, -i * 2.0 }));
            files.Add(File(temporary, "Single-pH=2.40-1", new[] { 600.0 }, new[] { -4.0 }));
            files.Add(File(temporary, "Missing-pH=7.4-1", new[] { 600.0000001 }, new[] { 99.0 }));
            files.Add(File(temporary, "Excluded-1", new[] { 600.0 }, new[] { 900.0 }));
            var batch = new SpectrumBatch(temporary, DateTimeOffset.Now, files);
            var session = new PhAnalysisSession(batch);
            for (var i = 0; i < 3; i++) { session.Samples[i].Series = "系列 A"; session.Samples[i].PhText = i == 1 ? "2.40" : "2.4"; }
            session.Samples[3].Series = "系列 B";
            session.Samples[4].Series = "系列 A";
            session.Samples[5].Included = false; session.Samples[5].ExclusionReason = "测试：明确人工排除";
            Check(session.ConfirmGrouping().Length == 0, "valid metadata confirmation");
            session.Parameters.WavelengthText = "600";
            var preview = session.Preview();
            Check(preview.MatchedCount == 4 && preview.MissingFiles.Length == 1, "exact matching rejects nearby wavelength");
            Throws(delegate { session.Calculate(CancellationToken.None); }, "partial missing requires confirmation");
            session.Parameters.AllowMissing = true;
            var rawCopy = (double[])batch.ValidDatasets[0].Intensities.Clone();
            var result = session.Calculate(CancellationToken.None);
            Check(result.Groups.Count == 2, "different series never pooled");
            var group = result.Groups.Single(x => x.Series == "系列 A");
            Check(group.N == 3, "numeric equal pH grouped"); Near(2, group.Mean, "mean 1,2,3"); Near(1, group.Sd.Value, "sample SD 1,2,3");
            var singleton = result.Groups.Single(x => x.Series == "系列 B");
            Check(singleton.N == 1 && !singleton.Sd.HasValue && singleton.Mean == -4, "negative singleton with missing SD");
            Check(result.Samples.Count == 6 && result.Samples.Count(x => x.Status == ExtractionStatus.Excluded) == 1, "excluded sample retained");
            Check(result.Samples.Single(x => x.Status == ExtractionStatus.MissingWavelength).Intensity == null, "missing intensity not zero");
            Check(batch.ValidDatasets[0].Intensities.SequenceEqual(rawCopy), "raw arrays unchanged");
            Check(session.HasCurrentResult, "current snapshot allowed");
            session.Parameters.WavelengthText = "610"; Check(!session.HasCurrentResult, "wavelength edit invalidates even without UI event");
            Throws(delegate { session.RequireCurrentResult(); }, "stale export rejected");
            session.Parameters.WavelengthText = "601"; Throws(delegate { session.Calculate(CancellationToken.None); }, "all missing blocked");
            session.Parameters.WavelengthText = "NaN"; Throws(delegate { session.Preview(); }, "nonfinite wavelength blocked");
            session.Parameters.WavelengthText = "600";
            session.Samples[0].PhText = "15.5";
            Check(!session.GroupingConfirmed, "manual pH edit revokes review");
            Check(session.ConfirmGrouping().Length == 0, "finite pH outside 0-14 accepted");
            var edited = session.Calculate(CancellationToken.None);
            Check(edited.Groups.Any(x => x.Ph == 15.5 && x.N == 1), "edited pH used");
            var two = edited.Groups.Single(x => x.Series == "系列 A" && x.Ph == 2.4);
            Near(2.5, two.Mean, "n2 mean"); Near(Math.Sqrt(0.5), two.Sd.Value, "n2 sample SD");
            Check(edited.Groups.Where(x => x.Series == "系列 A").First().Ph == 2.4, "numeric pH ordering");
            session.Samples[0].PhText = "2.40001"; session.ConfirmGrouping();
            Check(session.Calculate(CancellationToken.None).Groups.Count == 3, "near pH not merged");
            session.Samples[0].PhText = "Infinity"; Check(session.ConfirmGrouping().Length > 0, "nonfinite pH rejected");
            session.Samples[0].PhText = "2.4"; session.Samples[0].RepeatType = RepeatType.Independent;
            Check(session.ConfirmGrouping().Any(x => x.Contains("混用")), "mixed repeat design rejected");
            session.Samples[0].RepeatType = RepeatType.Unconfirmed; session.Samples[0].RepeatId = "2";
            Check(session.ConfirmGrouping().Any(x => x.Contains("冲突")), "duplicate repeat identity rejected");
            session.Samples[0].RepeatId = "sample1/technical1";
            Check(session.ConfirmGrouping().Any(x => x.Contains("嵌套")), "nested repeat design rejected");
            session.Samples[0].RepeatId = "1"; session.Samples[5].ExclusionReason = "";
            Check(session.ConfirmGrouping().Any(x => x.Contains("排除")), "exclusion requires reason");
            foreach (var row in session.Samples) { row.Included = false; row.ExclusionReason = "测试排除"; }
            Check(session.ConfirmGrouping().Length > 0, "no participating files blocked");
            Check(new PhAnalysisSession(new SpectrumBatch(temporary, DateTimeOffset.Now, new FileProcessingResult[0])).ConfirmGrouping().Length > 0, "empty batch blocked");
            Check(!new PhAnalysisSession(batch).HasCurrentResult, "new batch cannot carry result");
            WorkbookTests(temporary, result);
            var extremeBatch = new SpectrumBatch(temporary, DateTimeOffset.Now, new[] {
                File(temporary, "Extreme-pH=1-1", new[] { 600.0 }, new[] { double.MaxValue }),
                File(temporary, "Extreme-pH=1-2", new[] { 600.0 }, new[] { double.MaxValue }),
                File(temporary, "Extreme-pH=1-3", new[] { 600.0 }, new[] { double.MaxValue }) });
            var extremeSession = new PhAnalysisSession(extremeBatch);
            foreach (var row in extremeSession.Samples) row.Series = "Extreme finite test";
            Check(extremeSession.ConfirmGrouping().Length == 0, "extreme finite grouping");
            extremeSession.Parameters.WavelengthText = "600";
            var extreme = extremeSession.Calculate(CancellationToken.None).Groups.Single();
            Check(extreme.Mean == double.MaxValue && extreme.Sd == 0.0, "finite scaled mean avoids intermediate overflow");
            PerformanceTests();
            Console.WriteLine("PASS: pH grouping, exact extraction, descriptive statistics, invalidation and traceable export.");
        }

        private static void SuggestionTests(string folder)
        {
            foreach (var name in new[] { "probe pH=7.4-1", "probe(pH = 7.4)-2" })
            {
                var suggestion = PhAnalysisService.Suggest(File(folder, name, new[] { 600.0 }, new[] { 1.0 }).Dataset);
                Check(suggestion.PhText == "7.4" && suggestion.Series == "" && suggestion.RepeatType == RepeatType.Unconfirmed, "limited naming suggestions");
            }
            foreach (var name in new[] { "10uM 20261015-1", "probe pH7.4-1", "probe pH=7.4um-1", "probe pH=2.4 pH=7.4-1", "probe-pH=NaN-1" })
                Check(PhAnalysisService.Suggest(File(folder, name, new[] { 600.0 }, new[] { 1.0 }).Dataset).PhText == "", "ambiguous filename not inferred: " + name);
        }

        private static void WorkbookTests(string folder, PhAnalysisResult result)
        {
            var output = Path.Combine(folder, "ph-analysis.xlsx");
            new WorkbookExporter().ExportAnalysis(result, output, false);
            using (var archive = ZipFile.OpenRead(output))
            {
                var book = LoadXml(archive, "xl/workbook.xml");
                var manager = Namespaces(book);
                Check(book.SelectNodes("//s:sheet", manager).Count == 3, "analysis sheet count");
                Check(book.InnerText == "", "no metadata values leaked into workbook topology");
                var detail = LoadXml(archive, "xl/worksheets/sheet1.xml"); var ns = Namespaces(detail);
                Check(detail.SelectSingleNode("//s:c[@r='I6']", ns) == null, "missing extraction exported blank");
                Check(detail.SelectSingleNode("//s:c[@r='I7']", ns) == null, "excluded intensity exported blank");
                Check(detail.SelectSingleNode("//s:c[@r='A2']/s:is/s:t", ns).InnerText == result.Samples[0].SourceId, "source ID mapping");
                var stats = LoadXml(archive, "xl/worksheets/sheet2.xml"); ns = Namespaces(stats);
                Check(stats.SelectSingleNode("//s:c[@r='F3']", ns) == null, "singleton SD exported blank");
                Check(stats.SelectSingleNode("//s:c[@r='G3']/s:is/s:t", ns).InnerText.Contains("不可计算"), "singleton explanation retained");
                foreach (var part in archive.Entries.Where(x => x.FullName.EndsWith(".xml", StringComparison.Ordinal)))
                {
                    var xml = LoadXml(archive, part.FullName);
                    Check(xml.SelectNodes("//*[local-name()='f']").Count == 0, "all exports contain literal values, no formulas");
                }
            }
            var original = FileBytes(output);
            using (var locked = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.None))
                Throws(delegate { new WorkbookExporter().ExportAnalysis(result, output, true); }, "locked workbook fails safely");
            Check(original.SequenceEqual(FileBytes(output)), "locked existing workbook preserved");
            new WorkbookExporter().ExportAnalysis(result, output, true);
            Check(!Directory.GetFiles(folder, ".fluolab-*.tmp").Any(), "failed export temporary cleanup");
            var injection = new PhAnalysisSession(new SpectrumBatch(folder, DateTimeOffset.Now,
                new[] { File(folder, "=1+2 & sample-1", new[] { 600.0 }, new[] { 1.0 }) }));
            injection.Samples[0].Series = "=SUM(1,2) & <条件>"; injection.Samples[0].PhText = "-2";
            Check(injection.ConfirmGrouping().Length == 0, "literal text and negative pH accepted");
            injection.Parameters.WavelengthText = "600";
            new WorkbookExporter().ExportAnalysis(injection.Calculate(CancellationToken.None), output, true);
            using (var archive = ZipFile.OpenRead(output))
            {
                var xml = LoadXml(archive, "xl/worksheets/sheet1.xml"); var ns = Namespaces(xml);
                Check(xml.SelectSingleNode("//s:c[@r='C2']", ns).Attributes["t"].Value == "inlineStr", "formula-like series written as text");
                Check(xml.SelectSingleNode("//s:c[@r='C2']/s:is/s:t", ns).InnerText == "=SUM(1,2) & <条件>", "special text roundtrip");
            }
        }

        private static void PerformanceTests()
        {
            var batch = PhDemo.CreateBatch(); var session = PhDemo.CreateConfirmedSession(batch);
            var watch = Stopwatch.StartNew(); session.Calculate(CancellationToken.None); watch.Stop();
            Console.WriteLine("INFO: pH analysis-only time {0:0.000000} seconds (synthetic demo; excludes parsing/UI/export).", watch.Elapsed.TotalSeconds);
        }
        private static FileProcessingResult File(string folder, string name, double[] waves, double[] values)
        {
            var path = Path.Combine(folder, name + ".ASC");
            return FileProcessingResult.Succeeded(path, new SpectrumDataset(name, path, waves, values));
        }
        private static byte[] FileBytes(string path) { return System.IO.File.ReadAllBytes(path); }
        private static XmlDocument LoadXml(ZipArchive archive, string name)
        { var xml = new XmlDocument(); using (var stream = archive.GetEntry(name).Open()) xml.Load(stream); return xml; }
        private static XmlNamespaceManager Namespaces(XmlDocument xml)
        { var ns = new XmlNamespaceManager(xml.NameTable); ns.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"); return ns; }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("pH assertion: " + message); }
        private static void Near(double expected, double actual, string message) { Check(Math.Abs(expected - actual) < 1e-10, message); }
        private static void Throws(Action action, string message)
        { try { action(); } catch (InvalidOperationException) { return; } catch (IOException) { return; } throw new InvalidOperationException("Expected rejection: " + message); }
    }
}
