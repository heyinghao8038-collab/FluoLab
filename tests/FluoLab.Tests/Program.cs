using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using FluoLab.Core;
using FluoLab.Export;

namespace FluoLab.Tests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var tempRoot = Path.Combine(Path.GetTempPath(), "fluolab-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                RunParserAndExportTests(tempRoot);
                PhAnalysisTests.Run(tempRoot);
                LaboratoryTests.Run(tempRoot);
                RunPerformanceTest(tempRoot);
                Console.WriteLine("PASS: all FluoLab tests completed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: {0}", exception);
                return 1;
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private static void RunParserAndExportTests(string tempRoot)
        {
            var source = Path.Combine(tempRoot, "中文路径", "测试批次");
            Directory.CreateDirectory(source);
            WriteAscii(Path.Combine(source, "A-good.ASC"), "header\r\n#DATA\r\n490\t1.25\r\n491\t2.5\r\n");
            WriteAscii(Path.Combine(source, "b-good.asc"), "#DATA\n491 3.5\n492 4.5\n");
            WriteAscii(Path.Combine(source, "C-missing.ASC"), "header only\n");
            WriteAscii(Path.Combine(source, "D-duplicate.ASC"), "#DATA\n490 1\n490.000000 2\n");
            WriteAscii(Path.Combine(source, "E-invalid.ASC"), "#DATA\n490 not-a-number\n");
            WriteAscii(Path.Combine(source, "F-empty.ASC"), "#DATA\n\n");
            Directory.CreateDirectory(Path.Combine(source, "ignored-child"));
            WriteAscii(Path.Combine(source, "ignored-child", "ignored.ASC"), "#DATA\n490 99\n");

            var batch = new BatchProcessor().ProcessFolder(source, null, CancellationToken.None);
            AssertEqual(6, batch.DiscoveredCount, "top-level file count");
            AssertEqual(2, batch.SuccessCount, "valid file count");
            AssertEqual(4, batch.SkippedCount, "skipped file count");
            AssertEqual("A-good.ASC", batch.Files[0].FileName, "case-insensitive sort");

            var union = WavelengthAlignment.BuildUnion(batch.ValidDatasets);
            AssertEqual(3, union.Length, "wavelength union count");
            AssertClose(490, union[0], "wavelength union start");
            AssertClose(492, union[2], "wavelength union end");
            AssertClose(1.25, batch.ValidDatasets[0].Intensities[0], "intensity parsing");

            var output = Path.Combine(tempRoot, "synthetic-output.xlsx");
            new WorkbookExporter().Export(batch, output, false);
            AssertTrue(File.Exists(output), "workbook was created");
            ValidateWorkbookPackage(output, 4, 3);
            var originalLength = new FileInfo(output).Length;
            new WorkbookExporter().Export(batch, output, true);
            AssertTrue(File.Exists(output) && new FileInfo(output).Length == originalLength, "atomic workbook replacement");
            var overwriteRejected = false;
            try
            {
                new WorkbookExporter().Export(batch, output, false);
            }
            catch (IOException)
            {
                overwriteRejected = true;
            }

            AssertTrue(overwriteRejected, "existing workbook requires explicit overwrite");

            var emptyFolder = Path.Combine(tempRoot, "empty");
            Directory.CreateDirectory(emptyFolder);
            var emptyBatch = new BatchProcessor().ProcessFolder(emptyFolder, null, CancellationToken.None);
            AssertEqual(0, emptyBatch.DiscoveredCount, "empty folder count");

            var lockedPath = Path.Combine(tempRoot, "locked.ASC");
            WriteAscii(lockedPath, "#DATA\n490 1\n");
            using (var lockStream = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var lockedBatch = new BatchProcessor().ProcessFolder(tempRoot, null, CancellationToken.None);
                var lockedResult = lockedBatch.Files.Single(file => file.FileName == "locked.ASC");
                AssertEqual(ProcessingStatus.Failed, lockedResult.Status, "locked file status");
            }

            Console.WriteLine("PASS: parser, alignment, errors, Chinese paths, locked files, and xlsx package.");
        }

        private static void RunPerformanceTest(string tempRoot)
        {
            var source = Path.Combine(tempRoot, "performance-100x3101");
            Directory.CreateDirectory(source);
            var content = new StringBuilder(72000);
            content.AppendLine("Instrument header");
            content.AppendLine("#DATA");
            for (var index = 0; index < 3101; index++)
            {
                var wavelength = 490 + index * 0.1;
                content.Append(wavelength.ToString("0.0", CultureInfo.InvariantCulture));
                content.Append('\t');
                content.AppendLine((200 + 5000 * Math.Exp(-Math.Pow((wavelength - 610) / 35, 2))).ToString("R", CultureInfo.InvariantCulture));
            }

            var bytes = Encoding.ASCII.GetBytes(content.ToString());
            for (var fileIndex = 0; fileIndex < 100; fileIndex++)
            {
                File.WriteAllBytes(
                    Path.Combine(source, string.Format(CultureInfo.InvariantCulture, "Sample-{0:000}.ASC", fileIndex)),
                    bytes);
            }

            var watch = Stopwatch.StartNew();
            var batch = new BatchProcessor().ProcessFolder(source, null, CancellationToken.None);
            watch.Stop();
            AssertEqual(100, batch.SuccessCount, "performance batch valid count");
            AssertTrue(watch.Elapsed.TotalSeconds < 5.0, "100 × 3,101 parsing exceeded five seconds");
            Console.WriteLine(
                "PASS: 100 × 3,101 parsed in {0:0.000} seconds.",
                watch.Elapsed.TotalSeconds);
            var parseSeconds = watch.Elapsed.TotalSeconds;
            var session = new PhAnalysisSession(batch);
            for (var index = 0; index < session.Samples.Count; index++)
            {
                session.Samples[index].Series = "Synthetic performance series";
                session.Samples[index].PhText = (index / 3).ToString(CultureInfo.InvariantCulture);
                session.Samples[index].RepeatId = (index % 3 + 1).ToString(CultureInfo.InvariantCulture);
            }
            AssertEqual(0, session.ConfirmGrouping().Length, "performance grouping");
            session.Parameters.WavelengthText = "600";
            watch.Restart();
            var result = session.Calculate(CancellationToken.None);
            watch.Stop(); var analysisSeconds = watch.Elapsed.TotalSeconds;
            watch.Restart();
            new WorkbookExporter().ExportAnalysis(result, Path.Combine(tempRoot, "performance-analysis.xlsx"), false);
            watch.Stop();
            Console.WriteLine("INFO: 100-file pipeline parse={0:0.000000}s, analysis={1:0.000000}s, analysis-xlsx={2:0.000000}s, summed-machine-work={3:0.000000}s (excludes human review/UI).",
                parseSeconds, analysisSeconds, watch.Elapsed.TotalSeconds, parseSeconds + analysisSeconds + watch.Elapsed.TotalSeconds);
        }

        private static void ValidateWorkbookPackage(string workbookPath, int expectedRows, int expectedColumns)
        {
            using (var archive = ZipFile.OpenRead(workbookPath))
            {
                AssertTrue(archive.GetEntry("[Content_Types].xml") != null, "xlsx content types part");
                AssertTrue(archive.GetEntry("xl/workbook.xml") != null, "xlsx workbook part");
                AssertTrue(archive.GetEntry("xl/styles.xml") != null, "xlsx styles part");
                AssertTrue(archive.GetEntry("xl/worksheets/sheet1.xml") != null, "xlsx summary sheet");
                AssertTrue(archive.GetEntry("xl/worksheets/sheet2.xml") != null, "xlsx log sheet");

                var sheet = new XmlDocument();
                using (var stream = archive.GetEntry("xl/worksheets/sheet1.xml").Open())
                {
                    sheet.Load(stream);
                }

                var manager = new XmlNamespaceManager(sheet.NameTable);
                manager.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                var rows = sheet.SelectNodes("/s:worksheet/s:sheetData/s:row", manager);
                AssertEqual(expectedRows, rows.Count, "xlsx summary row count");
                var headerCells = sheet.SelectNodes("/s:worksheet/s:sheetData/s:row[@r='1']/s:c", manager);
                AssertEqual(expectedColumns, headerCells.Count, "xlsx summary column count");
                var pane = sheet.SelectSingleNode("/s:worksheet/s:sheetViews/s:sheetView/s:pane[@state='frozen']", manager);
                AssertTrue(pane != null, "xlsx frozen pane");
                AssertTrue(sheet.SelectSingleNode("//s:c[@r='C2']", manager) == null, "missing wavelength remains blank");
                AssertTrue(sheet.SelectSingleNode("//s:c[@r='B4']", manager) == null, "different range remains blank");
                AssertClose(1.25, double.Parse(sheet.SelectSingleNode("//s:c[@r='B2']/s:v", manager).InnerText, CultureInfo.InvariantCulture), "aligned original intensity");
            }
        }

        private static void WriteAscii(string path, string content)
        {
            File.WriteAllText(path, content, Encoding.ASCII);
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Assertion failed: " + message);
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new InvalidOperationException(
                    string.Format("Assertion failed: {0}. Expected {1}, actual {2}.", message, expected, actual));
            }
        }

        private static void AssertClose(double expected, double actual, string message)
        {
            if (Math.Abs(expected - actual) > 1e-9)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Assertion failed: {0}. Expected {1:R}, actual {2:R}.",
                        message,
                        expected,
                        actual));
            }
        }
    }
}
