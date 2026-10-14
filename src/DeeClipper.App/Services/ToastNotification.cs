using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.SolidColorBrush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaFontFamily = System.Windows.Media.FontFamily;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;
using WpfApplication = System.Windows.Application;

namespace DeeClipper.App.Services;

public static class ToastNotification
{
    public static void Show(string message)
    {
        WpfApplication.Current?.Dispatcher?.Invoke(() =>
        {
            var window = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = MediaBrushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Focusable = false
            };

            var border = new Border
            {
                Background = new MediaBrush(MediaColor.FromArgb(240, 15, 23, 42)), // Slate-900 with alpha
                BorderBrush = new MediaBrush(MediaColor.FromRgb(13, 148, 136)),   // Teal-600
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 10, 16, 10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 16,
                    ShadowDepth = 2,
                    Opacity = 0.4
                }
            };

            var text = new TextBlock
            {
                Text = message,
                Foreground = MediaBrushes.White,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                FontFamily = new MediaFontFamily("Segoe UI, sans-serif")
            };

            border.Child = text;
            window.Content = border;

            // Position at bottom center of the primary screen
            var workArea = SystemParameters.WorkArea;
            window.Left = workArea.Left + (workArea.Width / 2) - 120;
            window.Top = workArea.Bottom - 90;

            window.Show();

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                window.Close();
            };
            timer.Start();
        });
    }
}
