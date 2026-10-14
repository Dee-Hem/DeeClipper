using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using DeeClipper.App.Services;
using MediaBrush = System.Windows.Media.SolidColorBrush;
using MediaColor = System.Windows.Media.Color;
using MediaFontFamily = System.Windows.Media.FontFamily;
using WpfButton = System.Windows.Controls.Button;
using WpfCursors = System.Windows.Input.Cursors;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;

namespace DeeClipper.App.Dialogs;

public class DeleteAllConfirmDialog : Window
{
    private readonly WpfTextBox _inputBox;
    private readonly WpfButton _confirmBtn;

    public bool Confirmed { get; private set; }

    public DeleteAllConfirmDialog(Window? owner = null)
    {
        Owner = owner;
        Title = "Delete Everything Confirmation";
        Width = 460;
        Height = 260;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42)); // Slate-900
        Foreground = new MediaBrush(MediaColor.FromRgb(248, 250, 252));
        FontFamily = new MediaFontFamily("Segoe UI, Segoe UI Variable, sans-serif");
        ResizeMode = ResizeMode.NoResize;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.ApplyModernWindowRounding(hwnd);
        };

        var root = new StackPanel
        {
            Margin = new Thickness(24)
        };

        var warningHeader = new TextBlock
        {
            Text = "⚠️ Danger: Irreversible Action",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = new MediaBrush(MediaColor.FromRgb(239, 68, 68)), // Red-500
            Margin = new Thickness(0, 0, 0, 8)
        };
        root.Children.Add(warningHeader);

        var warningText = new TextBlock
        {
            Text = "This will permanently delete all history, including ALL PINNED items, labels, groups, and cached images.\nTo confirm, type DELETE below:",
            FontSize = 12,
            Foreground = new MediaBrush(MediaColor.FromRgb(203, 213, 225)),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18,
            Margin = new Thickness(0, 0, 0, 16)
        };
        root.Children.Add(warningText);

        _inputBox = new WpfTextBox
        {
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(71, 85, 105)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 6, 10, 6),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 18)
        };
        _inputBox.TextChanged += (_, _) =>
        {
            _confirmBtn.IsEnabled = string.Equals(_inputBox.Text.Trim(), "DELETE", StringComparison.Ordinal);
        };
        root.Children.Add(_inputBox);

        var btnStack = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right
        };

        var cancelBtn = new WpfButton
        {
            Content = "Cancel",
            Background = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(16, 6, 16, 6),
            FontSize = 12,
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = WpfCursors.Hand
        };
        cancelBtn.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };
        btnStack.Children.Add(cancelBtn);

        _confirmBtn = new WpfButton
        {
            Content = "Delete Everything",
            Background = new MediaBrush(MediaColor.FromRgb(220, 38, 38)), // Red-600
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(16, 6, 16, 6),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            IsEnabled = false,
            Cursor = WpfCursors.Hand
        };
        _confirmBtn.Click += (_, _) =>
        {
            if (string.Equals(_inputBox.Text.Trim(), "DELETE", StringComparison.Ordinal))
            {
                Confirmed = true;
                DialogResult = true;
                Close();
            }
        };
        btnStack.Children.Add(_confirmBtn);

        root.Children.Add(btnStack);
        Content = root;

        Loaded += (_, _) => _inputBox.Focus();
    }
}
