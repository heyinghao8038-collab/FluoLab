using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using FluoLab.Core;

namespace FluoLab.Export
{
    public sealed partial class WorkbookExporter
    {
        private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public void Export(SpectrumBatch batch, string destinationPath, bool overwrite)
        {
            if (batch == null)
            {
                throw new ArgumentNullException("batch");
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new ArgumentException("导出路径不能为空。", "destinationPath");
            }

            var datasets = batch.ValidDatasets;
            if (datasets.Length == 0)
            {
                throw new InvalidOperationException("没有可导出的有效 ASC 数据。");
            }

            var fullPath = Path.GetFullPath(destinationPath);
            var destinationFolder = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(destinationFolder) || !Directory.Exists(destinationFolder))
            {
                throw new DirectoryNotFoundException("导出文件夹不存在。");
            }

            if (!string.Equals(Path.GetExtension(fullPath), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                fullPath += ".xlsx";
            }

            if (File.Exists(fullPath) && !overwrite)
            {
                throw new IOException("目标文件已存在。");
            }

            var tempPath = Path.Combine(
                destinationFolder,
                string.Format(".{0}.{1}.tmp", Path.GetFileName(fullPath), Guid.NewGuid().ToString("N")));

            try
            {
                WritePackage(batch, datasets, tempPath);
                if (File.Exists(fullPath))
                {
                    File.Replace(tempPath, fullPath, null, true);
                }
                else
                {
                    File.Move(tempPath, fullPath);
                }
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        private static void WritePackage(
            SpectrumBatch batch,
            SpectrumDataset[] datasets,
            string tempPath)
        {
            var wavelengths = WavelengthAlignment.BuildUnion(datasets);
            var valueMaps = datasets
                .Select(BuildValueMap)
                .ToArray();

            using (var fileStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, false, Utf8WithoutBom))
            {
                WriteContentTypes(archive);
                WriteRootRelationships(archive);
                WriteCoreProperties(archive, batch.ProcessedAt);
                WriteAppProperties(archive);
                WriteWorkbook(archive);
                WriteWorkbookRelationships(archive);
                WriteStyles(archive);
                WriteSummarySheet(archive, wavelengths, datasets, valueMaps);
                WriteLogSheet(archive, batch);
            }
        }

        private static Dictionary<double, double> BuildValueMap(SpectrumDataset dataset)
        {
            var map = new Dictionary<double, double>(dataset.PointCount);
            for (var index = 0; index < dataset.PointCount; index++)
            {
                map.Add(dataset.Wavelengths[index], dataset.Intensities[index]);
            }

            return map;
        }

        private static XmlWriter CreateEntryWriter(ZipArchive archive, string entryName)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            var stream = entry.Open();
            var settings = new XmlWriterSettings
            {
                Encoding = Utf8WithoutBom,
                Indent = false,
                CloseOutput = true,
                CheckCharacters = true
            };
            return XmlWriter.Create(stream, settings);
        }

