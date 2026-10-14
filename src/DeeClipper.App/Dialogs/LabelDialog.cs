using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.SolidColorBrush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaFontFamily = System.Windows.Media.FontFamily;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfCursors = System.Windows.Input.Cursors;

namespace DeeClipper.App.Dialogs;

public class LabelDialog : Window
{
    private readonly WpfTextBox _labelBox;

    public string ResultLabel => _labelBox.Text.Trim();

    public LabelDialog(string? currentLabel = null, Window? owner = null)
    {
        Title = "Set Label for Pinned Item - DeeClipper";
        Width = 420;
        Height = 200;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = owner;
        Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42)); // Slate-900
        Foreground = MediaBrushes.White;
        FontFamily = new MediaFontFamily("Segoe UI, sans-serif");
        ResizeMode = ResizeMode.NoResize;

        var rootGrid = new Grid { Margin = new Thickness(20) };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Input
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

        var header = new TextBlock
        {
            Text = "Label (shown instead of the preview text):",
            FontSize = 13,
            Foreground = new MediaBrush(MediaColor.FromRgb(203, 213, 225)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(header, 0);
        rootGrid.Children.Add(header);

        _labelBox = new WpfTextBox
        {
            Text = currentLabel ?? string.Empty,
            Padding = new Thickness(10, 6, 10, 6),
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 16)
        };
        Grid.SetRow(_labelBox, 1);
        rootGrid.Children.Add(_labelBox);

        var btnStack = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right
        };

        var saveBtn = new WpfButton
        {
            Content = "Save Label",
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new MediaBrush(MediaColor.FromRgb(13, 148, 136)),
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Cursor = WpfCursors.Hand,
            IsDefault = true
        };
        saveBtn.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
        btnStack.Children.Add(saveBtn);

        var clearBtn = new WpfButton
        {
            Content = "Remove Label",
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = new MediaBrush(MediaColor.FromRgb(248, 113, 113)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            Cursor = WpfCursors.Hand
        };
        clearBtn.Click += (_, _) =>
        {
            _labelBox.Text = string.Empty;
            DialogResult = true;
            Close();
        };
        btnStack.Children.Add(clearBtn);

        var cancelBtn = new WpfButton
        {
            Content = "Cancel",
            Padding = new Thickness(12, 6, 12, 6),
            Background = MediaBrushes.Transparent,
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            BorderThickness = new Thickness(0),
            Cursor = WpfCursors.Hand,
            IsCancel = true
        };
        btnStack.Children.Add(cancelBtn);

        Grid.SetRow(btnStack, 2);
        rootGrid.Children.Add(btnStack);

        Content = rootGrid;

        Loaded += (_, _) =>
        {
            _labelBox.Focus();
            _labelBox.SelectAll();
        };
    }
}
