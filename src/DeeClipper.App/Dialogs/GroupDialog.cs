using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.SolidColorBrush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaFontFamily = System.Windows.Media.FontFamily;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfBrush = System.Windows.Media.Brush;
using WpfMessageBox = System.Windows.MessageBox;
using WpfCursors = System.Windows.Input.Cursors;

namespace DeeClipper.App.Dialogs;

public class GroupDialog : Window
{
    private readonly WpfTextBox _nameBox;
    private string _selectedColor = "#0D9488";
    private readonly Border _colorPreview;

    public string GroupName => _nameBox.Text.Trim();
    public string GroupColor => _selectedColor;

    public GroupDialog(string? initialName = null, string? initialColor = null, Window? owner = null)
    {
        Title = string.IsNullOrEmpty(initialName) ? "New Group - DeeClipper" : "Edit Group - DeeClipper";
        Width = 420;
        Height = 310;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = owner;
        Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42)); // Slate-900
        Foreground = MediaBrushes.White;
        FontFamily = new MediaFontFamily("Segoe UI, sans-serif");
        ResizeMode = ResizeMode.NoResize;

        _selectedColor = initialColor ?? "#0D9488";

        var rootGrid = new Grid { Margin = new Thickness(20) };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Name label
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Name input
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Color label
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Color swatches
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

        // 1. Name
        var nameLabel = new TextBlock
        {
            Text = "Group Name (e.g. Work, Snippets, Addresses):",
            FontSize = 13,
            Foreground = new MediaBrush(MediaColor.FromRgb(203, 213, 225)),
            Margin = new Thickness(0, 0, 0, 6)
        };
        Grid.SetRow(nameLabel, 0);
        rootGrid.Children.Add(nameLabel);

        _nameBox = new WpfTextBox
        {
            Text = initialName ?? string.Empty,
            Padding = new Thickness(10, 6, 10, 6),
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 16)
        };
        Grid.SetRow(_nameBox, 1);
        rootGrid.Children.Add(_nameBox);

        // 2. Color Label
        var colorLabel = new TextBlock
        {
            Text = "Group Color:",
            FontSize = 13,
            Foreground = new MediaBrush(MediaColor.FromRgb(203, 213, 225)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(colorLabel, 2);
        rootGrid.Children.Add(colorLabel);

        // 3. Color Swatches
        var colorPanel = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var presetColors = new[]
        {
            "#0D9488", // Teal
            "#3B82F6", // Blue
            "#8B5CF6", // Purple
            "#EC4899", // Pink
            "#EF4444", // Red
            "#F59E0B", // Amber
            "#10B981"  // Emerald
        };

        _colorPreview = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Background = new BrushConverter().ConvertFromString(_selectedColor) as WpfBrush,
            Margin = new Thickness(0, 0, 12, 0),
            BorderThickness = new Thickness(2),
            BorderBrush = MediaBrushes.White
        };
        colorPanel.Children.Add(_colorPreview);

        foreach (var colorHex in presetColors)
        {
            var swatch = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = new BrushConverter().ConvertFromString(colorHex) as WpfBrush,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = WpfCursors.Hand
            };
            swatch.MouseDown += (_, _) =>
            {
                _selectedColor = colorHex;
                _colorPreview.Background = new BrushConverter().ConvertFromString(_selectedColor) as WpfBrush;
            };
            colorPanel.Children.Add(swatch);
        }

        Grid.SetRow(colorPanel, 3);
        rootGrid.Children.Add(colorPanel);

        // 4. Buttons
        var btnStack = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right
        };

        var saveBtn = new WpfButton
        {
            Content = "Save",
            Padding = new Thickness(16, 6, 16, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new MediaBrush(MediaColor.FromRgb(13, 148, 136)),
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Cursor = WpfCursors.Hand,
            IsDefault = true
        };
        saveBtn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                WpfMessageBox.Show("Please enter a group name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        };
        btnStack.Children.Add(saveBtn);

        var cancelBtn = new WpfButton
        {
            Content = "Cancel",
            Padding = new Thickness(14, 6, 14, 6),
            Background = MediaBrushes.Transparent,
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            BorderThickness = new Thickness(0),
            Cursor = WpfCursors.Hand,
            IsCancel = true
        };
        btnStack.Children.Add(cancelBtn);

        Grid.SetRow(btnStack, 5);
        rootGrid.Children.Add(btnStack);

        Content = rootGrid;

        Loaded += (_, _) =>
        {
            _nameBox.Focus();
            _nameBox.SelectAll();
        };
    }
}
