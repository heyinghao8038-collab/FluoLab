using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FluoLab.Core
{
    public sealed class BatchProcessor
    {
        private readonly AscParser parser;

        public BatchProcessor()
            : this(new AscParser())
        {
        }

        public BatchProcessor(AscParser parser)
        {
            this.parser = parser;
        }

        public SpectrumBatch ProcessFolder(
            string folderPath,
            IProgress<ProcessingProgress> progress,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("文件夹路径不能为空。", "folderPath");
            }

            var fullPath = Path.GetFullPath(folderPath);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException("选择的文件夹不存在。");
            }

            var files = Directory
                .EnumerateFiles(fullPath, "*", SearchOption.TopDirectoryOnly)
                .Where(path => string.Equals(Path.GetExtension(path), ".asc", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ThenBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                .ToArray();

            var results = new FileProcessingResult[files.Length];
            var completed = 0;
            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, Math.Min(Environment.ProcessorCount, 4))
            };

            Parallel.For(0, files.Length, options, delegate(int index)
            {
                var filePath = files[index];
                try
                {
                    var dataset = parser.Parse(filePath, cancellationToken);
                    results[index] = FileProcessingResult.Succeeded(filePath, dataset);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    results[index] = FileProcessingResult.Failed(filePath, exception.Message);
                }
                finally
                {
                    var current = Interlocked.Increment(ref completed);
                    if (progress != null)
                    {
                        progress.Report(new ProcessingProgress(current, files.Length, Path.GetFileName(filePath)));
                    }
                }
            });

            return new SpectrumBatch(fullPath, DateTimeOffset.Now, results);
        }
    }

    public static class WavelengthAlignment
    {
        public static double[] BuildUnion(IEnumerable<SpectrumDataset> datasets)
        {
            if (datasets == null)
            {
                return new double[0];
            }

            var union = new SortedSet<double>();
            foreach (var dataset in datasets)
            {
                if (dataset == null)
                {
                    continue;
                }

                foreach (var wavelength in dataset.Wavelengths)
                {
                    union.Add(wavelength);
                }
            }

            return union.ToArray();
        }
    }
}

