using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using FluoLab.Core;

namespace FluoLab.App
{
    internal sealed class LaboratoryModulesWindow : Window
    {
        private static LoopbackAssetServer mouseServer;
        private readonly TextBlock status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Theme.SecondaryTextBrush, Margin = new Thickness(0, 16, 0, 0) };
        public LaboratoryModulesWindow()
        {
            Title = "FluoLab 实验室工具"; Width = 920; Height = 640; MinWidth = 760; MinHeight = 480; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var content = new StackPanel { Margin = new Thickness(28) };
            content.Children.Add(new TextBlock { Text = "实验室工具", FontSize = 26, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 20) });
            content.Children.Add(Card("文献阅读与离线翻译", "导入英文 PDF，查看原始页、提取原文、按页或全文生成中文机器译文。需要配套可选 CPU 组件，文献不上传。", "打开文献阅读", delegate {
                var paper = OwnedWindows.OfType<LiteratureWindow>().FirstOrDefault();
                if (paper == null) new LiteratureWindow { Owner = this }.Show(); else paper.Activate();
            }));
            content.Children.Add(Card("小鼠管理", "复用原鼠鼠管理的笼位、分组合笼、繁育、来源和日志。使用独立浏览器本机数据空间，初次为空库；旧记录仅通过明确的 JSON 备份导入。", "打开小鼠管理（本机浏览器）", OpenMouse));
            content.Children.Add(status); Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs args)
            {
                foreach (LiteratureWindow paper in OwnedWindows.OfType<LiteratureWindow>().ToArray())
                { if (!paper.CanClose()) { args.Cancel = true; paper.Activate(); return; } paper.Close(); }
            };
        }
        private void OpenMouse()
        {
            try
            {
                EnsureMouseServer(); Process.Start(new ProcessStartInfo("http://127.0.0.1:41873/") { UseShellExecute = true });
                status.Text = "已打开本机浏览器模块。数据保存在当前浏览器，请定期导出 JSON 备份；关闭 FluoLab 后无法刷新模块页面。原鼠鼠管理未被修改。";
            }
            catch (SocketException) { status.Text = "本机固定端口 41873 被占用。请关闭其他 FluoLab 实例或占用程序后重试。不会换到其他地址，以免误用新的浏览器数据空间。"; }
            catch (Exception error) { status.Text = "小鼠模块未打开：" + error.Message + " 请检查完整安装文件或默认浏览器设置。"; }
        }
        internal static void EnsureMouseServer()
        {
            if (mouseServer != null) return;
            mouseServer = new LoopbackAssetServer(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Modules", "Mouse"), 41873);
            if (Application.Current != null) Application.Current.Exit += delegate { if (mouseServer != null) mouseServer.Dispose(); };
        }
        private static Border Card(string title, string detail, string action, Action handler)
        {
            var content = new StackPanel(); content.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold });
            content.Children.Add(new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap, Foreground = Theme.SecondaryTextBrush, FontSize = 14, Margin = new Thickness(0, 12, 0, 16) });
            var button = new Button { Content = action, HorizontalAlignment = HorizontalAlignment.Left }; button.Click += delegate { handler(); }; content.Children.Add(button);
            return new Border { Background = Theme.SurfaceBrush, CornerRadius = new CornerRadius(14), Padding = new Thickness(24), Margin = new Thickness(0, 0, 0, 16), Child = content };
        }
    }
}
