using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace FluoLab.Core
{
    public enum RepeatType { Unconfirmed, Technical, Independent }
    public enum ExtractionStatus { Success, MissingWavelength, Excluded }

    public sealed class PhSampleMetadata
    {
        public string SourcePath { get; set; }
        public string Series { get; set; }
        public string PhText { get; set; }
        public string RepeatId { get; set; }
        public RepeatType RepeatType { get; set; }
        public bool Included { get; set; }
        public string ExclusionReason { get; set; }
        public PhSampleMetadata Copy() { return (PhSampleMetadata)MemberwiseClone(); }
    }

    public sealed class PhAnalysisParameters
    {
        public string WavelengthText { get; set; }
        public string IntensityUnit { get; set; }
        public bool AllowMissing { get; set; }
        public PhAnalysisParameters Copy() { return (PhAnalysisParameters)MemberwiseClone(); }
    }

    public sealed class PhExtractionResult
    {
        public string SourceId { get; internal set; }
        public string SourceFingerprint { get; internal set; }
        public string FileName { get; internal set; }
        public string Series { get; internal set; }
        public double? Ph { get; internal set; }
        public string PhInput { get; internal set; }
        public string RepeatId { get; internal set; }
        public RepeatType RepeatType { get; internal set; }
        public bool RequestedInclusion { get; internal set; }
        public double TargetWavelength { get; internal set; }
        public double? Intensity { get; internal set; }
        public ExtractionStatus Status { get; internal set; }
        public string Reason { get; internal set; }
    }

    public sealed class PhGroupStatistics
    {
        public string Series { get; internal set; }
        public double Ph { get; internal set; }
        public RepeatType RepeatType { get; internal set; }
        public int N { get; internal set; }
        public double Mean { get; internal set; }
        public double? Sd { get; internal set; }
        public string SdNote { get { return Sd.HasValue ? "样本 SD，分母 n−1" : "不可计算（n=1）"; } }
    }

    public sealed class PhAnalysisResult
    {
        public const string SoftwareVersion = "1.4.2";
        public double TargetWavelength { get; internal set; }
        public string IntensityUnit { get; internal set; }
        public string SourceBatchName { get; internal set; }
        public DateTimeOffset AnalyzedAt { get; internal set; }
        public int SourceCount { get; internal set; }
        public int ParseFailedCount { get; internal set; }
        public bool MissingExclusionConfirmed { get; internal set; }
        public ReadOnlyCollection<PhExtractionResult> Samples { get; internal set; }
        public ReadOnlyCollection<PhGroupStatistics> Groups { get; internal set; }
        public string ConfigurationKey { get; internal set; }
    }

    public sealed class PhMatchPreview
    {
        public int ParticipatingCount { get; internal set; }
        public int MatchedCount { get; internal set; }
        public string[] MissingFiles { get; internal set; }
    }

    public static class PhAnalysisService
    {
        private static readonly Regex PhToken = new Regex(
            @"(?<![A-Za-z0-9])pH\s*=\s*([+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)(?=$|[\s)\]_\-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex RepeatToken = new Regex(@"-(\d+)$", RegexOptions.CultureInvariant);

        public static PhSampleMetadata Suggest(SpectrumDataset dataset)
        {
            var matches = PhToken.Matches(dataset.DisplayName);
            var repeat = RepeatToken.Match(dataset.DisplayName);
            double value;
            return new PhSampleMetadata
            {
                SourcePath = dataset.SourcePath,
                Series = string.Empty,
                PhText = matches.Count == 1 && AscParser.TryParseFinite(matches[0].Groups[1].Value, out value)
                    ? value.ToString("R", CultureInfo.InvariantCulture) : string.Empty,
                RepeatId = repeat.Success ? repeat.Groups[1].Value : string.Empty,
                RepeatType = RepeatType.Unconfirmed,
                Included = true,
                ExclusionReason = string.Empty
            };
        }

        public static string RepeatLabel(RepeatType type)
        {
            return type == RepeatType.Technical ? "技术重复" : type == RepeatType.Independent ? "独立实验重复" : "未确认";
        }

        public static string StatusLabel(ExtractionStatus status)
        {
            return status == ExtractionStatus.Success ? "成功" : status == ExtractionStatus.MissingWavelength ? "缺少目标波长" : "被排除";
        }

        public static string[] Validate(SpectrumBatch batch, IList<PhSampleMetadata> samples)
        {
            var errors = new List<string>();
            if (batch == null || samples == null || samples.Count == 0)
                return new[] { "没有可分析的有效光谱，请先导入 ASC。" };
            var validPaths = new HashSet<string>(batch.ValidDatasets.Select(x => x.SourcePath), StringComparer.OrdinalIgnoreCase);
            if (samples.Count != validPaths.Count || samples.Select(x => x.SourcePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != samples.Count)
                errors.Add("分组表必须且只能包含本批次的每份有效文件一次。请重新打开分析。");
            foreach (var sample in samples)
            {
                var prefix = Path.GetFileName(sample.SourcePath) + "：";
                if (!validPaths.Contains(sample.SourcePath)) errors.Add(prefix + "不属于当前有效批次。");
                if (!Enum.IsDefined(typeof(RepeatType), sample.RepeatType)) errors.Add(prefix + "重复类型无效。");
                double ph;
                var validPh = AscParser.TryParseFinite(sample.PhText, out ph);
                if (!string.IsNullOrWhiteSpace(sample.PhText) && !validPh) errors.Add(prefix + "已填写的 pH 必须是有限数值。");
                if (!sample.Included)
                {
                    if (string.IsNullOrWhiteSpace(sample.ExclusionReason)) errors.Add(prefix + "排除时必须填写原因。");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(sample.Series)) errors.Add(prefix + "请填写分析系列。");
                if (string.IsNullOrWhiteSpace(sample.PhText)) errors.Add(prefix + "请填写有限的 pH 数值。");
                if (string.IsNullOrWhiteSpace(sample.RepeatId)) errors.Add(prefix + "请填写重复编号。");
                else if (Regex.IsMatch(sample.RepeatId, @"[/\\:;]|\d[._-]\d"))
                    errors.Add(prefix + "编号可能表达嵌套测量；本版只支持单层重复，请先明确设计。");
            }
            if (!samples.Any(x => x.Included)) errors.Add("没有参与分析的文件，请至少纳入一份。");
            foreach (var series in samples.Where(x => x.Included).GroupBy(x => (x.Series ?? "").Trim(), StringComparer.Ordinal))
            {
                if (series.Select(x => x.RepeatType).Distinct().Count() > 1)
                    errors.Add("系列“" + series.Key + "”混用了重复类型；本版不支持混合/嵌套设计。");
                var numericRows = series.Select(x => new { Row = x, Ph = ParseNullable(x.PhText) }).Where(x => x.Ph.HasValue);
                foreach (var group in numericRows.GroupBy(x => x.Ph.Value))
                {
                    foreach (var duplicate in group.GroupBy(x => (x.Row.RepeatId ?? "").Trim(), StringComparer.Ordinal).Where(x => x.Count() > 1))
                        errors.Add("系列“" + series.Key + "” pH=" + group.Key.ToString("R", CultureInfo.InvariantCulture) +
                            " 的重复编号“" + duplicate.Key + "”冲突：" + string.Join("、", duplicate.Select(x => Path.GetFileName(x.Row.SourcePath))));
                }
            }
            return errors.ToArray();
        }

        public static double? ParseNullable(string text)
        {
            double value;
            return AscParser.TryParseFinite(text, out value) ? (double?)value : null;
        }

        public static PhMatchPreview Preview(SpectrumBatch batch, IList<PhSampleMetadata> samples, string wavelengthText)
        {
            EnsureValid(batch, samples);
            double wavelength;
            if (!AscParser.TryParseFinite(wavelengthText, out wavelength)) throw new InvalidOperationException("分析波长必须是有限数值（nm）。");
            var datasets = batch.ValidDatasets.ToDictionary(x => x.SourcePath, StringComparer.OrdinalIgnoreCase);
            var included = samples.Where(x => x.Included).ToArray();
            var missing = included.Where(x => Array.BinarySearch(datasets[x.SourcePath].Wavelengths, wavelength) < 0)
                .Select(x => Path.GetFileName(x.SourcePath)).ToArray();
            return new PhMatchPreview { ParticipatingCount = included.Length, MatchedCount = included.Length - missing.Length, MissingFiles = missing };
        }

        public static PhAnalysisResult Analyze(SpectrumBatch batch, IList<PhSampleMetadata> samples,
            PhAnalysisParameters parameters, string configurationKey, CancellationToken cancellationToken)
        {
            var preview = Preview(batch, samples, parameters.WavelengthText);
            if (preview.MatchedCount == 0) throw new InvalidOperationException("所有参与文件均缺少该精确波长，无法生成结果。");
            if (preview.MissingFiles.Length > 0 && !parameters.AllowMissing)
                throw new InvalidOperationException("有文件缺少目标波长，请先查看名单并明确确认仅使用匹配成功的文件。");
            var wavelength = ParseNullable(parameters.WavelengthText).Value;
            var datasets = batch.ValidDatasets.ToDictionary(x => x.SourcePath, StringComparer.OrdinalIgnoreCase);
            var extractions = new List<PhExtractionResult>();
            foreach (var metadata in samples)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dataset = datasets[metadata.SourcePath];
                var index = Array.BinarySearch(dataset.Wavelengths, wavelength);
                var status = !metadata.Included ? ExtractionStatus.Excluded : index < 0 ? ExtractionStatus.MissingWavelength : ExtractionStatus.Success;
                var intensity = status == ExtractionStatus.Success ? (double?)dataset.Intensities[index] : null;
                if (intensity.HasValue && (double.IsNaN(intensity.Value) || double.IsInfinity(intensity.Value)))
                    throw new InvalidOperationException(Path.GetFileName(metadata.SourcePath) + "：原始强度不是有限数值。");
                extractions.Add(new PhExtractionResult
                {
                    SourceId = dataset.DisplayName, FileName = Path.GetFileName(dataset.SourcePath), SourceFingerprint = SourceIdentity.ForDataset(dataset),
                    Series = (metadata.Series ?? "").Trim(), Ph = ParseNullable(metadata.PhText), PhInput = metadata.PhText ?? "",
                    RepeatId = (metadata.RepeatId ?? "").Trim(), RepeatType = metadata.RepeatType,
                    RequestedInclusion = metadata.Included, TargetWavelength = wavelength, Intensity = intensity, Status = status,
                    Reason = status == ExtractionStatus.Excluded ? metadata.ExclusionReason : status == ExtractionStatus.MissingWavelength
                        ? "原始数据中无数值相等的波长；用户已确认仅纳入匹配成功文件。" : string.Empty
                });
            }
            var groups = new List<PhGroupStatistics>();
            foreach (var series in extractions.Where(x => x.Status == ExtractionStatus.Success).GroupBy(x => x.Series).OrderBy(x => x.Key, StringComparer.Ordinal))
            foreach (var group in series.GroupBy(x => x.Ph.Value).OrderBy(x => x.Key))
            {
                var values = group.Select(x => x.Intensity.Value).ToArray();
                var scale = values.Max(x => Math.Abs(x));
                var meanScaled = scale == 0 ? 0 : CompensatedSum(values.Select(x => x / scale)) / values.Length;
                var mean = meanScaled * scale;
                double? sd = values.Length == 1 ? (double?)null : scale == 0 ? 0 :
                    scale * Math.Sqrt(values.Sum(x => Math.Pow(x / scale - meanScaled, 2) / (values.Length - 1)));
                if (double.IsNaN(mean) || double.IsInfinity(mean) || (sd.HasValue && (double.IsNaN(sd.Value) || double.IsInfinity(sd.Value))))
                    throw new InvalidOperationException("数值范围超出可表示范围，无法可靠计算均值或 SD。");
                groups.Add(new PhGroupStatistics { Series = series.Key, Ph = group.Key, RepeatType = group.First().RepeatType,
                    N = values.Length, Mean = mean, Sd = sd });
            }
            return new PhAnalysisResult { TargetWavelength = wavelength, IntensityUnit = (parameters.IntensityUnit ?? "").Trim(),
                SourceBatchName = new DirectoryInfo(batch.SourceFolder).Name,
                AnalyzedAt = DateTimeOffset.Now, SourceCount = batch.DiscoveredCount, ParseFailedCount = batch.SkippedCount,
                MissingExclusionConfirmed = preview.MissingFiles.Length > 0 && parameters.AllowMissing,
                Samples = extractions.AsReadOnly(), Groups = groups.AsReadOnly(), ConfigurationKey = configurationKey };
        }

        private static void EnsureValid(SpectrumBatch batch, IList<PhSampleMetadata> samples)
        {
            var errors = Validate(batch, samples);
            if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
        }

        private static double CompensatedSum(IEnumerable<double> values)
        {
            // Scaling avoids overflow; compensation retains small terms through cancellation.
            var sum = 0.0;
            var correction = 0.0;
            foreach (var value in values)
            {
                var next = sum + value;
                correction += Math.Abs(sum) >= Math.Abs(value) ? (sum - next) + value : (value - next) + sum;
                sum = next;
            }
            return sum + correction;
        }
    }

    // Configuration signatures also detect edits made without a WPF change notification.
    public sealed class PhAnalysisSession
    {
        private readonly SpectrumBatch batch;
        private string confirmedKey;
        public PhAnalysisSession(SpectrumBatch batch)
        {
            this.batch = batch;
            Samples = batch.ValidDatasets.Select(PhAnalysisService.Suggest).ToList();
            Parameters = new PhAnalysisParameters { WavelengthText = "", IntensityUnit = "" };
        }
        public IList<PhSampleMetadata> Samples { get; private set; }
        public PhAnalysisParameters Parameters { get; private set; }
        public PhAnalysisResult Result { get; private set; }
        public bool GroupingConfirmed { get { return confirmedKey != null && confirmedKey == GroupKey(); } }
        public bool HasCurrentResult { get { return GroupingConfirmed && Result != null && Result.ConfigurationKey == ConfigurationKey(); } }
        public void Invalidate() { Result = null; Parameters.AllowMissing = false; }
        public void RevokeGrouping() { confirmedKey = null; Invalidate(); }
        public string[] ConfirmGrouping()
        {
            var errors = PhAnalysisService.Validate(batch, Samples);
            if (errors.Length == 0) confirmedKey = GroupKey();
            else confirmedKey = null;
            return errors;
        }
        public PhMatchPreview Preview()
        {
            if (!GroupingConfirmed) throw new InvalidOperationException("请先检查并明确确认样品分组。");
            return PhAnalysisService.Preview(batch, Samples, Parameters.WavelengthText);
        }
        public PhAnalysisResult Calculate(CancellationToken cancellationToken)
        {
            if (!GroupingConfirmed) throw new InvalidOperationException("分组已改变，请重新确认。");
            var key = ConfigurationKey();
            var result = PhAnalysisService.Analyze(batch, Samples.Select(x => x.Copy()).ToArray(), Parameters.Copy(), key, cancellationToken);
            if (key != ConfigurationKey()) throw new InvalidOperationException("参数已改变，请重新分析。");
            Result = result;
            return result;
        }
        public PhAnalysisResult RequireCurrentResult()
        {
            if (!HasCurrentResult) throw new InvalidOperationException("结果已失效，请确认当前分组和波长后重新分析。");
            return Result;
        }
        private string GroupKey()
        {
            var builder = new StringBuilder();
            foreach (var row in Samples)
            {
                foreach (var value in new[] { row.SourcePath, row.Series, row.PhText, row.RepeatId,
                    row.RepeatType.ToString(), row.Included.ToString(), row.ExclusionReason })
                {
                    var text = value ?? "";
                    builder.Append(text.Length).Append(':').Append(text);
                }
            }
            return builder.ToString();
        }
        private string ConfigurationKey()
        {
            return GroupKey() + "|" + Parameters.WavelengthText + "|" + Parameters.IntensityUnit + "|" + Parameters.AllowMissing;
        }
    }
}
