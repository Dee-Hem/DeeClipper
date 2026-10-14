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
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfGroupBox = System.Windows.Controls.GroupBox;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfCursors = System.Windows.Input.Cursors;

namespace DeeClipper.App.Dialogs;

public class MergeDialog : Window
{
    private readonly IReadOnlyList<string> _texts;
    private WpfRadioButton _rbNewline = null!;
    private WpfRadioButton _rbSpace = null!;
    private WpfRadioButton _rbComma = null!;
    private WpfRadioButton _rbCustom = null!;
    private WpfTextBox _customBox = null!;
    private TextBlock _previewText = null!;

    public string ResultText { get; private set; } = string.Empty;

    public MergeDialog(IReadOnlyList<string> texts, Window? owner = null)
    {
        _texts = texts;

        Title = "Merge Selected Items - DeeClipper";
        Width = 520;
        Height = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = owner;
        Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42)); // Slate-900
        Foreground = MediaBrushes.White;
        FontFamily = new MediaFontFamily("Segoe UI, sans-serif");

        var rootGrid = new Grid { Margin = new Thickness(20) };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Separator Choices
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Preview
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

        // Header
        var header = new TextBlock
        {
            Text = $"Merge {_texts.Count} selected text items into a new item",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 0, 0, 14)
        };
        Grid.SetRow(header, 0);
        rootGrid.Children.Add(header);

        // Separator Choices
        var sepBox = new WpfGroupBox
        {
            Header = "Select Separator",
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var sepStack = new StackPanel();

        _rbNewline = new WpfRadioButton
        {
            Content = "New line (\\n)",
            IsChecked = true,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        };
        _rbNewline.Checked += (_, _) => UpdateMergedPreview();
        sepStack.Children.Add(_rbNewline);

        _rbSpace = new WpfRadioButton
        {
            Content = "Space ( )",
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        };
        _rbSpace.Checked += (_, _) => UpdateMergedPreview();
        sepStack.Children.Add(_rbSpace);

        _rbComma = new WpfRadioButton
        {
            Content = "Comma with space (, )",
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        };
        _rbComma.Checked += (_, _) => UpdateMergedPreview();
        sepStack.Children.Add(_rbComma);

        var customGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        customGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        customGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _rbCustom = new WpfRadioButton
        {
            Content = "Custom: ",
            Foreground = MediaBrushes.White,
            VerticalAlignment = WpfVerticalAlignment.Center
        };
        _rbCustom.Checked += (_, _) => UpdateMergedPreview();
        customGrid.Children.Add(_rbCustom);

        _customBox = new WpfTextBox
        {
            Text = " --- ",
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(6, 2, 6, 2),
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85))
        };
        _customBox.TextChanged += (_, _) =>
        {
            _rbCustom.IsChecked = true;
            UpdateMergedPreview();
        };
        Grid.SetColumn(_customBox, 1);
        customGrid.Children.Add(_customBox);

        sepStack.Children.Add(customGrid);
        sepBox.Content = sepStack;
        Grid.SetRow(sepBox, 1);
        rootGrid.Children.Add(sepBox);

        // Preview Box
        var prevBox = new WpfGroupBox
        {
            Header = "Preview",
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 0, 14)
        };

        _previewText = new TextBlock
        {
            Foreground = new MediaBrush(MediaColor.FromRgb(203, 213, 225)),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            FontFamily = new MediaFontFamily("Consolas, Segoe UI, monospace")
        };
        prevBox.Content = new ScrollViewer
        {
            Content = _previewText,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(prevBox, 2);
        rootGrid.Children.Add(prevBox);

        // Buttons
        var btnStack = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right
        };

        var mergeBtn = new WpfButton
        {
            Content = "Merge Items",
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new MediaBrush(MediaColor.FromRgb(13, 148, 136)),
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Cursor = WpfCursors.Hand,
            IsDefault = true
        };
        mergeBtn.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
        btnStack.Children.Add(mergeBtn);

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

        Grid.SetRow(btnStack, 3);
        rootGrid.Children.Add(btnStack);

        Content = rootGrid;
        UpdateMergedPreview();
    }

    private void UpdateMergedPreview()
    {
        var sep = Environment.NewLine;
        if (_rbSpace.IsChecked == true) sep = " ";
        else if (_rbComma.IsChecked == true) sep = ", ";
        else if (_rbCustom.IsChecked == true) sep = _customBox.Text;

        ResultText = string.Join(sep, _texts);
        _previewText.Text = ResultText.Length > 500
            ? ResultText[..500] + "... (truncated preview)"
            : ResultText;
    }
}
