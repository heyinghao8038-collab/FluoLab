using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace FluoLab.App
{
    internal static class WindowPlacement
    {
        private static bool registered;
        private static readonly DependencyProperty FittedProperty = DependencyProperty.RegisterAttached("Fitted", typeof(bool), typeof(WindowPlacement), new PropertyMetadata(false));
        internal static void Register()
        {
            if (registered) return;
            registered = true;
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(delegate(object sender, RoutedEventArgs args)
            {
                var window = (Window)sender;
                // Non-taskbar tools and offscreen QA own their explicit placement.
                if (!window.ShowInTaskbar || (bool)window.GetValue(FittedProperty)) return;
                window.SetValue(FittedProperty, true);
                if (window.WindowState != WindowState.Normal) return;
                var screen = Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle).WorkingArea;
                var dpi = VisualTreeHelper.GetDpi(window);
                var work = new Rect(screen.Left / dpi.DpiScaleX, screen.Top / dpi.DpiScaleY, screen.Width / dpi.DpiScaleX, screen.Height / dpi.DpiScaleY);
                var bounds = Fit(new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight), work);
                window.MinWidth = Math.Min(window.MinWidth, bounds.Width); window.MinHeight = Math.Min(window.MinHeight, bounds.Height);
                window.Width = bounds.Width; window.Height = bounds.Height; window.Left = bounds.Left; window.Top = bounds.Top;
            }));
        }
        internal static Rect Fit(Rect requested, Rect work)
        {
            const double gap = 8;
            var width = Math.Min(requested.Width, Math.Max(1, work.Width - gap * 2));
            var height = Math.Min(requested.Height, Math.Max(1, work.Height - gap * 2));
            var x = Math.Max(work.Left + gap, Math.Min(requested.Left, work.Right - gap - width));
            var y = Math.Max(work.Top + gap, Math.Min(requested.Top, work.Bottom - gap - height));
            return new Rect(x, y, width, height);
        }
    }
}
