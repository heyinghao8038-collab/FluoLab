using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace FluoLab.Core
{
    public static class PhDemo
    {
        public static SpectrumBatch CreateBatch()
        {
            var files = new List<FileProcessingResult>();
            foreach (var series in new[] { "模拟条件 A", "模拟条件 B" })
            foreach (var ph in new[] { 2.4, 4.0, 7.4, 10.4 })
            for (var repeat = 1; repeat <= (ph == 10.4 ? 1 : 3); repeat++)
            {
                var missing = series == "模拟条件 B" && ph == 7.4 && repeat == 3;
                var waves = missing ? new[] { 490.0, 610.0, 800.0 } : new[] { 490.0, 600.0, 610.0, 800.0 };
                var baseValue = (series == "模拟条件 A" ? 100 : 160) + ph * (series == "模拟条件 A" ? 28 : 16);
                var intensity = baseValue + (repeat - 2) * 12;
                var name = series + " pH=" + ph.ToString("0.0", CultureInfo.InvariantCulture) + "-" + repeat;
                var source = Path.GetFullPath(Path.Combine("SyntheticDemo", name + ".ASC"));
                var dataset = new SpectrumDataset(name, source, waves,
                    waves.Select(x => x == 600 ? intensity : x == 490 ? -2.0 + repeat : x == 610 ? intensity * 0.92 : 20.0).ToArray());
                files.Add(FileProcessingResult.Succeeded(source, dataset));
            }
            var excludedPath = Path.GetFullPath(Path.Combine("SyntheticDemo", "模拟排除样品 pH=4.0-9.ASC"));
            files.Add(FileProcessingResult.Succeeded(excludedPath, new SpectrumDataset(Path.GetFileNameWithoutExtension(excludedPath), excludedPath,
                new[] { 490.0, 600.0, 800.0 }, new[] { -3.0, 500.0, 10.0 })));
            var failed = Path.GetFullPath(Path.Combine("SyntheticDemo", "模拟解析失败.ASC"));
            files.Add(FileProcessingResult.Failed(failed, "未找到准确的 #DATA 标记。"));
            return new SpectrumBatch(Path.GetFullPath("SyntheticDemo"), DateTimeOffset.Now, files);
        }

        public static PhAnalysisSession CreateConfirmedSession(SpectrumBatch batch)
        {
            var session = new PhAnalysisSession(batch);
            foreach (var sample in session.Samples)
            {
                var name = Path.GetFileName(sample.SourcePath);
                sample.Series = name.StartsWith("模拟条件 A", StringComparison.Ordinal) ? "模拟条件 A" : "模拟条件 B";
                if (name.StartsWith("模拟排除", StringComparison.Ordinal))
                { sample.Included = false; sample.ExclusionReason = "演示人工排除流程，非自动异常值判断。"; }
            }
            var errors = session.ConfirmGrouping();
            if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
            session.Parameters.WavelengthText = "600";
            session.Parameters.AllowMissing = true;
            return session;
        }

        public static SpectrumBatch CreateLongNameBatch()
        {
            var batch = CreateBatch();
            var files = batch.Files.ToArray();
            var name = "模拟条件 A " + string.Concat(Enumerable.Repeat("长文件名与不同实验条件应明确区分_", 8)) + "pH=2.4-1";
            var source = Path.GetFullPath(Path.Combine("SyntheticDemo", name + ".ASC"));
            files[0] = FileProcessingResult.Succeeded(source, new SpectrumDataset(name, source, files[0].Dataset.Wavelengths, files[0].Dataset.Intensities));
            return new SpectrumBatch(batch.SourceFolder, batch.ProcessedAt, files);
        }

        public static void WriteAscFiles(string folder)
        {
            Directory.CreateDirectory(folder);
            foreach (var file in CreateBatch().Files)
            {
                var path = Path.Combine(folder, file.FileName);
                if (File.Exists(path)) throw new IOException("演示文件已存在，未覆盖：" + file.FileName);
                var builder = new StringBuilder("SYNTHETIC DEMO - NOT EXPERIMENTAL DATA\n");
                if (file.Dataset != null)
                {
                    builder.AppendLine("#DATA");
                    for (var i = 0; i < file.Dataset.PointCount; i++)
                        builder.AppendLine(file.Dataset.Wavelengths[i].ToString("R", CultureInfo.InvariantCulture) + "\t" + file.Dataset.Intensities[i].ToString("R", CultureInfo.InvariantCulture));
                }
                File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
            }
        }
        public static SpectrumBatch ReadAscFiles(string folder)
        {
            var batch = new BatchProcessor().ProcessFolder(folder, null, CancellationToken.None);
            var expected = CreateBatch();
            if (batch.Files.Length != expected.Files.Length) throw new InvalidOperationException("模拟 ASC 数量不一致，未将该目录作为公开演示数据导出。");
            foreach (var source in expected.Files)
            {
                var actual = batch.Files.SingleOrDefault(file => string.Equals(file.FileName, source.FileName, StringComparison.Ordinal));
                if (actual == null || actual.Status != source.Status || (source.Dataset != null &&
                    (!actual.Dataset.Wavelengths.SequenceEqual(source.Dataset.Wavelengths) || !actual.Dataset.Intensities.SequenceEqual(source.Dataset.Intensities))))
                    throw new InvalidOperationException("模拟 ASC 内容已改变，未将其作为公开演示数据导出。");
            }
            return batch;
        }
    }
}
