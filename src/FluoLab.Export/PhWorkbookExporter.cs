using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using FluoLab.Core;

namespace FluoLab.Export
{
    public static class AtomicOutput
    {
        public static void Write(string path, bool overwrite, Action<string> writeTemporary)
        {
            var fullPath = Path.GetFullPath(path);
            var folder = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("目标文件夹不存在。");
            if (File.Exists(fullPath) && !overwrite) throw new IOException("目标文件已存在，请明确确认覆盖。");
            var temporary = Path.Combine(folder, ".fluolab-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                writeTemporary(temporary);
                if (File.Exists(fullPath))
                {
                    if (!overwrite) throw new IOException("目标文件已存在，请重新确认覆盖。");
                    File.Replace(temporary, fullPath, null, true);
                }
                else File.Move(temporary, fullPath);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    public sealed partial class WorkbookExporter
    {
        public void ExportAnalysis(PhAnalysisResult result, string path, bool overwrite)
        {
            if (result == null || result.Groups.Count == 0) throw new InvalidOperationException("没有可导出的分析结果。");
            AtomicOutput.Write(path, overwrite, delegate(string temporary)
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, false, Utf8WithoutBom))
                {
                    WriteAnalysisTypes(archive);
                    WriteRootRelationships(archive);
                    WriteCoreProperties(archive, result.AnalyzedAt);
                    WriteAppProperties(archive);
                    WriteStyles(archive, true);
                    WriteAnalysisBook(archive);
                    var sampleRows = result.Samples.Select(x => new object[] {
                        x.SourceId, x.FileName, x.Series, x.Ph, x.RepeatId, PhAnalysisService.RepeatLabel(x.RepeatType),
                        x.RequestedInclusion ? "参与" : "排除", x.TargetWavelength, x.Intensity,
                        PhAnalysisService.StatusLabel(x.Status), x.Reason, x.PhInput, x.SourceFingerprint }).ToList();
                    WriteAnalysisTable(archive, 1,
                        new[] { "源文件标识", "源文件名", "分析系列", "pH", "重复编号", "重复类型", "分析参与状态", "目标波长 (nm)", "原始荧光强度", "提取状态", "原因 / 决定", "pH 输入原文", "源文件内容指纹" },
                        new double[] { 44, 44, 30, 12, 14, 20, 18, 18, 24, 20, 64, 20, 80 }, sampleRows);
                    WriteAnalysisTable(archive, 2,
                        new[] { "分析系列", "pH", "重复类型", "测量次数 n", "算术平均值", "样本标准差 SD", "SD 说明" },
                        new double[] { 34, 14, 22, 18, 26, 26, 32 },
                        result.Groups.Select(x => new object[] { x.Series, x.Ph, PhAnalysisService.RepeatLabel(x.RepeatType), x.N, x.Mean, x.Sd, x.SdNote }).ToList());
                    var records = new List<object[]> {
                        new object[] { "分析", "pH 响应描述统计" },
                        new object[] { "软件版本", PhAnalysisResult.SoftwareVersion },
                        new object[] { "源批次文件夹名", result.SourceBatchName },
                        new object[] { "处理时间", result.AnalyzedAt.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) },
                        new object[] { "目标波长 (nm)", result.TargetWavelength },
                        new object[] { "已确认强度单位", string.IsNullOrWhiteSpace(result.IntensityUnit) ? "未填写，图中使用“荧光强度”" : result.IntensityUnit },
                        new object[] { "匹配规则", "原始完整波长数组，double 数值相等；不寻找最近点、不插值、不使用绘图降采样。" },
                        new object[] { "统计方法", "按分析系列和 pH 数值分组；算术平均值；样本 SD，分母 n−1；n=1 的 SD 不可计算。" },
                        new object[] { "n 的定义", "测量次数；未确认重复类型时不说明实验独立性。技术重复不能当作独立实验重复。" },
                        new object[] { "数值显示", "均值、SD 和强度通常显示六位小数，极小/极大值使用科学计数法；单元格底层保留完整值，未提前舍入。" },
                        new object[] { "分组确认", "用户已检查文件名建议及编辑值，确认各系列除 pH 外条件一致，且为单层重复设计。" },
                        new object[] { "发现 ASC", result.SourceCount },
                        new object[] { "解析失败", result.ParseFailedCount },
                        new object[] { "有效光谱", result.Samples.Count },
                        new object[] { "用户设置参与", result.Samples.Count(x => x.RequestedInclusion) },
                        new object[] { "成功提取并纳入", result.Samples.Count(x => x.Status == ExtractionStatus.Success) },
                        new object[] { "用户排除", result.Samples.Count(x => x.Status == ExtractionStatus.Excluded) },
                        new object[] { "缺少目标波长", result.Samples.Count(x => x.Status == ExtractionStatus.MissingWavelength) },
                        new object[] { "缺失文件决定", result.MissingExclusionConfirmed ? "已逐文件展示缺失名单，用户明确确认仅使用匹配成功文件。" : "无缺失波长文件需排除。" },
                        new object[] { "边界", "仅描述统计；无拟合、显著性检验、自动去异常或预处理；原始光谱未修改。" },
                        new object[] { "映射规则", "源文件标识等于原始“汇总数据”中的列标题（不含 .ASC）；文件名保留完整扩展名。" }
                    };
                    foreach (var row in result.Samples.Where(x => x.Status != ExtractionStatus.Success))
                        records.Add(new object[] { row.FileName, PhAnalysisService.StatusLabel(row.Status) + "：" + row.Reason });
                    WriteAnalysisTable(archive, 3, new[] { "记录项", "内容" }, new double[] { 44, 110 }, records);
                }
            });
        }

