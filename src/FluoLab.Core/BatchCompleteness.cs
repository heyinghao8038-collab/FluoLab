using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace FluoLab.Core
{
    public sealed class BatchCompletenessRow
    {
        public string FileName { get; internal set; }
        public string Status { get; internal set; }
        public string Reason { get; internal set; }
        public string FailureCategory { get; internal set; }
        public int? PointCount { get; internal set; }
        public double? Minimum { get; internal set; }
        public double? Maximum { get; internal set; }
        public int? MissingCount { get; internal set; }
    }

    public sealed class BatchCompletenessReport
    {
        public string BatchName { get; internal set; }
        public DateTimeOffset CreatedAt { get; internal set; }
        public int ValidCount { get; internal set; }
        public long TotalMeasurementCount { get; internal set; }
        public bool CoverageConsistent { get; internal set; }
        public double[] Union { get; internal set; }
        public double[] Intersection { get; internal set; }
        public BatchCompletenessRow[] Files { get; internal set; }
    }

    public static class BatchCompleteness
    {
        public const string Explanation = "完整性仅描述文件解析与波长覆盖，不评价实验质量，不排除数据、不补零、不改变原始光谱或导出范围。缺点数相对于本批有效文件的波长并集。共同点为每份有效文件都实际测得的精确波长，不是范围重叠。";

        public static BatchCompletenessReport Create(SpectrumBatch batch, CancellationToken cancellation)
        {
            if (batch == null) throw new ArgumentNullException("batch");
            var union = new SortedSet<double>();
            HashSet<double> intersection = null;
            foreach (var dataset in batch.ValidDatasets)
            {
                cancellation.ThrowIfCancellationRequested();
                union.UnionWith(dataset.Wavelengths);
                if (intersection == null) intersection = new HashSet<double>(dataset.Wavelengths);
                else intersection.IntersectWith(dataset.Wavelengths);
            }
            var rows = new List<BatchCompletenessRow>();
            foreach (var file in batch.Files)
            {
                cancellation.ThrowIfCancellationRequested();
                var dataset = file.Dataset;
                var valid = file.Status == ProcessingStatus.Success && dataset != null;
                rows.Add(new BatchCompletenessRow {
                    FileName = file.FileName, Status = valid ? "有效" : "解析失败", Reason = valid ? "" : file.ErrorMessage,
                    FailureCategory = valid ? "" : ClassifyFailure(file.ErrorMessage),
                    PointCount = valid ? (int?)dataset.PointCount : null,
                    Minimum = valid ? (double?)dataset.Wavelengths[0] : null,
                    Maximum = valid ? (double?)dataset.Wavelengths[dataset.PointCount - 1] : null,
                    MissingCount = valid ? (int?)(union.Count - dataset.PointCount) : null
                });
            }
            return new BatchCompletenessReport {
                BatchName = System.IO.Path.GetFileName(batch.SourceFolder.TrimEnd(System.IO.Path.DirectorySeparatorChar)),
                CreatedAt = DateTimeOffset.Now, ValidCount = batch.ValidDatasets.Length,
                TotalMeasurementCount = batch.ValidDatasets.Sum(dataset => (long)dataset.PointCount),
                CoverageConsistent = intersection != null && union.Count == intersection.Count,
                Union = union.ToArray(), Intersection = intersection == null ? new double[0] : intersection.OrderBy(x => x).ToArray(), Files = rows.ToArray()
            };
        }

        public static string Text(BatchCompletenessReport report)
        {
            var text = new StringBuilder("FluoLab 批次完整性摘要\r\n批次：" + report.BatchName + "\r\n时间：" + report.CreatedAt.ToString("O", CultureInfo.InvariantCulture) + "\r\n" + Explanation + "\r\n");
            text.AppendFormat(CultureInfo.InvariantCulture, "发现 {0} 份；有效 {1} 份；解析失败 {2} 份。\r\n", report.Files.Length, report.ValidCount, report.Files.Length - report.ValidCount);
            text.Append("波长并集：").Append(report.Union.Length).Append(" 点；共同波长：").Append(report.Intersection.Length).Append(" 点。\r\n");
            foreach (var row in report.Files)
                text.Append("\r\n文件：").Append(row.FileName).Append("\r\n状态：").Append(row.Status).Append("\r\n点数：").Append(row.PointCount.HasValue ? row.PointCount.Value.ToString(CultureInfo.InvariantCulture) : "不可用")
                    .Append("；最小波长：").Append(Number(row.Minimum)).Append(" nm；最大波长：").Append(Number(row.Maximum)).Append(" nm；缺点数：").Append(row.MissingCount.HasValue ? row.MissingCount.Value.ToString(CultureInfo.InvariantCulture) : "不可用")
                    .Append("\r\n").Append(row.Reason);
            return text.ToString();
        }
        private static string Number(double? value) { return value.HasValue ? value.Value.ToString("R", CultureInfo.InvariantCulture) : "不可用"; }
        public static string ClassifyFailure(string reason)
        {
            reason = reason ?? "";
            if ((reason.Contains("未找到") || reason.Contains("缺少")) && reason.Contains("#DATA")) return "缺少 #DATA";
            if (reason.Contains("没有可用数据")) return "空数据";
            if (reason.Contains("重复波长")) return "重复波长";
            if (reason.Contains("数值") || reason.Contains("两列")) return "数据行格式错误";
            if (reason.Contains("占用") || reason.Contains("权限") || reason.Contains("不存在") || reason.Contains("读取失败")) return "文件无法读取";
            return "其他解析失败";
        }
    }
}
