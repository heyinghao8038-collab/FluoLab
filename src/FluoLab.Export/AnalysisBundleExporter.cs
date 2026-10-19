using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using FluoLab.Core;

namespace FluoLab.Export
{
    public static class AnalysisBundleExporter
    {
        public static void Export(PhAnalysisResult result, SpectrumBatch batch, string path, bool overwrite,
            bool includeRaw, bool synthetic, Action<string, string> chartWriter)
        {
            if (result == null || result.Groups.Count == 0) throw new InvalidOperationException("没有当前有效分析结果。");
            AtomicOutput.Write(path, overwrite, delegate(string temporary)
            {
                var staging = Path.Combine(Path.GetDirectoryName(temporary), ".fluolab-bundle-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging);
                try
                {
                    var prefix = synthetic ? "模拟数据_" : "";
                    var workbook = Path.Combine(staging, prefix + "pH分析.xlsx");
                    new WorkbookExporter().ExportAnalysis(result, workbook, false);
                    var png = Path.Combine(staging, prefix + "pH响应.png"); var svg = Path.Combine(staging, prefix + "pH响应.svg");
                    chartWriter(png, svg);
                    var manifest = Path.Combine(staging, "源文件指纹.xml");
                    using (var xml = XmlWriter.Create(manifest, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
                    {
                        xml.WriteStartElement("FluoLabSources"); xml.WriteAttributeString("softwareVersion", PhAnalysisResult.SoftwareVersion);
                        xml.WriteAttributeString("synthetic", synthetic.ToString());
                        foreach (var sample in result.Samples)
                        {
                            xml.WriteStartElement("Source"); xml.WriteAttributeString("file", sample.FileName);
                            xml.WriteAttributeString("fingerprint", sample.SourceFingerprint); xml.WriteAttributeString("status", PhAnalysisService.StatusLabel(sample.Status));
                            xml.WriteEndElement();
                        }
                        xml.WriteEndElement();
                    }
                    var readme = Path.Combine(staging, "分析说明.txt");
                    File.WriteAllText(readme, (synthetic ? "本包为明确标注的模拟数据，不是实验结果。\r\n" : "本包为本地实验分析结果，请确认分享权限。\r\n") +
                        "FluoLab " + PhAnalysisResult.SoftwareVersion + "\r\n分析时间：" + result.AnalyzedAt.ToString("O", CultureInfo.InvariantCulture) +
                        "\r\n目标波长：" + result.TargetWavelength.ToString("R", CultureInfo.InvariantCulture) + " nm\r\n" +
                        "精确原始点匹配。按系列和 pH 分组，n 为测量次数，误差棒为均值±样本 SD；n=1 的 SD 不可计算。\r\n" +
                        "分析表、PNG 和真实矢量 SVG 来自同一当前结果。缺失和人工排除见样品明细与分析记录。\r\n" +
                        "指纹 file-sha256 为原文件字节 SHA-256；spectrum-sha256 为内存模拟光谱的 binary64 波长/强度序列 SHA-256。\r\n" +
                        (includeRaw ? "用户明确选择包含原始 ASC；已核对内容指纹。\r\n" : "不包含原始 ASC，未上传任何数据。\r\n"), new UTF8Encoding(true));
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
                    {
                        foreach (var file in Directory.GetFiles(staging)) AddFile(zip, file, Path.GetFileName(file));
                        if (includeRaw)
                        {
                            foreach (var sample in result.Samples)
                            {
                                var data = batch.ValidDatasets.Single(x => x.DisplayName == sample.SourceId);
                                using (var input = new FileStream(data.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                                using (var hash = System.Security.Cryptography.SHA256.Create())
                                {
                                    var actual = "file-sha256:" + BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
                                    if (actual != sample.SourceFingerprint) throw new IOException(sample.FileName + "：源文件已改变，请重新导入后分析。");
                                    input.Position = 0;
                                    using (var entry = zip.CreateEntry("ASC/" + Path.GetFileName(data.SourcePath), CompressionLevel.Optimal).Open()) input.CopyTo(entry);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    // The staging path is a project-owned GUID sibling of the atomic output, never a user-selected directory.
                    if (Directory.Exists(staging)) Directory.Delete(staging, true);
                }
            });
        }
        private static void AddFile(ZipArchive zip, string path, string name)
        {
            using (var input = File.OpenRead(path))
            using (var entry = zip.CreateEntry(name, CompressionLevel.Optimal).Open()) input.CopyTo(entry);
        }
    }
}
