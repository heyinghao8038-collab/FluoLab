using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FluoLab.Core
{
    public enum ProcessingStatus
    {
        Pending,
        Success,
        Failed
    }

    public sealed class SpectrumDataset
    {
        public SpectrumDataset(string displayName, string sourcePath, double[] wavelengths, double[] intensities)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Display name is required.", "displayName");
            }

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path is required.", "sourcePath");
            }

            if (wavelengths == null || intensities == null || wavelengths.Length == 0)
            {
                throw new ArgumentException("Spectrum data cannot be empty.");
            }

            if (wavelengths.Length != intensities.Length)
            {
                throw new ArgumentException("Wavelength and intensity arrays must have equal length.");
            }

            DisplayName = displayName;
            SourcePath = Path.GetFullPath(sourcePath);
            Wavelengths = wavelengths;
            Intensities = intensities;
        }

        public string DisplayName { get; private set; }
        public string SourcePath { get; private set; }
        public string SourceFingerprint { get; internal set; }
        public double[] Wavelengths { get; private set; }
        public double[] Intensities { get; private set; }
        public int PointCount { get { return Wavelengths.Length; } }
    }

    public sealed class FileProcessingResult
    {
        private FileProcessingResult(
            string fileName,
            string sourcePath,
            ProcessingStatus status,
            SpectrumDataset dataset,
            string errorMessage)
        {
            FileName = fileName;
            SourcePath = sourcePath;
            Status = status;
            Dataset = dataset;
            ErrorMessage = errorMessage;
        }

        public string FileName { get; private set; }
        public string SourcePath { get; private set; }
        public ProcessingStatus Status { get; private set; }
        public SpectrumDataset Dataset { get; private set; }
        public string ErrorMessage { get; private set; }

        public static FileProcessingResult Succeeded(string sourcePath, SpectrumDataset dataset)
        {
            return new FileProcessingResult(
                Path.GetFileName(sourcePath),
                Path.GetFullPath(sourcePath),
                ProcessingStatus.Success,
                dataset,
                null);
        }

        public static FileProcessingResult Failed(string sourcePath, string errorMessage)
        {
            return new FileProcessingResult(
                Path.GetFileName(sourcePath),
                Path.GetFullPath(sourcePath),
                ProcessingStatus.Failed,
                null,
                errorMessage);
        }
    }

    public sealed class SpectrumBatch
    {
        public SpectrumBatch(string sourceFolder, DateTimeOffset processedAt, IList<FileProcessingResult> files)
        {
            SourceFolder = Path.GetFullPath(sourceFolder);
            ProcessedAt = processedAt;
            Files = files == null ? new FileProcessingResult[0] : files.ToArray();
        }

        public string SourceFolder { get; private set; }
        public DateTimeOffset ProcessedAt { get; private set; }
        public FileProcessingResult[] Files { get; private set; }
        public int DiscoveredCount { get { return Files.Length; } }
        public int SuccessCount { get { return Files.Count(file => file.Status == ProcessingStatus.Success); } }
        public int SkippedCount { get { return Files.Count(file => file.Status == ProcessingStatus.Failed); } }

        public SpectrumDataset[] ValidDatasets
        {
            get
            {
                return Files
                    .Where(file => file.Status == ProcessingStatus.Success && file.Dataset != null)
                    .Select(file => file.Dataset)
                    .ToArray();
            }
        }
    }

    public sealed class ProcessingProgress
    {
        public ProcessingProgress(int completed, int total, string currentFile)
        {
            Completed = completed;
            Total = total;
            CurrentFile = currentFile;
        }

        public int Completed { get; private set; }
        public int Total { get; private set; }
        public string CurrentFile { get; private set; }
    }
}