        private static void WriteContentTypes(ZipArchive archive)
        {
            using (var writer = CreateEntryWriter(archive, "[Content_Types].xml"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                WriteDefaultContentType(writer, "rels", "application/vnd.openxmlformats-package.relationships+xml");
                WriteDefaultContentType(writer, "xml", "application/xml");
                WriteOverrideContentType(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
                WriteOverrideContentType(writer, "/xl/worksheets/sheet1.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                WriteOverrideContentType(writer, "/xl/worksheets/sheet2.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
                WriteOverrideContentType(writer, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
                WriteOverrideContentType(writer, "/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml");
                WriteOverrideContentType(writer, "/docProps/app.xml", "application/vnd.openxmlformats-officedocument.extended-properties+xml");
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteDefaultContentType(XmlWriter writer, string extension, string contentType)
        {
            writer.WriteStartElement("Default");
            writer.WriteAttributeString("Extension", extension);
            writer.WriteAttributeString("ContentType", contentType);
            writer.WriteEndElement();
        }

        private static void WriteOverrideContentType(XmlWriter writer, string partName, string contentType)
        {
            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", partName);
            writer.WriteAttributeString("ContentType", contentType);
            writer.WriteEndElement();
        }

        private static void WriteRootRelationships(ZipArchive archive)
        {
            using (var writer = CreateEntryWriter(archive, "_rels/.rels"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                WriteRelationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
                WriteRelationship(writer, "rId2", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml");
                WriteRelationship(writer, "rId3", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties", "docProps/app.xml");
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteWorkbookRelationships(ZipArchive archive)
        {
            using (var writer = CreateEntryWriter(archive, "xl/_rels/workbook.xml.rels"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                WriteRelationship(writer, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet1.xml");
                WriteRelationship(writer, "rId2", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "worksheets/sheet2.xml");
                WriteRelationship(writer, "rId3", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml");
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteRelationship(XmlWriter writer, string id, string type, string target)
        {
            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", id);
            writer.WriteAttributeString("Type", type);
            writer.WriteAttributeString("Target", target);
            writer.WriteEndElement();
        }

        private static void WriteCoreProperties(ZipArchive archive, DateTimeOffset processedAt)
        {
            using (var writer = CreateEntryWriter(archive, "docProps/core.xml"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("cp", "coreProperties", "http://schemas.openxmlformats.org/package/2006/metadata/core-properties");
                writer.WriteAttributeString("xmlns", "dc", null, "http://purl.org/dc/elements/1.1/");
                writer.WriteAttributeString("xmlns", "dcterms", null, "http://purl.org/dc/terms/");
                writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteElementString("dc", "title", null, "FluoLab ASC 汇总");
                writer.WriteElementString("dc", "creator", null, "FluoLab");
                writer.WriteStartElement("dcterms", "created", null);
                writer.WriteAttributeString("xsi", "type", null, "dcterms:W3CDTF");
                writer.WriteString(processedAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteAppProperties(ZipArchive archive)
        {
            using (var writer = CreateEntryWriter(archive, "docProps/app.xml"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Properties", "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties");
                writer.WriteElementString("Application", "FluoLab");
                writer.WriteElementString("AppVersion", "1.0");
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteWorkbook(ZipArchive archive)
        {
            using (var writer = CreateEntryWriter(archive, "xl/workbook.xml"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("workbook", SpreadsheetNamespace);
                writer.WriteAttributeString("xmlns", "r", null, "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
                writer.WriteStartElement("bookViews");
                writer.WriteStartElement("workbookView");
                writer.WriteAttributeString("activeTab", "0");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteStartElement("sheets");
                WriteSheetReference(writer, "汇总数据", 1, "rId1");
                WriteSheetReference(writer, "处理日志", 2, "rId2");
                writer.WriteEndElement();
                writer.WriteStartElement("calcPr");
                writer.WriteAttributeString("calcId", "0");
                writer.WriteAttributeString("calcMode", "manual");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteSheetReference(XmlWriter writer, string name, int sheetId, string relationshipId)
        {
            writer.WriteStartElement("sheet");
            writer.WriteAttributeString("name", name);
            writer.WriteAttributeString("sheetId", sheetId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("r", "id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships", relationshipId);
            writer.WriteEndElement();
        }

        private static void WriteStyles(ZipArchive archive, bool analysis = false)
        {
            using (var writer = CreateEntryWriter(archive, "xl/styles.xml"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("styleSheet", SpreadsheetNamespace);
                writer.WriteStartElement("numFmts");
                writer.WriteAttributeString("count", analysis ? "2" : "1");
                writer.WriteStartElement("numFmt");
                writer.WriteAttributeString("numFmtId", "164");
                writer.WriteAttributeString("formatCode", "0.###############");
                writer.WriteEndElement();
                if (analysis)
                {
                    writer.WriteStartElement("numFmt");
                    writer.WriteAttributeString("numFmtId", "165");
                    writer.WriteAttributeString("formatCode", "0.000000");
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();

                writer.WriteStartElement("fonts");
                writer.WriteAttributeString("count", "2");
                WriteFont(writer, false, "1D1D1F");
                WriteFont(writer, true, "FFFFFF");
                writer.WriteEndElement();

                writer.WriteStartElement("fills");
                writer.WriteAttributeString("count", "3");
                WritePatternFill(writer, "none", null);
                WritePatternFill(writer, "gray125", null);
                WritePatternFill(writer, "solid", "0071E3");
                writer.WriteEndElement();

                writer.WriteStartElement("borders");
                writer.WriteAttributeString("count", "2");
                WriteBorder(writer, false);
                WriteBorder(writer, true);
                writer.WriteEndElement();

                writer.WriteStartElement("cellStyleXfs");
                writer.WriteAttributeString("count", "1");
                WriteXf(writer, 0, 0, 0, false, false);
                writer.WriteEndElement();

                writer.WriteStartElement("cellXfs");
                writer.WriteAttributeString("count", analysis ? "7" : "4");
                WriteXf(writer, 0, 0, 0, false, false);
                WriteXf(writer, 0, 1, 2, true, true);
                WriteXf(writer, 164, 0, 0, true, false);
                WriteWrapXf(writer);
                if (analysis)
                {
                    WriteXf(writer, 165, 0, 0, true, false);
                    WriteXf(writer, 1, 0, 0, true, false);
                    WriteXf(writer, 11, 0, 0, true, false);
                }
                writer.WriteEndElement();

                writer.WriteStartElement("cellStyles");
                writer.WriteAttributeString("count", "1");
                writer.WriteStartElement("cellStyle");
                writer.WriteAttributeString("name", "Normal");
                writer.WriteAttributeString("xfId", "0");
                writer.WriteAttributeString("builtinId", "0");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteFont(XmlWriter writer, bool bold, string rgb)
        {
            writer.WriteStartElement("font");
            writer.WriteStartElement("sz");
            writer.WriteAttributeString("val", "11");
            writer.WriteEndElement();
            writer.WriteStartElement("name");
            writer.WriteAttributeString("val", "Microsoft YaHei");
            writer.WriteEndElement();
            if (bold)
            {
                writer.WriteElementString("b", string.Empty);
            }

            writer.WriteStartElement("color");
            writer.WriteAttributeString("rgb", "FF" + rgb);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WritePatternFill(XmlWriter writer, string patternType, string rgb)
        {
            writer.WriteStartElement("fill");
            writer.WriteStartElement("patternFill");
            writer.WriteAttributeString("patternType", patternType);
            if (rgb != null)
            {
                writer.WriteStartElement("fgColor");
                writer.WriteAttributeString("rgb", "FF" + rgb);
                writer.WriteEndElement();
                writer.WriteStartElement("bgColor");
                writer.WriteAttributeString("indexed", "64");
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteBorder(XmlWriter writer, bool thin)
        {
            writer.WriteStartElement("border");
            WriteBorderSide(writer, "left", thin);
            WriteBorderSide(writer, "right", thin);
            WriteBorderSide(writer, "top", thin);
            WriteBorderSide(writer, "bottom", thin);
            writer.WriteElementString("diagonal", string.Empty);
            writer.WriteEndElement();
        }

        private static void WriteBorderSide(XmlWriter writer, string side, bool thin)
        {
            writer.WriteStartElement(side);
            if (thin)
            {
                writer.WriteAttributeString("style", "thin");
                writer.WriteStartElement("color");
                writer.WriteAttributeString("rgb", "FFD2D2D7");
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        private static void WriteXf(
            XmlWriter writer,
            int numberFormatId,
            int fontId,
            int fillId,
            bool applyNumberFormat,
            bool header)
        {
            writer.WriteStartElement("xf");
            writer.WriteAttributeString("numFmtId", numberFormatId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("fontId", fontId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("fillId", fillId.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("borderId", header ? "1" : "0");
            writer.WriteAttributeString("xfId", "0");
            if (applyNumberFormat)
            {
                writer.WriteAttributeString("applyNumberFormat", "1");
            }

            if (header)
            {
                writer.WriteAttributeString("applyAlignment", "1");
                writer.WriteStartElement("alignment");
                writer.WriteAttributeString("horizontal", "center");
                writer.WriteAttributeString("vertical", "center");
                writer.WriteAttributeString("wrapText", "1");
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        private static void WriteWrapXf(XmlWriter writer)
        {
            writer.WriteStartElement("xf");
            writer.WriteAttributeString("numFmtId", "0");
            writer.WriteAttributeString("fontId", "0");
            writer.WriteAttributeString("fillId", "0");
            writer.WriteAttributeString("borderId", "0");
            writer.WriteAttributeString("xfId", "0");
            writer.WriteAttributeString("applyAlignment", "1");
            writer.WriteStartElement("alignment");
            writer.WriteAttributeString("vertical", "center");
            writer.WriteAttributeString("wrapText", "1");
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteSummarySheet(
            ZipArchive archive,
            double[] wavelengths,
            SpectrumDataset[] datasets,
            Dictionary<double, double>[] valueMaps)
        {
            using (var writer = CreateEntryWriter(archive, "xl/worksheets/sheet1.xml"))
            {
                var lastColumn = GetColumnName(datasets.Length + 1);
                var lastRow = wavelengths.Length + 1;
                writer.WriteStartDocument();
                writer.WriteStartElement("worksheet", SpreadsheetNamespace);
                WriteFrozenPane(writer, "B2", "1", "1");
                writer.WriteStartElement("cols");
                WriteColumn(writer, 1, 1, 18);
                if (datasets.Length > 0)
                {
                    WriteColumn(writer, 2, datasets.Length + 1, 28);
                }

                writer.WriteEndElement();
                writer.WriteStartElement("sheetData");
                writer.WriteStartElement("row");
                writer.WriteAttributeString("r", "1");
                writer.WriteAttributeString("ht", "38");
                writer.WriteAttributeString("customHeight", "1");
                WriteInlineStringCell(writer, "A1", "Wavelength (nm)", 1);
                for (var datasetIndex = 0; datasetIndex < datasets.Length; datasetIndex++)
                {
                    WriteInlineStringCell(
                        writer,
                        GetCellReference(datasetIndex + 2, 1),
                        datasets[datasetIndex].DisplayName,
                        1);
                }

                writer.WriteEndElement();

                for (var rowIndex = 0; rowIndex < wavelengths.Length; rowIndex++)
                {
                    var excelRow = rowIndex + 2;
                    var wavelength = wavelengths[rowIndex];
                    writer.WriteStartElement("row");
                    writer.WriteAttributeString("r", excelRow.ToString(CultureInfo.InvariantCulture));
                    WriteNumberCell(writer, "A" + excelRow.ToString(CultureInfo.InvariantCulture), wavelength, 2);
                    for (var datasetIndex = 0; datasetIndex < datasets.Length; datasetIndex++)
                    {
                        double intensity;
                        if (valueMaps[datasetIndex].TryGetValue(wavelength, out intensity))
                        {
                            WriteNumberCell(
                                writer,
                                GetCellReference(datasetIndex + 2, excelRow),
                                intensity,
                                2);
                        }
                    }

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteStartElement("autoFilter");
                writer.WriteAttributeString("ref", string.Format("A1:{0}{1}", lastColumn, lastRow));
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteLogSheet(ZipArchive archive, SpectrumBatch batch)
        {
            using (var writer = CreateEntryWriter(archive, "xl/worksheets/sheet2.xml"))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("worksheet", SpreadsheetNamespace);
                WriteFrozenPane(writer, "A9", null, "8");
                writer.WriteStartElement("cols");
                WriteColumn(writer, 1, 1, 44);
                WriteColumn(writer, 2, 2, 80);
                WriteColumn(writer, 3, 3, 12);
                WriteColumn(writer, 4, 4, 36);
                writer.WriteEndElement();
                writer.WriteStartElement("sheetData");
                WriteKeyValueRow(writer, 1, "项目", "FluoLab ASC 数据整理");
                WriteKeyValueRow(writer, 2, "源文件夹", batch.SourceFolder);
                WriteKeyValueRow(writer, 3, "运行时间", batch.ProcessedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture));
                WriteKeyValueRow(writer, 4, "发现文件", batch.DiscoveredCount.ToString(CultureInfo.InvariantCulture));
                WriteKeyValueRow(writer, 5, "成功文件", batch.SuccessCount.ToString(CultureInfo.InvariantCulture));
                WriteKeyValueRow(writer, 6, "跳过文件", batch.SkippedCount.ToString(CultureInfo.InvariantCulture));
                WriteKeyValueRow(
                    writer,
                    7,
                    "波长点数",
                    WavelengthAlignment.BuildUnion(batch.ValidDatasets).Length.ToString(CultureInfo.InvariantCulture));

                writer.WriteStartElement("row");
                writer.WriteAttributeString("r", "8");
                WriteInlineStringCell(writer, "A8", "文件名", 1);
                WriteInlineStringCell(writer, "B8", "源路径", 1);
                WriteInlineStringCell(writer, "C8", "状态", 1);
                WriteInlineStringCell(writer, "D8", "说明", 1);
                writer.WriteEndElement();

                for (var index = 0; index < batch.Files.Length; index++)
                {
                    var row = index + 9;
                    var result = batch.Files[index];
                    writer.WriteStartElement("row");
                    writer.WriteAttributeString("r", row.ToString(CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("ht", "30");
                    writer.WriteAttributeString("customHeight", "1");
                    WriteInlineStringCell(writer, "A" + row, result.FileName, 3);
                    WriteInlineStringCell(writer, "B" + row, result.SourcePath, 3);
                    WriteInlineStringCell(
                        writer,
                        "C" + row,
                        result.Status == ProcessingStatus.Success ? "成功" : "跳过",
                        0);
                    WriteInlineStringCell(
                        writer,
                        "D" + row,
                        result.Status == ProcessingStatus.Success
                            ? string.Format("{0} 个数据点", result.Dataset.PointCount)
                            : result.ErrorMessage,
                        3);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteStartElement("autoFilter");
                writer.WriteAttributeString("ref", string.Format("A8:D{0}", Math.Max(8, batch.Files.Length + 8)));
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void WriteFrozenPane(XmlWriter writer, string topLeftCell, string xSplit, string ySplit)
        {
            writer.WriteStartElement("sheetViews");
            writer.WriteStartElement("sheetView");
            writer.WriteAttributeString("workbookViewId", "0");
            writer.WriteStartElement("pane");
            if (xSplit != null)
            {
                writer.WriteAttributeString("xSplit", xSplit);
            }

            if (ySplit != null)
            {
                writer.WriteAttributeString("ySplit", ySplit);
            }

            writer.WriteAttributeString("topLeftCell", topLeftCell);
            writer.WriteAttributeString("activePane", xSplit == null ? "bottomLeft" : "bottomRight");
            writer.WriteAttributeString("state", "frozen");
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteColumn(XmlWriter writer, int min, int max, double width)
        {
            writer.WriteStartElement("col");
            writer.WriteAttributeString("min", min.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("max", max.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("width", width.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }

        private static void WriteKeyValueRow(XmlWriter writer, int row, string key, string value)
        {
            writer.WriteStartElement("row");
            writer.WriteAttributeString("r", row.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("ht", "28");
            writer.WriteAttributeString("customHeight", "1");
            WriteInlineStringCell(writer, "A" + row, key, 1);
            WriteInlineStringCell(writer, "B" + row, value, 3);
            writer.WriteEndElement();
        }

        private static void WriteInlineStringCell(XmlWriter writer, string reference, string value, int styleIndex)
        {
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", reference);
            writer.WriteAttributeString("t", "inlineStr");
            if (styleIndex > 0)
            {
                writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));
            }

            writer.WriteStartElement("is");
            writer.WriteStartElement("t");
            writer.WriteAttributeString("xml", "space", null, "preserve");
            writer.WriteString(value ?? string.Empty);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteNumberCell(XmlWriter writer, string reference, double value, int styleIndex)
        {
            writer.WriteStartElement("c");
            writer.WriteAttributeString("r", reference);
            if (styleIndex > 0)
            {
                writer.WriteAttributeString("s", styleIndex.ToString(CultureInfo.InvariantCulture));
            }

            writer.WriteElementString("v", value.ToString("R", CultureInfo.InvariantCulture));
            writer.WriteEndElement();
        }

        private static string GetCellReference(int column, int row)
        {
            return GetColumnName(column) + row.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetColumnName(int column)
        {
            var name = string.Empty;
            var value = column;
            while (value > 0)
            {
                value--;
                name = (char)('A' + (value % 26)) + name;
                value /= 26;
            }

            return name;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // A failed cleanup must not hide the original export error.
            }
        }
    }
}
