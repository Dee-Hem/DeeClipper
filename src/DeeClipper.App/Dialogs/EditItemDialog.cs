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
using WpfCursors = System.Windows.Input.Cursors;

namespace DeeClipper.App.Dialogs;

public enum EditItemAction
{
    Cancel,
    Replace,
    SaveAsNew
}

public class EditItemDialog : Window
{
    private readonly WpfTextBox _textBox;
    private readonly TextBlock _countText;

    public EditItemAction UserAction { get; private set; } = EditItemAction.Cancel;
    public string EditedText => _textBox.Text;

    public EditItemDialog(string initialText, Window? owner = null)
    {
        Title = "Edit Text Item - DeeClipper";
        Width = 560;
        Height = 420;
        MinWidth = 400;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = owner;
        Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42)); // Slate-900
        Foreground = MediaBrushes.White;
        FontFamily = new MediaFontFamily("Segoe UI, sans-serif");

        var rootGrid = new Grid { Margin = new Thickness(16) };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Editor
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer buttons

        // Header
        var header = new TextBlock
        {
            Text = "Edit Item Content",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(header, 0);
        rootGrid.Children.Add(header);

        // Editor
        _textBox = new WpfTextBox
        {
            Text = initialText ?? string.Empty,
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            Padding = new Thickness(10),
            FontSize = 13,
            FontFamily = new MediaFontFamily("Consolas, Segoe UI, monospace")
        };
        _textBox.TextChanged += (_, _) => UpdateCount();
        Grid.SetRow(_textBox, 1);
        rootGrid.Children.Add(_textBox);

        // Footer Grid
        var footerGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _countText = new TextBlock
        {
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            FontSize = 11,
            VerticalAlignment = WpfVerticalAlignment.Center
        };
        footerGrid.Children.Add(_countText);

        var btnStack = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            VerticalAlignment = WpfVerticalAlignment.Center
        };

        var replaceBtn = new WpfButton
        {
            Content = "Replace Item",
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new MediaBrush(MediaColor.FromRgb(13, 148, 136)),
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Cursor = WpfCursors.Hand,
            IsDefault = true
        };
        replaceBtn.Click += (_, _) =>
        {
            UserAction = EditItemAction.Replace;
            DialogResult = true;
            Close();
        };
        btnStack.Children.Add(replaceBtn);

        var saveNewBtn = new WpfButton
        {
            Content = "Save as New",
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = new MediaBrush(MediaColor.FromRgb(56, 189, 248)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            Cursor = WpfCursors.Hand
        };
        saveNewBtn.Click += (_, _) =>
        {
            UserAction = EditItemAction.SaveAsNew;
            DialogResult = true;
            Close();
        };
        btnStack.Children.Add(saveNewBtn);

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

        Grid.SetColumn(btnStack, 1);
        footerGrid.Children.Add(btnStack);

        Grid.SetRow(footerGrid, 2);
        rootGrid.Children.Add(footerGrid);

        Content = rootGrid;
        UpdateCount();

        Loaded += (_, _) =>
        {
            _textBox.Focus();
            _textBox.SelectAll();
        };
    }

    private void UpdateCount()
    {
        var len = _textBox.Text.Length;
        var lines = _textBox.LineCount;
        _countText.Text = $"{len:N0} characters · {lines:N0} lines";
    }
}
