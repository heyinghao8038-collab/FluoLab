using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using FluoLab.Core;

namespace FluoLab.Tests
{
    internal static class LaboratoryTests
    {
        internal static void Run(string root)
        {
            var document = new LiteratureDocument { FileName = "<script>paper&.pdf", SourceSha256 = "abc", Engine = "local", Model = "synthetic", Pages = new List<LiteraturePage> {
                new LiteraturePage { Number = 1, OriginalText = "<script>alert(1)</script> 600 nm", ChineseText = "模拟 & 译文", Status = "未复核" },
                new LiteraturePage { Number = 2, OriginalText = "Page 2", ChineseText = "", Status = "未翻译" } } };
            var html = LiteratureReport.Html(document);
            Check(!html.Contains("<script>") && html.Contains("&lt;script&gt;") && html.Contains("600 nm") && html.Contains("未翻译"), "HTML literal escaping and missing translation status");
            Check(LiteratureReport.Text(document).Contains("Page 2"), "page-linked source text retained");
            foreach (var names in new[] { new[] { "../escape.exe" }, new[] { "/rooted" }, new[] { "C:/escape" }, new[] { "one", "ONE" } })
            {
                var zip = Path.Combine(root, Guid.NewGuid().ToString("N") + ".zip");
                using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
                    foreach (var name in names) using (var writer = new StreamWriter(archive.CreateEntry(name).Open())) writer.Write("synthetic");
                var rejected = false;
                try { LiteratureComponent.ExtractChecked(zip, Path.Combine(root, Guid.NewGuid().ToString("N"))); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "unsafe component path/duplicate rejected");
                rejected = false;
                try { LiteratureComponent.Install(zip, new string('0', 64), Path.Combine(root, "component-parent")); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && !Directory.Exists(Path.Combine(root, "component-parent")), "hash mismatch rejected before extraction");
            }
            var assets = Path.Combine(root, "mouse-assets"); Directory.CreateDirectory(assets);
            foreach (var file in new[] { "index.html", "app.js", "app.css" }) File.WriteAllText(Path.Combine(assets, file), "synthetic-" + file);
            using (var server = new LoopbackAssetServer(assets, 0))
            {
                var good = Request(server.Port, "GET / HTTP/1.1\r\nHost: 127.0.0.1:" + server.Port + "\r\n\r\n");
                Check(good.Contains("200 OK") && good.Contains("synthetic-index.html") && good.Contains("connect-src 'none'") && good.Contains("no-store"), "loopback assets and no external connection CSP");
                Check(Request(server.Port, "GET / HTTP/1.1\r\nHost: evil.invalid\r\n\r\n").Contains("403"), "foreign host rejected");
                Check(Request(server.Port, "GET /../component.json HTTP/1.1\r\nHost: 127.0.0.1:" + server.Port + "\r\n\r\n").Contains("404"), "no filesystem routes");
                Check(Request(server.Port, "POST / HTTP/1.1\r\nHost: 127.0.0.1:" + server.Port + "\r\n\r\n").Contains("405"), "server cannot write records");
                var head = Request(server.Port, "HEAD / HTTP/1.1\r\nHost: 127.0.0.1:" + server.Port + "\r\n\r\n");
                Check(head.Contains("200 OK") && !head.Contains("synthetic-index.html"), "HEAD omits body");
            }
            Console.WriteLine("PASS: literature report escaping, component safety and loopback origin/route isolation.");
        }
        private static string Request(int port, string text)
        {
            using (var client = new TcpClient("127.0.0.1", port))
            using (var stream = client.GetStream())
            {
                client.ReceiveTimeout = 5000; var bytes = Encoding.ASCII.GetBytes(text); stream.Write(bytes, 0, bytes.Length);
                using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
            }
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
