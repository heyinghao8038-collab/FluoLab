using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FluoLab.Setup
{
    internal static class Program
    {
        private const string ProductName = "FluoLab 荧光数据分析器";
        private static readonly string InstallFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "FluoLab");

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length > 0 && string.Equals(args[0], "/uninstall", StringComparison.OrdinalIgnoreCase))
            {
                return Uninstall(args.Length > 1 && string.Equals(args[1], "/quiet", StringComparison.OrdinalIgnoreCase));
            }

            if (args.Length > 0 && string.Equals(args[0], "/install", StringComparison.OrdinalIgnoreCase))
            {
                var createDesktopShortcut = true;
                var quiet = false;
                foreach (var argument in args)
                {
                    if (string.Equals(argument, "/nodesktop", StringComparison.OrdinalIgnoreCase))
                    {
                        createDesktopShortcut = false;
                    }

                    if (string.Equals(argument, "/quiet", StringComparison.OrdinalIgnoreCase))
                    {
                        quiet = true;
                    }
                }

                try
                {
                    InstallFiles(createDesktopShortcut);
                    return 0;
                }
                catch (Exception exception)
                {
                    if (!quiet)
                    {
                        MessageBox.Show(
                            "无法完成安装：\n\n" + exception.Message,
                            ProductName,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }

                    return 1;
                }
            }

            Application.Run(new SetupForm());
            return 0;
        }

        private static int Uninstall(bool quiet)
        {
            var expected = Path.GetFullPath(InstallFolder).TrimEnd(Path.DirectorySeparatorChar);
            var current = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(expected, current, StringComparison.OrdinalIgnoreCase))
            {
                if (!quiet)
                {
                    MessageBox.Show("卸载程序不在预期的安装目录中，已停止操作。", ProductName);
                }

                return 1;
            }

            if (!quiet)
            {
                var answer = MessageBox.Show(
                    "将从当前账户卸载 FluoLab。实验 ASC 和已导出的 Excel 文件不会被删除。",
                    "卸载 FluoLab",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Information);
                if (answer != DialogResult.OK)
                {
                    return 0;
                }
            }

            try
            {
                DeleteShortcut(GetStartMenuShortcut());
                DeleteShortcut(GetDesktopShortcut());
                Registry.CurrentUser.DeleteSubKeyTree(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FluoLab",
                    false);

                var command = string.Format(
                    "/d /c ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{0}\"",
                    expected.Replace("\"", string.Empty));
                Process.Start(new ProcessStartInfo
                {
                    FileName = Environment.GetEnvironmentVariable("ComSpec"),
                    Arguments = command,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
                return 0;
            }
            catch (Exception exception)
            {
                if (!quiet)
                {
                    MessageBox.Show(
                        "卸载失败：\n\n" + exception.Message,
                        ProductName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

                return 1;
            }
        }

        private static void DeleteShortcut(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder) &&
                Directory.Exists(folder) &&
                Directory.GetFileSystemEntries(folder).Length == 0)
            {
                Directory.Delete(folder);
            }
        }

        private static string GetStartMenuShortcut()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                "FluoLab",
                "FluoLab 荧光数据分析器.lnk");
        }

        private static string GetDesktopShortcut()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "FluoLab 荧光数据分析器.lnk");
        }

        private sealed class SetupForm : Form
        {
            private readonly CheckBox desktopShortcut;
            private readonly Button installButton;
            private readonly Label statusLabel;
            private readonly ProgressBar progressBar;

            public SetupForm()
            {
                Text = "安装 " + ProductName;
                StartPosition = FormStartPosition.CenterScreen;
                ClientSize = new Size(620, 390);
                MinimumSize = new Size(620, 390);
                MaximumSize = new Size(620, 390);
                Font = new Font("Microsoft YaHei UI", 9F);
                BackColor = Color.FromArgb(245, 245, 247);
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;

                var title = new Label
                {
                    Text = "FluoLab",
                    Font = new Font("Microsoft YaHei UI", 24F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(29, 29, 31),
                    AutoSize = true,
                    Location = new Point(36, 30)
                };
                Controls.Add(title);

                var subtitle = new Label
                {
                    Text = "荧光数据分析器 1.4.0 · 完整性与文献核对",
                    Font = new Font("Microsoft YaHei UI", 11F),
                    ForeColor = Color.FromArgb(110, 110, 115),
                    AutoSize = true,
                    Location = new Point(39, 78)
                };
                Controls.Add(subtitle);

                var description = new Label
                {
                    Text = "程序仅在本机处理 ASC 数据，不需要 Excel、WPS、网络或管理员权限。",
                    Font = new Font("Microsoft YaHei UI", 10F),
                    ForeColor = Color.FromArgb(29, 29, 31),
                    AutoSize = true,
                    Location = new Point(39, 126)
                };
                Controls.Add(description);

                var pathLabel = new Label
                {
                    Text = "安装位置",
                    Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(29, 29, 31),
                    AutoSize = true,
                    Location = new Point(39, 175)
                };
                Controls.Add(pathLabel);

                var pathValue = new Label
                {
                    Text = InstallFolder,
                    Font = new Font("Microsoft YaHei UI", 9F),
                    ForeColor = Color.FromArgb(110, 110, 115),
                    AutoEllipsis = true,
                    Location = new Point(39, 199),
                    Size = new Size(540, 22)
                };
                Controls.Add(pathValue);

                desktopShortcut = new CheckBox
                {
                    Text = "创建桌面快捷方式",
                    Checked = true,
                    AutoSize = true,
                    Location = new Point(39, 239)
                };
                Controls.Add(desktopShortcut);

                progressBar = new ProgressBar
                {
                    Location = new Point(39, 280),
                    Size = new Size(540, 6),
                    Style = ProgressBarStyle.Continuous,
                    Visible = false
                };
                Controls.Add(progressBar);

                statusLabel = new Label
                {
                    Text = "安装将创建开始菜单入口，可从 Windows 设置中卸载。",
                    ForeColor = Color.FromArgb(110, 110, 115),
                    Location = new Point(39, 301),
                    Size = new Size(390, 42)
                };
                Controls.Add(statusLabel);

                installButton = new Button
                {
                    Text = "安装",
                    BackColor = Color.FromArgb(0, 113, 227),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                    Location = new Point(464, 300),
                    Size = new Size(115, 42),
                    Cursor = Cursors.Hand
                };
                installButton.FlatAppearance.BorderSize = 0;
                installButton.Click += InstallButtonClick;
                Controls.Add(installButton);
                AcceptButton = installButton;
            }

            private async void InstallButtonClick(object sender, EventArgs eventArgs)
            {
                installButton.Enabled = false;
                desktopShortcut.Enabled = false;
                progressBar.Visible = true;
                progressBar.Style = ProgressBarStyle.Marquee;
                statusLabel.Text = "正在安装…";

                try
                {
                    var createDesktopShortcut = desktopShortcut.Checked;
                    await System.Threading.Tasks.Task.Run(delegate { InstallFiles(createDesktopShortcut); });
                    progressBar.Style = ProgressBarStyle.Continuous;
                    progressBar.Value = 100;
                    statusLabel.Text = "安装完成。";
                    installButton.Text = "启动 FluoLab";
                    installButton.Enabled = true;
                    installButton.Click -= InstallButtonClick;
                    installButton.Click += delegate
                    {
                        Process.Start(Path.Combine(InstallFolder, "FluoLab.exe"));
                        Close();
                    };
                }
                catch (Exception exception)
                {
                    progressBar.Visible = false;
                    statusLabel.Text = "安装失败。";
                    installButton.Enabled = true;
                    desktopShortcut.Enabled = true;
                    MessageBox.Show(
                        this,
                        "无法完成安装：\n\n" + exception.Message,
                        ProductName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private static void InstallFiles(bool createDesktopShortcut)
        {
            Directory.CreateDirectory(InstallFolder);
            Directory.CreateDirectory(Path.Combine(InstallFolder, "Modules"));

            var resources = new Dictionary<string, string>
            {
                { "FluoLab.Payload.App", "FluoLab.exe" },
                { "FluoLab.Payload.Cli", "FluoLab.Cli.exe" },
                { "FluoLab.Payload.Core", "FluoLab.Core.dll" },
                { "FluoLab.Payload.Export", "FluoLab.Export.dll" },
                { "FluoLab.Payload.Guide", "使用说明.txt" },
                { "FluoLab.Payload.ModuleGuide", Path.Combine("Modules", "模块接口说明.md") }
            };

            foreach (var item in resources)
            {
                WriteResource(item.Key, Path.Combine(InstallFolder, item.Value));
            }

            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("FluoLab.Payload.Modules"))
            using (var archive = new ZipArchive(input, ZipArchiveMode.Read))
            {
                var prefix = Path.GetFullPath(Path.Combine(InstallFolder, "Modules")) + Path.DirectorySeparatorChar;
                foreach (var entry in archive.Entries)
                {
                    var target = Path.GetFullPath(Path.Combine(prefix, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || entry.FullName.Contains(":")) throw new InvalidOperationException("模块资源路径无效。");
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    var temporary = target + ".installing";
                    using (var source = entry.Open()) using (var output = File.Create(temporary)) source.CopyTo(output);
                    if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
                }
            }

            File.Copy(Application.ExecutablePath, Path.Combine(InstallFolder, "Uninstall.exe"), true);
            CreateShortcut(
                GetStartMenuShortcut(),
                Path.Combine(InstallFolder, "FluoLab.exe"),
                InstallFolder,
                ProductName);

            if (createDesktopShortcut)
            {
                CreateShortcut(
                    GetDesktopShortcut(),
                    Path.Combine(InstallFolder, "FluoLab.exe"),
                    InstallFolder,
                    ProductName);
            }
            else
            {
                DeleteShortcut(GetDesktopShortcut());
            }

            using (var key = Registry.CurrentUser.CreateSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FluoLab"))
            {
                key.SetValue("DisplayName", ProductName);
                key.SetValue("DisplayVersion", "1.4.0");
                key.SetValue("Publisher", "FluoLab");
                key.SetValue("InstallLocation", InstallFolder);
                key.SetValue("DisplayIcon", Path.Combine(InstallFolder, "FluoLab.exe"));
                key.SetValue("UninstallString", "\"" + Path.Combine(InstallFolder, "Uninstall.exe") + "\" /uninstall");
                key.SetValue("QuietUninstallString", "\"" + Path.Combine(InstallFolder, "Uninstall.exe") + "\" /uninstall /quiet");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        private static void WriteResource(string resourceName, string destination)
        {
            var destinationFolder = Path.GetDirectoryName(destination);
            if (!string.IsNullOrWhiteSpace(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (input == null)
                {
                    throw new InvalidOperationException("安装资源缺失：" + resourceName);
                }

                var tempPath = destination + ".installing";
                using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                }

                if (File.Exists(destination))
                {
                    File.Delete(destination);
                }

                File.Move(tempPath, destination);
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string description)
        {
            var folder = Path.GetDirectoryName(shortcutPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var shellLink = (IShellLinkW)(object)new ShellLink();
            shellLink.SetPath(targetPath);
            shellLink.SetWorkingDirectory(workingDirectory);
            shellLink.SetDescription(description);
            shellLink.SetIconLocation(targetPath, 0);
            ((IPersistFile)shellLink).Save(shortcutPath, true);
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private sealed class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maximumPath, IntPtr data, uint flags);
            void GetIDList(out IntPtr itemIdList);
            void SetIDList(IntPtr itemIdList);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maximumName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maximumPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maximumPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
            void GetHotkey(out short hotkey);
            void SetHotkey(short hotkey);
            void GetShowCmd(out int showCommand);
            void SetShowCmd(int showCommand);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int iconPathLength, out int iconIndex);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
            void Resolve(IntPtr windowHandle, uint flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        private interface IPersistFile
        {
            void GetClassID(out Guid classId);
            [PreserveSig]
            int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, bool remember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
        }
    }
}
