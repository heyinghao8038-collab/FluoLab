using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace FluoLab.Core
{
    public static class SourceIdentity
    {
        public static string FileHash(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var hash = SHA256.Create()) return "file-sha256:" + Hex(hash.ComputeHash(stream));
        }
        public static string ForDataset(SpectrumDataset dataset)
        {
            if (!string.IsNullOrEmpty(dataset.SourceFingerprint)) return dataset.SourceFingerprint;
            using (var hash = SHA256.Create())
            using (var memory = new MemoryStream())
            {
                using (var writer = new BinaryWriter(memory, Encoding.UTF8, true))
                    for (var i = 0; i < dataset.PointCount; i++) { writer.Write(dataset.Wavelengths[i]); writer.Write(dataset.Intensities[i]); }
                memory.Position = 0;
                return "spectrum-sha256:" + Hex(hash.ComputeHash(memory));
            }
        }
        internal static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
    }

    public static class GroupingTransfer
    {
        private static readonly string[] Headers = { "源文件名", "分析系列", "pH", "重复编号", "重复类型", "参与分析", "排除原因" };
        public static string Copy(IEnumerable<PhSampleMetadata> samples)
        {
            var source = samples.ToArray();
            foreach (var sample in source)
            {
                double ph;
                if (!string.IsNullOrWhiteSpace(sample.PhText) && !AscParser.TryParseFinite(sample.PhText, out ph))
                    throw new InvalidOperationException("部分 pH 文本不是有限数值，请先修正或直接编辑，避免表格软件将其解释为公式。");
            }
            if (source.SelectMany(x => new[] { Path.GetFileName(x.SourcePath), x.Series, x.RepeatId, x.ExclusionReason })
                .Any(x => !string.IsNullOrWhiteSpace(x) && "=+-@".IndexOf(x.TrimStart()[0]) >= 0))
                throw new InvalidOperationException("部分文本以公式触发字符开头。为避免 Excel/WPS 自动执行公式，请直接在分组表编辑这些文件，或使用安全的分析 XLSX 导出。");
            var lines = new List<string> { string.Join("\t", Headers) };
            lines.AddRange(source.Select(x => string.Join("\t", new[] { Path.GetFileName(x.SourcePath), x.Series, x.PhText, x.RepeatId,
                PhAnalysisService.RepeatLabel(x.RepeatType), x.Included ? "是" : "否", x.ExclusionReason }.Select(Quote))));
            return string.Join("\r\n", lines);
        }
        public static PhSampleMetadata[] Parse(string text, IList<PhSampleMetadata> current)
        {
            if (text == null || text.Length > 1024 * 1024) throw new InvalidOperationException("粘贴内容为空或超过 1 MB，请分批粘贴。");
            var table = ReadTsv(text);
            if (table.Count < 2 || !table[0].SequenceEqual(Headers))
                throw new InvalidOperationException("请先复制分组模板，在 Excel/WPS 中编辑，并连同七列表头一起粘贴。列顺序：" + string.Join("、", Headers));
            var lookup = current.ToDictionary(x => Path.GetFileName(x.SourcePath), StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var updates = new List<PhSampleMetadata>();
            for (var i = 1; i < table.Count; i++)
            {
                var cells = table[i]; PhSampleMetadata source;
                if (cells.Length != 7) throw new InvalidOperationException("粘贴第 " + (i + 1) + " 行不是七列，未修改任何数据。");
                if (cells[0].Length > 255 || cells[0] != Path.GetFileName(cells[0])) throw new InvalidOperationException("粘贴第 " + (i + 1) + " 行的源文件名无效，须为不含路径的文件名。");
                if (!lookup.TryGetValue(cells[0], out source) || !seen.Add(cells[0]))
                    throw new InvalidOperationException("源文件名不存在或重复：" + cells[0] + "。未修改任何数据。");
                double value;
                if (!string.IsNullOrWhiteSpace(cells[2]) && !AscParser.TryParseFinite(cells[2], out value))
                    throw new InvalidOperationException(cells[0] + "：pH 不是有限数值，未修改任何数据。");
                if (cells[4] != "未确认" && cells[4] != "技术重复" && cells[4] != "独立实验重复")
                    throw new InvalidOperationException(cells[0] + "：重复类型须为未确认、技术重复或独立实验重复。");
                if (cells[5] != "是" && cells[5] != "否") throw new InvalidOperationException(cells[0] + "：参与分析须填写是或否。");
                var row = source.Copy(); row.Series = cells[1]; row.PhText = cells[2]; row.RepeatId = cells[3];
                row.RepeatType = cells[4] == "技术重复" ? RepeatType.Technical : cells[4] == "独立实验重复" ? RepeatType.Independent : RepeatType.Unconfirmed;
                row.Included = cells[5] == "是"; row.ExclusionReason = cells[6]; updates.Add(row);
            }
            return updates.ToArray();
        }
        private static string Quote(string text) { return "\"" + (text ?? "").Replace("\"", "\"\"") + "\""; }
        private static List<string[]> ReadTsv(string text)
        {
            var result = new List<string[]>(); var row = new List<string>(); var cell = new StringBuilder(); var quoted = false; var closedQuote = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (closedQuote && c != '\t' && c != '\r' && c != '\n') throw new InvalidOperationException("粘贴文本的引号后包含额外字符。");
                if (c == '"')
                {
                    if (quoted && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (quoted || cell.Length == 0) { quoted = !quoted; if (!quoted) closedQuote = true; }
                    else throw new InvalidOperationException("粘贴文本的引号格式不正确。");
                }
                else if (!quoted && (c == '\t' || c == '\r' || c == '\n'))
                {
                    row.Add(cell.ToString()); cell.Length = 0;
                    closedQuote = false;
                    if (c != '\t') { result.Add(row.ToArray()); row.Clear(); if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; }
                }
                else cell.Append(c);
                if (result.Count > 10000 || cell.Length > 32767) throw new InvalidOperationException("粘贴行数或单元格长度过大，请分批处理。");
            }
            if (quoted) throw new InvalidOperationException("粘贴文本的引号未闭合。");
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); result.Add(row.ToArray()); }
            return result;
        }
    }

    public sealed class WorkProjectSample
    {
        public string FileName { get; set; }
        public string Fingerprint { get; set; }
        public PhSampleMetadata Metadata { get; set; }
    }
    public sealed class WorkProject
    {
        public string SourceFolder { get; set; }
        public bool Synthetic { get; set; }
        public string WavelengthText { get; set; }
        public string IntensityUnit { get; set; }
        public List<WorkProjectSample> Samples { get; set; }
    }
    public static class WorkProjectStore
    {
        public static WorkProject Capture(SpectrumBatch batch, PhAnalysisSession session, string projectPath, bool synthetic)
        {
            var root = new Uri(Path.GetDirectoryName(Path.GetFullPath(projectPath)) + Path.DirectorySeparatorChar);
            var relative = root.MakeRelativeUri(new Uri(batch.SourceFolder + Path.DirectorySeparatorChar));
            var sameDrive = string.Equals(Path.GetPathRoot(batch.SourceFolder), Path.GetPathRoot(Path.GetFullPath(projectPath)), StringComparison.OrdinalIgnoreCase);
            return new WorkProject { Synthetic = synthetic,
                SourceFolder = synthetic || !sameDrive || relative.IsAbsoluteUri ? "" : Uri.UnescapeDataString(relative.ToString()),
                WavelengthText = session.Parameters.WavelengthText, IntensityUnit = session.Parameters.IntensityUnit,
                Samples = session.Samples.Select(x => new WorkProjectSample { FileName = Path.GetFileName(x.SourcePath), Metadata = x.Copy(),
                    Fingerprint = SourceIdentity.ForDataset(batch.ValidDatasets.Single(d => string.Equals(d.SourcePath, x.SourcePath, StringComparison.OrdinalIgnoreCase))) }).ToList() };
        }
        public static byte[] Serialize(WorkProject project)
        {
            using (var memory = new MemoryStream())
            {
                using (var xml = XmlWriter.Create(memory, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false }))
                {
                    xml.WriteStartElement("FluoLabProject"); xml.WriteAttributeString("version", "1");
                    xml.WriteElementString("SourceFolder", project.SourceFolder); xml.WriteElementString("Synthetic", project.Synthetic.ToString());
                    xml.WriteElementString("Wavelength", project.WavelengthText); xml.WriteElementString("Unit", project.IntensityUnit);
                    xml.WriteStartElement("Samples");
                    foreach (var sample in project.Samples)
                    {
                        xml.WriteStartElement("Sample"); xml.WriteAttributeString("file", sample.FileName); xml.WriteAttributeString("fingerprint", sample.Fingerprint);
                        var m = sample.Metadata;
                        xml.WriteElementString("Series", m.Series); xml.WriteElementString("Ph", m.PhText); xml.WriteElementString("RepeatId", m.RepeatId);
                        xml.WriteElementString("RepeatType", ((int)m.RepeatType).ToString(CultureInfo.InvariantCulture));
                        xml.WriteElementString("Included", m.Included.ToString()); xml.WriteElementString("Reason", m.ExclusionReason); xml.WriteEndElement();
                    }
                    xml.WriteEndElement(); xml.WriteEndElement();
                }
                return memory.ToArray();
            }
        }
        public static WorkProject Read(string path)
        {
            if (new FileInfo(path).Length > 5 * 1024 * 1024) throw new InvalidOperationException("工作项目超过 5 MB，无法打开。");
            var document = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 5 * 1024 * 1024 })) document.Load(reader);
            var root = document.DocumentElement;
            if (root == null || root.Name != "FluoLabProject" || root.GetAttribute("version") != "1") throw new InvalidOperationException("工作项目格式或版本不支持。");
            var result = new WorkProject { SourceFolder = Text(root, "SourceFolder"), Synthetic = Boolean(Text(root, "Synthetic")),
                WavelengthText = Text(root, "Wavelength"), IntensityUnit = Text(root, "Unit"), Samples = new List<WorkProjectSample>() };
            if (result.IntensityUnit.Length > 80) throw new InvalidOperationException("工作项目的单位字段超过 80 字符。");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlElement node in root.SelectNodes("Samples/Sample"))
            {
                var name = node.GetAttribute("file"); var hash = node.GetAttribute("fingerprint"); int type;
                if (name.Length == 0 || name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !seen.Add(name) || hash.Length > 100 || hash.Length < 64 ||
                    !int.TryParse(Text(node, "RepeatType"), out type) || !Enum.IsDefined(typeof(RepeatType), type)) throw new InvalidOperationException("工作项目中的源文件标识或重复类型无效。");
                result.Samples.Add(new WorkProjectSample { FileName = name, Fingerprint = hash, Metadata = new PhSampleMetadata {
                    Series = Text(node, "Series"), PhText = Text(node, "Ph"), RepeatId = Text(node, "RepeatId"), RepeatType = (RepeatType)type,
                    Included = Boolean(Text(node, "Included")), ExclusionReason = Text(node, "Reason") } });
                if (result.Samples.Count > 10000) throw new InvalidOperationException("工作项目文件数量超过限制。");
            }
            if (result.Samples.Count == 0) throw new InvalidOperationException("工作项目没有样品。");
            return result;
        }
        public static string ResolveFolder(WorkProject project, string path)
        {
            if (string.IsNullOrWhiteSpace(project.SourceFolder) || Path.IsPathRooted(project.SourceFolder) || project.SourceFolder.IndexOf(':') >= 0) throw new InvalidOperationException("源目录需重新定位，请选择对应的本地 ASC 文件夹。");
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), project.SourceFolder.Replace('/', Path.DirectorySeparatorChar)));
        }
        public static PhAnalysisSession Restore(WorkProject project, SpectrumBatch batch)
        {
            var datasets = batch.ValidDatasets.ToDictionary(x => Path.GetFileName(x.SourcePath), StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();
            foreach (var row in project.Samples)
            {
                SpectrumDataset data;
                if (!datasets.TryGetValue(row.FileName, out data)) errors.Add(row.FileName + "：缺失或解析失败");
                else if (SourceIdentity.ForDataset(data) != row.Fingerprint) errors.Add(row.FileName + "：内容指纹已改变");
                else if (row.Fingerprint.StartsWith("file-sha256:", StringComparison.Ordinal) &&
                    (!File.Exists(data.SourcePath) || SourceIdentity.FileHash(data.SourcePath) != row.Fingerprint)) errors.Add(row.FileName + "：磁盘源文件在导入后已改变或缺失，请重新导入");
            }
            errors.AddRange(datasets.Keys.Where(name => !project.Samples.Any(x => string.Equals(x.FileName, name, StringComparison.OrdinalIgnoreCase))).Select(x => x + "：新增文件，未包含在工作项目中"));
            if (errors.Count > 0) throw new InvalidOperationException("源文件与工作项目不一致，未恢复分组。请核对或选择原始批次：\n" + string.Join("\n", errors));
            var session = new PhAnalysisSession(batch);
            foreach (var row in project.Samples)
            {
                var target = session.Samples.Single(x => string.Equals(Path.GetFileName(x.SourcePath), row.FileName, StringComparison.OrdinalIgnoreCase));
                Apply(row.Metadata, target);
            }
            session.Parameters.WavelengthText = project.WavelengthText; session.Parameters.IntensityUnit = project.IntensityUnit;
            return session;
        }
        public static void Apply(PhSampleMetadata from, PhSampleMetadata to)
        {
            to.Series = from.Series; to.PhText = from.PhText; to.RepeatId = from.RepeatId; to.RepeatType = from.RepeatType;
            to.Included = from.Included; to.ExclusionReason = from.ExclusionReason;
        }
        private static bool Boolean(string text) { bool value; if (!bool.TryParse(text, out value)) throw new InvalidOperationException("工作项目中的参与状态无效。"); return value; }
        private static string Text(XmlElement element, string name)
        {
            var nodes = element.SelectNodes(name);
            if (nodes.Count != 1 || nodes[0].InnerText.Length > 32767) throw new InvalidOperationException("工作项目字段缺失、重复或过长：" + name);
            return nodes[0].InnerText;
        }
    }
}
