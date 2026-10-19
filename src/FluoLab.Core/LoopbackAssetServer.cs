using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FluoLab.Core
{
    public sealed class LoopbackAssetServer : IDisposable
    {
        private readonly TcpListener listener;
        private readonly Dictionary<string, byte[]> assets = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private volatile bool disposed;
        public int Port { get; private set; }
        public LoopbackAssetServer(string root, int port)
        {
            foreach (var name in new[] { "index.html", "app.js", "app.css" })
            {
                var path = Path.Combine(root, name);
                if (!File.Exists(path)) throw new FileNotFoundException("小鼠模块文件缺失，请重新安装 FluoLab。", name);
                assets["/" + name] = File.ReadAllBytes(path);
            }
            assets["/"] = assets["/index.html"];
            assets["/health"] = Encoding.UTF8.GetBytes("FluoLab.Mouse.v1");
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task.Run((Func<Task>)AcceptLoop);
        }
        private async Task AcceptLoop()
        {
            while (!disposed)
            {
                try
                {
                    using (var client = await listener.AcceptTcpClientAsync()) Serve(client);
                }
                catch (ObjectDisposedException) { if (disposed) return; }
                catch (SocketException) { if (disposed) return; }
                catch (IOException) { }
            }
        }
        private void Serve(TcpClient client)
        {
            client.ReceiveTimeout = 2000; client.SendTimeout = 2000;
            using (var stream = client.GetStream())
            {
                var input = new List<byte>(); var end = false;
                while (input.Count < 16384)
                {
                    var value = stream.ReadByte(); if (value < 0) return; input.Add((byte)value);
                    var n = input.Count;
                    if (n >= 4 && input[n - 4] == 13 && input[n - 3] == 10 && input[n - 2] == 13 && input[n - 1] == 10) { end = true; break; }
                }
                if (!end) { Reply(stream, 400, "text/plain", new byte[0], false); return; }
                var lines = Encoding.ASCII.GetString(input.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.None);
                var first = lines[0].Split(' ');
                if (first.Length != 3 || (first[0] != "GET" && first[0] != "HEAD")) { Reply(stream, 405, "text/plain", new byte[0], false); return; }
                var host = "";
                foreach (var line in lines) if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)) host = line.Substring(5).Trim();
                if (host != "127.0.0.1:" + Port) { Reply(stream, 403, "text/plain", new byte[0], false); return; }
                var route = first[1].Split('?')[0]; byte[] body;
                if (!assets.TryGetValue(route, out body)) { Reply(stream, 404, "text/plain", new byte[0], false); return; }
                var type = route.EndsWith(".js", StringComparison.Ordinal) ? "text/javascript" : route.EndsWith(".css", StringComparison.Ordinal) ? "text/css" : route == "/health" ? "text/plain" : "text/html";
                Reply(stream, 200, type, body, first[0] == "HEAD");
            }
        }
        private static void Reply(Stream stream, int status, string type, byte[] body, bool head)
        {
            var header = "HTTP/1.1 " + status + " " + (status == 200 ? "OK" : "Error") + "\r\nContent-Type: " + type + "; charset=utf-8\r\nContent-Length: " + body.Length +
                "\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'none'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'none'\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(header); stream.Write(bytes, 0, bytes.Length);
            if (!head) stream.Write(body, 0, body.Length);
        }
        public void Dispose() { disposed = true; listener.Stop(); }
    }
}