        private static void WriteAnalysisTypes(ZipArchive archive)
        {
            using (var writer = CreateEntryWriter(archive, "[Content_Types].xml"))
            {
                writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                WriteDefaultContentType(writer, "rels", "application/vnd.openxmlformats-package.relationships+xml");
                WriteDefaultContentType(writer, "xml", "application/xml");
                WriteOverrideContentType(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
                for (var index = 1; index <= 3; index++)
                    WriteOverrideContentType(writer, "/xl/worksheets/sheet" + index + ".xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                WriteOverrideContentType(writer, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
                WriteOverrideContentType(writer, "/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml");
                WriteOverrideContentType(writer, "/docProps/app.xml", "application/vnd.openxmlformats-officedocument.extended-properties+xml");
                writer.WriteEndElement();
            }
        }

        private static void WriteAnalysisBook(ZipArchive archive)
        {
            using (var writer = CreateEntryWriter(archive, "xl/workbook.xml"))
            {
                writer.WriteStartElement("workbook", SpreadsheetNamespace);
                writer.WriteStartElement("sheets");
                WriteSheetReference(writer, "样品明细", 1, "rId1");
                WriteSheetReference(writer, "分组统计", 2, "rId2");
                WriteSheetReference(writer, "分析记录", 3, "rId3");
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            using (var writer = CreateEntryWriter(archive, "xl/_rels/workbook.xml.rels"))
            {
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                for (var index = 1; index <= 3; index++)
                    WriteRelationship(writer, "rId" + index, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet" + index + ".xml");
                WriteRelationship(writer, "rId4", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml");
                writer.WriteEndElement();
            }
        }

        private static void WriteAnalysisTable(ZipArchive archive, int sheetNumber, string[] headers, double[] widths, IList<object[]> rows)
        {
            using (var writer = CreateEntryWriter(archive, "xl/worksheets/sheet" + sheetNumber + ".xml"))
            {
                writer.WriteStartElement("worksheet", SpreadsheetNamespace);
                WriteFrozenPane(writer, "B2", "1", "1");
                writer.WriteStartElement("cols");
                for (var col = 0; col < widths.Length; col++) WriteColumn(writer, col + 1, col + 1, widths[col]);
                writer.WriteEndElement();
                writer.WriteStartElement("sheetData");
                var all = new List<object[]> { headers.Cast<object>().ToArray() };
                all.AddRange(rows);
                for (var row = 0; row < all.Count; row++)
                {
                    writer.WriteStartElement("row");
                    writer.WriteAttributeString("r", (row + 1).ToString(CultureInfo.InvariantCulture));
                    var height = row == 0 ? 32.0 : 32.0;
                    for (var col = 0; col < all[row].Length; col++)
                    {
                        var text = all[row][col] as string;
                        if (text != null)
                        {
                            var units = text.Sum(character => character > 255 ? 2 : 1);
                            height = Math.Max(height, Math.Ceiling(units / Math.Max(8.0, widths[col] - 2)) * 15 + 8);
                        }
                    }
                    writer.WriteAttributeString("ht", Math.Min(409, height).ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("customHeight", "1");
                    for (var col = 0; col < all[row].Length; col++)
                    {
                        var value = all[row][col];
                        if (value == null) continue;
                        var reference = GetCellReference(col + 1, row + 1);
                        if (value is double || value is int)
                        {
                            var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                            var style = number == Math.Truncate(number) ? 5 : headers[col] == "pH" || headers[col].Contains("波长") ? 2 : 4;
                            if (Math.Abs(number) >= 1e12 || (number != 0 && Math.Abs(number) < 0.000001)) style = 6;
                            WriteNumberCell(writer, reference, number, style);
                        }
                        else WriteInlineStringCell(writer, reference, Convert.ToString(value, CultureInfo.InvariantCulture), row == 0 ? 1 : 3);
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
                writer.WriteStartElement("autoFilter");
                writer.WriteAttributeString("ref", "A1:" + GetCellReference(headers.Length, all.Count));
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
        }
    }
}
