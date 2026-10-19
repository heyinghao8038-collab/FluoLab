using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace FluoLab.Core
{
    public sealed class LiteratureProgress
    {
        public int Completed { get; set; }
        public int Total { get; set; }
    }
    public static class LiteratureComponent
    {
        public static string InstalledRoot { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FluoLab", "Components", "lit-v1"); } }
        public static bool Available(string root) { return File.Exists(Path.Combine(root, "python", "python.exe")) && File.Exists(Path.Combine(root, "model", "model", "model.bin")) && File.Exists(Path.Combine(root, "component.json")); }
        public static string Install(string archivePath, string expectedSha256, string parent)
        {
            if (string.IsNullOrWhiteSpace(expectedSha256) || expectedSha256.Length != 64) throw new InvalidOperationException("此程序缺少离线组件的发布校验信息，请重新获取完整安装包。");
            using (var source = File.OpenRead(archivePath))
            using (var sha = SHA256.Create())
                if (!string.Equals(SourceIdentity.Hex(sha.ComputeHash(source)), expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("组件包校验不一致，未安装。请使用与本版本配套的官方交付组件包。");
            Directory.CreateDirectory(parent);
            var staging = Path.Combine(parent, "lit-stage-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            var target = Path.Combine(parent, "lit-v1");
            var backup = Path.Combine(parent, "lit-previous-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(staging);
            try
            {
                ExtractChecked(archivePath, staging);
                if (!Available(staging)) throw new InvalidOperationException("组件包不完整，没有找到本地渲染和翻译文件。");
                var manifest = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(Path.Combine(staging, "component.json")));
                if (manifest == null || !manifest.ContainsKey("id") || !object.Equals(manifest["id"], "FluoLab.Literature.cpu.v1") || !manifest.ContainsKey("version") || Convert.ToInt32(manifest["version"], CultureInfo.InvariantCulture) != 1)
                    throw new InvalidOperationException("组件类型或版本不受支持。");
                if (Directory.Exists(target)) Directory.Move(target, backup);
                try { Directory.Move(staging, target); }
                catch { if (Directory.Exists(backup) && !Directory.Exists(target)) Directory.Move(backup, target); throw; }
                return target;
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }
        public static void ExtractChecked(string archivePath, string destination)
        {
            var prefix = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                if (archive.Entries.Count > 15000) throw new InvalidOperationException("组件包文件数超过限制。");
                long total = 0, actualTotal = 0; var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in archive.Entries)
                {
                    var name = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    var path = Path.GetFullPath(Path.Combine(prefix, name));
                    if (name.IndexOf(':') >= 0 || Array.IndexOf(name.Split(Path.DirectorySeparatorChar), "..") >= 0 || Path.IsPathRooted(name) || !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !names.Add(path) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                        throw new InvalidOperationException("组件包包含不安全或重复路径，已拒绝。");
                    total += entry.Length;
                    if (total > 1024L * 1024 * 1024) throw new InvalidOperationException("组件解压大小超过 1 GB 限制。");
                    if (entry.Name.Length == 0) { Directory.CreateDirectory(path); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var input = entry.Open())
                    using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[65536]; int count; long actual = 0;
                        while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                        {
                            actual += count; actualTotal += count;
                            if (actual > entry.Length || actualTotal > 1024L * 1024 * 1024) throw new InvalidOperationException("组件包实际解压大小不符合限制。");
                            output.Write(buffer, 0, count);
                        }
                        if (actual != entry.Length) throw new InvalidOperationException("组件包文件长度不一致。");
                    }
                }
            }
        }
    }
    public sealed class LiteratureWorker
    {
        private readonly string componentRoot;
        private readonly string script;
        public LiteratureWorker(string root, string workerScript) { componentRoot = Path.GetFullPath(root); script = Path.GetFullPath(workerScript); }
        public Task RunAsync(string[] arguments, IProgress<LiteratureProgress> progress, CancellationToken cancellation)
        {
            return Task.Run(delegate
            {
                if (!LiteratureComponent.Available(componentRoot)) throw new InvalidOperationException("请先安装配套离线组件包。");
                if (!File.Exists(script)) throw new InvalidOperationException("文献处理程序缺失，请重新安装 FluoLab。");
                var command = new StringBuilder(Quote(script)); foreach (var item in arguments) command.Append(' ').Append(Quote(item));
                var errorMessage = "";
                using (var process = new Process { StartInfo = new ProcessStartInfo { FileName = Path.Combine(componentRoot, "python", "python.exe"), Arguments = command.ToString(),
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 } })
                {
                    cancellation.ThrowIfCancellationRequested(); process.Start();
                    process.ErrorDataReceived += delegate { }; process.BeginErrorReadLine();
                    using (cancellation.Register(delegate { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } }))
                    {
                        string line;
                        while ((line = process.StandardOutput.ReadLine()) != null)
                        {
                            var record = Json().Deserialize<Dictionary<string, object>>(line);
                            object kind;
                            if (record != null && record.TryGetValue("Kind", out kind))
                            {
                                if ((string)kind == "progress" && progress != null) progress.Report(new LiteratureProgress { Completed = Convert.ToInt32(record["Completed"], CultureInfo.InvariantCulture), Total = Convert.ToInt32(record["Total"], CultureInfo.InvariantCulture) });
                                else if ((string)kind == "error") errorMessage = Convert.ToString(record["Message"], CultureInfo.InvariantCulture);
                            }
                        }
                        process.WaitForExit(); cancellation.ThrowIfCancellationRequested();
                        if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrEmpty(errorMessage) ? "离线组件无法运行。请检查组件完整性及 Microsoft Visual C++ x64 运行库，再重试。" : errorMessage);
                    }
                }
            }, cancellation);
        }
        public static LiteratureDocument ReadDocument(string path)
        {
            if (new FileInfo(path).Length > 30 * 1024 * 1024) throw new InvalidOperationException("文献处理结果过大。");
            var document = Json().Deserialize<LiteratureDocument>(File.ReadAllText(path, Encoding.UTF8));
            if (document == null || document.Pages == null || document.Pages.Count == 0 || document.Pages.Count > 1000) throw new InvalidOperationException("文献结果无有效页面。");
            for (var i = 0; i < document.Pages.Count; i++) if (document.Pages[i].Number != i + 1) throw new InvalidOperationException("文献页码不完整，请重新导入。");
            return document;
        }
        public static void WriteDocument(string path, LiteratureDocument document) { File.WriteAllText(path, Json().Serialize(document), new UTF8Encoding(false)); }
        private static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = 30 * 1024 * 1024, RecursionLimit = 100 }; }
        private static string Quote(string value)
        {
            if (value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0) throw new InvalidOperationException("文件路径或参数包含无效字符。");
            return "\"" + value.TrimEnd('\\') + "\"";
        }
    }
}
