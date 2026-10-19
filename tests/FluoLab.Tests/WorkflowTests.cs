using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using FluoLab.Core;
using FluoLab.Export;

namespace FluoLab.Tests
{
    internal static class WorkflowTests
    {
        public static void Run(string temporary)
        {
            var folder = Path.Combine(temporary, "workflow-source"); Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, "模拟样品 pH=2.4-1.ASC");
            File.WriteAllText(file, "header\n#DATA\n600 1\n610 -2\n", new UTF8Encoding(false));
            var batch = new BatchProcessor().ProcessFolder(folder, null, CancellationToken.None);
            Check(batch.SuccessCount == 1, "fingerprint parser success");
            Check(batch.ValidDatasets[0].SourceFingerprint == SourceIdentity.FileHash(file), "hash covers exact parsed bytes");
            var session = new PhAnalysisSession(batch); session.Samples[0].Series = "系列\tA\n含\"引号";
            var tsv = GroupingTransfer.Copy(session.Samples);
            var paste = GroupingTransfer.Parse(tsv, session.Samples);
            Check(paste.Length == 1 && paste[0].Series == session.Samples[0].Series, "quoted TSV roundtrip");
            paste[0].Series = "manual"; Check(session.Samples[0].Series != "manual", "paste returns independent validated updates");
            Throws(delegate { GroupingTransfer.Parse(tsv + "\r\n" + tsv.Split('\n').Last(), session.Samples); }, "duplicate paste file blocked");
            Throws(delegate { GroupingTransfer.Parse(tsv.Replace("2.4", "NaN"), session.Samples); }, "nonfinite paste blocked");
            Throws(delegate { GroupingTransfer.Parse(tsv.Replace("模拟样品", "Unknown"), session.Samples); }, "unknown source blocked");
            Throws(delegate { GroupingTransfer.Parse("not a template", session.Samples); }, "missing headers blocked");
            Throws(delegate { GroupingTransfer.Parse(tsv + "\r\n\"unclosed", session.Samples); }, "malformed quote blocked");
            var risky = session.Samples[0].Copy(); risky.Series = "=1+1";
            Throws(delegate { GroupingTransfer.Copy(new[] { risky }); }, "clipboard formula triggers fail safely without changing text");
            risky.Series = "safe"; risky.PhText = "=1+1";
            Throws(delegate { GroupingTransfer.Copy(new[] { risky }); }, "invalid pH draft cannot become clipboard formula");
            session.Parameters.WavelengthText = "600"; session.Parameters.IntensityUnit = "已确认单位";
            var projectPath = Path.Combine(temporary, "模拟工作项目.fluolab");
            var project = WorkProjectStore.Capture(batch, session, projectPath, false);
            AtomicOutput.Write(projectPath, false, p => File.WriteAllBytes(p, WorkProjectStore.Serialize(project)));
            var loaded = WorkProjectStore.Read(projectPath);
            var projectBytes = File.ReadAllBytes(projectPath);
            using (var lockedProject = new FileStream(projectPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Throws(delegate { AtomicOutput.Write(projectPath, true, p => File.WriteAllBytes(p, WorkProjectStore.Serialize(project))); }, "locked work project fails safely");
            Check(projectBytes.SequenceEqual(File.ReadAllBytes(projectPath)), "locked project bytes preserved");
            Check(WorkProjectStore.ResolveFolder(loaded, projectPath).TrimEnd(Path.DirectorySeparatorChar) == folder, "relative source path roundtrip");
            var restored = WorkProjectStore.Restore(loaded, batch);
            Check(restored.Samples[0].Series == session.Samples[0].Series && restored.Parameters.WavelengthText == "600", "project metadata and parameters roundtrip");
            Check(!restored.GroupingConfirmed && !restored.HasCurrentResult && !restored.Parameters.AllowMissing, "restore never trusts cached consents/results");
            session.Samples[0].Series = "";
            var draft = WorkProjectStore.Capture(batch, session, projectPath, false);
            Check(WorkProjectStore.Restore(draft, batch).Samples[0].Series == "", "unfinished draft can be resumed");
            var moved = Path.Combine(temporary, "moved-source"); Directory.CreateDirectory(moved);
            File.Copy(file, Path.Combine(moved, Path.GetFileName(file)));
            var relocated = new BatchProcessor().ProcessFolder(moved, null, CancellationToken.None);
            Check(WorkProjectStore.Restore(loaded, relocated).Samples[0].SourcePath.StartsWith(moved, StringComparison.Ordinal), "relocation matches content not absolute path");
            var extra = Path.Combine(moved, "extra.ASC"); File.Copy(file, extra);
            Throws(delegate { WorkProjectStore.Restore(loaded, new BatchProcessor().ProcessFolder(moved, null, CancellationToken.None)); }, "new valid source blocks silent metadata reuse");
            File.Delete(extra);
            session.Samples[0].Series = "实验组"; session.ConfirmGrouping();
            var result = session.Calculate(CancellationToken.None);
            Check(result.Samples[0].SourceFingerprint == SourceIdentity.FileHash(file), "analysis carries source fingerprint");
            var zipPath = Path.Combine(temporary, "analysis.zip");
            Action<string, string> fakeCharts = delegate(string png, string svg) { File.WriteAllText(png, "test chart callback"); File.WriteAllText(svg, "<svg/>"); };
            AnalysisBundleExporter.Export(result, batch, zipPath, false, false, false, fakeCharts);
            using (var zip = ZipFile.OpenRead(zipPath)) Check(zip.Entries.Count == 5 && !zip.Entries.Any(x => x.FullName.StartsWith("ASC/")), "bundle excludes raw by default");
            AnalysisBundleExporter.Export(result, batch, zipPath, true, true, false, fakeCharts);
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                Check(zip.Entries.Count == 6, "bundle raw opt-in includes one source");
                using (var input = zip.GetEntry("ASC/" + Path.GetFileName(file)).Open())
                using (var memory = new MemoryStream()) { input.CopyTo(memory); Check(memory.ToArray().SequenceEqual(File.ReadAllBytes(file)), "raw source bytes preserved in zip"); }
                using (var reader = new StreamReader(zip.GetEntry("源文件指纹.xml").Open())) Check(!reader.ReadToEnd().Contains(folder), "public manifest omits absolute paths");
            }
            var before = File.ReadAllBytes(zipPath);
            using (var locked = new FileStream(zipPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Throws(delegate { AnalysisBundleExporter.Export(result, batch, zipPath, true, false, false, fakeCharts); }, "locked bundle fails safely");
            Check(before.SequenceEqual(File.ReadAllBytes(zipPath)), "locked zip preserved");
            File.AppendAllText(file, "\n");
            Throws(delegate { WorkProjectStore.Restore(loaded, batch); }, "disk changed after import blocks restoration");
            var changed = new BatchProcessor().ProcessFolder(folder, null, CancellationToken.None);
            Throws(delegate { WorkProjectStore.Restore(loaded, changed); }, "same name changed bytes blocked");
            Throws(delegate { AnalysisBundleExporter.Export(result, batch, zipPath, true, true, false, fakeCharts); }, "changed raw source blocks bundle");
            Check(before.SequenceEqual(File.ReadAllBytes(zipPath)), "failed raw verification preserves previous zip");
            File.Delete(file);
            Throws(delegate { WorkProjectStore.Restore(loaded, new BatchProcessor().ProcessFolder(folder, null, CancellationToken.None)); }, "missing file blocks restoration");
            File.WriteAllText(projectPath, "<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///not-allowed'>]><FluoLabProject version='1'>&a;</FluoLabProject>");
            Throws(delegate { WorkProjectStore.Read(projectPath); }, "DTD entities rejected");
            File.WriteAllText(projectPath, "<FluoLabProject version='999'/>");
            Throws(delegate { WorkProjectStore.Read(projectPath); }, "unknown project version rejected");
            Console.WriteLine("PASS: grouping TSV, project drafts/relocation/fingerprints, bundle privacy/raw opt-in/atomic failure.");
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Throws(Action action, string message) { try { action(); } catch (Exception) { return; } throw new InvalidOperationException(message); }
    }
}
