using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading;

namespace FluoLab.Core
{
    public sealed class AscParseException : Exception
    {
        public AscParseException(string message)
            : base(message)
        {
        }

        public AscParseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class AscParser
    {
        private static readonly Regex ColumnSeparator = new Regex(@"\s+", RegexOptions.Compiled);

        public SpectrumDataset Parse(string filePath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("ASC 文件路径不能为空。", "filePath");
            }

            var fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath))
            {
                throw new AscParseException("文件不存在或已被移动。");
            }

            try
            {
                using (var stream = new FileStream(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    65536,
                    FileOptions.SequentialScan))
                using (var hash = SHA256.Create())
                using (var hashed = new CryptoStream(stream, hash, CryptoStreamMode.Read))
                using (var reader = new StreamReader(hashed, Encoding.Default, true, 65536))
                {
                    var dataset = ParseReader(fullPath, reader, cancellationToken);
                    dataset.SourceFingerprint = "file-sha256:" + SourceIdentity.Hex(hash.Hash);
                    return dataset;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (AscParseException)
            {
                throw;
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new AscParseException("没有权限读取该文件。", exception);
            }
            catch (IOException exception)
            {
                throw new AscParseException("文件正被占用或读取失败。", exception);
            }
        }

        private static SpectrumDataset ParseReader(
            string fullPath,
            TextReader reader,
            CancellationToken cancellationToken)
        {
            var markerFound = false;
            var points = new List<SpectrumPoint>(4096);
            var wavelengths = new HashSet<double>();
            var lineNumber = 0;
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lineNumber++;
                var trimmed = line.Trim();

                if (!markerFound)
                {
                    if (string.Equals(trimmed, "#DATA", StringComparison.Ordinal))
                    {
                        markerFound = true;
                    }

                    continue;
                }

                if (trimmed.Length == 0)
                {
                    continue;
                }

                var columns = ColumnSeparator.Split(trimmed);
                if (columns.Length != 2)
                {
                    throw new AscParseException(
                        string.Format(CultureInfo.CurrentCulture, "第 {0} 行不是两列数值。", lineNumber));
                }

                double wavelength;
                double intensity;
                if (!TryParseFinite(columns[0], out wavelength) ||
                    !TryParseFinite(columns[1], out intensity))
                {
                    throw new AscParseException(
                        string.Format(CultureInfo.CurrentCulture, "第 {0} 行包含非数值或无效数值。", lineNumber));
                }

                if (!wavelengths.Add(wavelength))
                {
                    throw new AscParseException(
                        string.Format(CultureInfo.CurrentCulture, "第 {0} 行出现重复波长 {1}。", lineNumber, columns[0]));
                }

                points.Add(new SpectrumPoint(wavelength, intensity));
            }

            if (!markerFound)
            {
                throw new AscParseException("未找到准确的 #DATA 标记。");
            }

            if (points.Count == 0)
            {
                throw new AscParseException("#DATA 后没有可用数据。");
            }

            points.Sort(delegate(SpectrumPoint left, SpectrumPoint right)
            {
                return left.Wavelength.CompareTo(right.Wavelength);
            });

            var wavelengthArray = new double[points.Count];
            var intensityArray = new double[points.Count];
            for (var index = 0; index < points.Count; index++)
            {
                wavelengthArray[index] = points[index].Wavelength;
                intensityArray[index] = points[index].Intensity;
            }

            return new SpectrumDataset(
                Path.GetFileNameWithoutExtension(fullPath),
                fullPath,
                wavelengthArray,
                intensityArray);
        }

        public static bool TryParseFinite(string value, out double result)
        {
            var styles = NumberStyles.Float | NumberStyles.AllowThousands;
            var parsed = double.TryParse(value, styles, CultureInfo.InvariantCulture, out result) ||
                         double.TryParse(value, styles, CultureInfo.CurrentCulture, out result);

            return parsed && !double.IsNaN(result) && !double.IsInfinity(result);
        }

        private struct SpectrumPoint
        {
            public SpectrumPoint(double wavelength, double intensity)
            {
                Wavelength = wavelength;
                Intensity = intensity;
            }

            public readonly double Wavelength;
            public readonly double Intensity;
        }
    }
}

