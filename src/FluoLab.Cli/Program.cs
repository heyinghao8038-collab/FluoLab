using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using FluoLab.Core;
using FluoLab.Export;

namespace FluoLab.Cli
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (args.Length < 1 || args.Length > 2)
            {
                Console.Error.WriteLine("用法：FluoLab.Cli.exe <ASC 文件夹> [输出.xlsx]");
                return 1;
            }

            try
            {
                var sourceFolder = Path.GetFullPath(args[0]);
                if (!Directory.Exists(sourceFolder))
                {
                    Console.Error.WriteLine("源文件夹不存在：{0}", sourceFolder);
                    return 1;
                }

                var outputPath = args.Length == 2
                    ? Path.GetFullPath(args[1])
                    : Path.Combine(sourceFolder, BuildDefaultFileName(sourceFolder));

                var processor = new BatchProcessor();
                var batch = processor.ProcessFolder(sourceFolder, null, CancellationToken.None);
                foreach (var skipped in batch.Files.Where(file => file.Status == ProcessingStatus.Failed))
                {
                    Console.Error.WriteLine("跳过：{0} — {1}", skipped.FileName, skipped.ErrorMessage);
                }

                if (batch.SuccessCount == 0)
                {
                    Console.Error.WriteLine("没有可导出的有效 ASC 文件。");
                    return 1;
                }

                new WorkbookExporter().Export(batch, outputPath, false);
                Console.WriteLine(
                    "完成：发现 {0}，成功 {1}，跳过 {2}，输出 {3}",
                    batch.DiscoveredCount,
                    batch.SuccessCount,
                    batch.SkippedCount,
                    outputPath);
                return 0;
            }
            catch (IOException exception)
            {
                Console.Error.WriteLine("导出失败：{0}", exception.Message);
                return 2;
            }
            catch (UnauthorizedAccessException exception)
            {
                Console.Error.WriteLine("导出失败：{0}", exception.Message);
                return 2;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("处理失败：{0}", exception.Message);
                return 1;
            }
        }

        private static string BuildDefaultFileName(string sourceFolder)
        {
            var folderName = new DirectoryInfo(sourceFolder).Name;
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            {
                folderName = folderName.Replace(invalidCharacter, '_');
            }

            return string.Format(
                "{0}_ASC汇总_{1}.xlsx",
                folderName,
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        }
    }
}

