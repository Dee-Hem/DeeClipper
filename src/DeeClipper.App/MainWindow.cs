using System.IO;
using System.Windows;
using System.Windows.Controls;
using WpfBinding = System.Windows.Data.Binding;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeeClipper.App.ViewModels;
using DeeClipper.Core.Models;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.SolidColorBrush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaFontFamily = System.Windows.Media.FontFamily;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfImage = System.Windows.Controls.Image;
using WpfCursors = System.Windows.Input.Cursors;
using WpfMessageBox = System.Windows.MessageBox;

namespace DeeClipper.App;

public class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private WpfListBox _pinnedListBox = null!;
    private WpfListBox _unpinnedListBox = null!;
    private ContentControl _detailHost = null!;
    private TextBlock _statusTextBlock = null!;

    public bool IsExplicitExit { get; set; }

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;

        InitializeWindow();
        BuildUI();

        Loaded += async (_, _) => await _vm.RefreshAsync();
        Closing += OnWindowClosing;
    }

    private void InitializeWindow()
    {
        Title = "DeeClipper";
        Width = 1000;
        Height = 650;
        MinHeight = 480;
        MinWidth = 760;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42)); // Slate-900
        Foreground = new MediaBrush(MediaColor.FromRgb(248, 250, 252));
        FontFamily = new MediaFontFamily("Segoe UI, Segoe UI Variable, sans-serif");
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!IsExplicitExit)
        {
            // Hide to tray instead of quitting
            e.Cancel = true;
            Hide();
        }
    }

    private void BuildUI()
    {
        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Top Bar
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status Bar

        // 1. TOP HEADER (Search, Filter Chips, Clear History)
        var topHeader = BuildTopHeader();
        Grid.SetRow(topHeader, 0);
        mainGrid.Children.Add(topHeader);

        // 2. MAIN BODY (List on Left, Detail on Right)
        var bodyGrid = new Grid();
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Star) });
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Pixel) }); // Separator
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) });

        var leftListPanel = BuildLeftListPanel();
        Grid.SetColumn(leftListPanel, 0);
        bodyGrid.Children.Add(leftListPanel);

        var separator = new Border { Background = new MediaBrush(MediaColor.FromRgb(51, 65, 85)) }; // Slate-700
        Grid.SetColumn(separator, 1);
        bodyGrid.Children.Add(separator);

        var rightDetailPanel = BuildRightDetailPanel();
        Grid.SetColumn(rightDetailPanel, 2);
        bodyGrid.Children.Add(rightDetailPanel);

        Grid.SetRow(bodyGrid, 1);
        mainGrid.Children.Add(bodyGrid);

        // 3. STATUS BAR
        var statusBar = BuildStatusBar();
        Grid.SetRow(statusBar, 2);
        mainGrid.Children.Add(statusBar);

        Content = mainGrid;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedItem))
            {
                UpdateDetailView(_vm.SelectedItem);
            }
            if (e.PropertyName == nameof(MainViewModel.StatusText))
            {
                _statusTextBlock.Text = _vm.StatusText;
            }
        };
    }

    private FrameworkElement BuildTopHeader()
    {
        var border = new Border
        {
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)), // Slate-800
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12, 16, 12)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) }); // Search
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Filters
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Clear History

        // Search Box
        var searchBox = new WpfTextBox
        {
            Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(71, 85, 105)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 6, 10, 6),
            FontSize = 13,
            VerticalContentAlignment = WpfVerticalAlignment.Center
        };
        searchBox.TextChanged += (_, _) => _vm.SearchText = searchBox.Text;
        Grid.SetColumn(searchBox, 0);
        grid.Children.Add(searchBox);

        // Filter Chips Panel
        var chipsPanel = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            VerticalAlignment = WpfVerticalAlignment.Center
        };

        AddFilterChip(chipsPanel, "All", ItemFilter.All);
        AddFilterChip(chipsPanel, "Pinned", ItemFilter.Pinned);
        AddFilterChip(chipsPanel, "Text", ItemFilter.Text);
        AddFilterChip(chipsPanel, "Links", ItemFilter.Links);
        AddFilterChip(chipsPanel, "Images", ItemFilter.Images);
        AddFilterChip(chipsPanel, "Files", ItemFilter.Files);

        Grid.SetColumn(chipsPanel, 2);
        grid.Children.Add(chipsPanel);

        // Clear History Button
        var clearBtn = new WpfButton
        {
            Content = "Clear History",
            Background = new MediaBrush(MediaColor.FromArgb(50, 239, 68, 68)),
            Foreground = new MediaBrush(MediaColor.FromRgb(248, 113, 113)),
            BorderBrush = new MediaBrush(MediaColor.FromArgb(100, 239, 68, 68)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 12, 6),
            FontSize = 12,
            Cursor = WpfCursors.Hand
        };
        clearBtn.Click += async (_, _) =>
        {
            var result = WpfMessageBox.Show(
                "Clear all unpinned clipboard history?\n(Pinned items will NOT be removed)",
                "Confirm Clear History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );
            if (result == MessageBoxResult.Yes)
            {
                await _vm.ClearHistoryAsync();
            }
        };

        Grid.SetColumn(clearBtn, 3);
        grid.Children.Add(clearBtn);

        border.Child = grid;
        return border;
    }

    private void AddFilterChip(StackPanel panel, string label, ItemFilter filter)
    {
        var btn = new WpfButton
        {
            Content = label,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 5, 12, 5),
            FontSize = 12,
            Cursor = WpfCursors.Hand,
            BorderThickness = new Thickness(1)
        };

        void UpdateStyle()
        {
            var isSelected = _vm.SelectedFilter == filter;
            btn.Background = isSelected
                ? new MediaBrush(MediaColor.FromRgb(13, 148, 136)) // Teal-600
                : new MediaBrush(MediaColor.FromRgb(30, 41, 59));
            btn.Foreground = isSelected ? MediaBrushes.White : new MediaBrush(MediaColor.FromRgb(203, 213, 225));
            btn.BorderBrush = isSelected
                ? new MediaBrush(MediaColor.FromRgb(20, 184, 166))
                : new MediaBrush(MediaColor.FromRgb(51, 65, 85));
        }

        UpdateStyle();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedFilter))
            {
                UpdateStyle();
            }
        };

        btn.Click += (_, _) => _vm.SelectedFilter = filter;
        panel.Children.Add(btn);
    }

    private FrameworkElement BuildLeftListPanel()
    {
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42))
        };

        var stack = new StackPanel { Margin = new Thickness(12) };

        // 1. Pinned Section Header
        var pinnedHeader = new TextBlock
        {
            Text = "PINNED ITEMS",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new MediaBrush(MediaColor.FromRgb(245, 158, 11)), // Amber-500
            Margin = new Thickness(4, 4, 4, 8)
        };
        stack.Children.Add(pinnedHeader);

        // Pinned ListBox
        _pinnedListBox = CreateVirtualizedListBox(_vm.PinnedItems);
        stack.Children.Add(_pinnedListBox);

        // 2. Unpinned History Header
        var unpinnedHeader = new TextBlock
        {
            Text = "HISTORY",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)), // Slate-400
            Margin = new Thickness(4, 16, 4, 8)
        };
        stack.Children.Add(unpinnedHeader);

        // Unpinned ListBox
        _unpinnedListBox = CreateVirtualizedListBox(_vm.UnpinnedItems);
        stack.Children.Add(_unpinnedListBox);

        scrollViewer.Content = stack;
        return scrollViewer;
    }

    private WpfListBox CreateVirtualizedListBox(System.Collections.IEnumerable itemsSource)
    {
        var listBox = new WpfListBox
        {
            ItemsSource = itemsSource,
            Background = MediaBrushes.Transparent,
            BorderThickness = new Thickness(0)
        };

        ScrollViewer.SetHorizontalScrollBarVisibility(listBox, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(listBox, ScrollBarVisibility.Disabled);
        ScrollViewer.SetCanContentScroll(listBox, true);
        VirtualizingPanel.SetIsVirtualizing(listBox, true);
        VirtualizingPanel.SetVirtualizationMode(listBox, VirtualizationMode.Recycling);

        listBox.SelectionChanged += (sender, _) =>
        {
            if (sender is WpfListBox lb && lb.SelectedItem is ItemViewModel vm)
            {
                _vm.SelectedItem = vm;
                if (sender == _pinnedListBox) _unpinnedListBox.SelectedIndex = -1;
                else _pinnedListBox.SelectedIndex = -1;
            }
        };

        listBox.MouseDoubleClick += (_, e) =>
        {
            if (listBox.SelectedItem is ItemViewModel vm)
            {
                _vm.CopyItem(vm);
                WpfMessageBox.Show("Copied to clipboard!", "DeeClipper", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };

        // ItemTemplate
        var template = new DataTemplate(typeof(ItemViewModel));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        factory.SetValue(Border.PaddingProperty, new Thickness(10, 8, 10, 8));
        factory.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 4));
        factory.SetValue(Border.BackgroundProperty, new MediaBrush(MediaColor.FromRgb(30, 41, 59)));
        factory.SetValue(Border.BorderBrushProperty, new MediaBrush(MediaColor.FromRgb(51, 65, 85)));
        factory.SetValue(Border.BorderThicknessProperty, new Thickness(1));

        var rowGrid = new FrameworkElementFactory(typeof(Grid));
        var c0 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c0.SetValue(ColumnDefinition.WidthProperty, new GridLength(28));
        var c1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var c2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);

        rowGrid.AppendChild(c0);
        rowGrid.AppendChild(c1);
        rowGrid.AppendChild(c2);

        // Icon
        var iconText = new FrameworkElementFactory(typeof(TextBlock));
        iconText.SetBinding(TextBlock.TextProperty, new WpfBinding(nameof(ItemViewModel.TypeIcon)));
        iconText.SetValue(TextBlock.FontSizeProperty, 15.0);
        iconText.SetValue(TextBlock.VerticalAlignmentProperty, WpfVerticalAlignment.Center);
        iconText.SetValue(Grid.ColumnProperty, 0);
        rowGrid.AppendChild(iconText);

        // Middle Text Stack
        var midStack = new FrameworkElementFactory(typeof(StackPanel));
        midStack.SetValue(Grid.ColumnProperty, 1);
        midStack.SetValue(StackPanel.MarginProperty, new Thickness(8, 0, 8, 0));

        var previewText = new FrameworkElementFactory(typeof(TextBlock));
        previewText.SetBinding(TextBlock.TextProperty, new WpfBinding(nameof(ItemViewModel.DisplayPreview)));
        previewText.SetValue(TextBlock.FontSizeProperty, 13.0);
        previewText.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
        previewText.SetValue(TextBlock.ForegroundProperty, MediaBrushes.White);
        previewText.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        midStack.AppendChild(previewText);

        var metaPanel = new FrameworkElementFactory(typeof(StackPanel));
        metaPanel.SetValue(StackPanel.OrientationProperty, WpfOrientation.Horizontal);
        metaPanel.SetValue(StackPanel.MarginProperty, new Thickness(0, 4, 0, 0));

        var timeText = new FrameworkElementFactory(typeof(TextBlock));
        timeText.SetBinding(TextBlock.TextProperty, new WpfBinding(nameof(ItemViewModel.RelativeTime)));
        timeText.SetValue(TextBlock.FontSizeProperty, 11.0);
        timeText.SetValue(TextBlock.ForegroundProperty, new MediaBrush(MediaColor.FromRgb(148, 163, 184)));
        metaPanel.AppendChild(timeText);

        var appText = new FrameworkElementFactory(typeof(TextBlock));
        appText.SetBinding(TextBlock.TextProperty, new WpfBinding(nameof(ItemViewModel.SourceApp)));
        appText.SetValue(TextBlock.FontSizeProperty, 11.0);
        appText.SetValue(TextBlock.ForegroundProperty, new MediaBrush(MediaColor.FromRgb(56, 189, 248))); // Sky-400
        appText.SetValue(TextBlock.MarginProperty, new Thickness(10, 0, 0, 0));
        metaPanel.AppendChild(appText);

        var tagText = new FrameworkElementFactory(typeof(TextBlock));
        tagText.SetBinding(TextBlock.TextProperty, new WpfBinding(nameof(ItemViewModel.PrimaryTag)));
        tagText.SetValue(TextBlock.FontSizeProperty, 11.0);
        tagText.SetValue(TextBlock.ForegroundProperty, new MediaBrush(MediaColor.FromRgb(52, 211, 153))); // Emerald-400
        tagText.SetValue(TextBlock.MarginProperty, new Thickness(10, 0, 0, 0));
        metaPanel.AppendChild(tagText);

        midStack.AppendChild(metaPanel);
        rowGrid.AppendChild(midStack);

        // Right Actions (Pin, Delete)
        var actionsPanel = new FrameworkElementFactory(typeof(StackPanel));
        actionsPanel.SetValue(StackPanel.OrientationProperty, WpfOrientation.Horizontal);
        actionsPanel.SetValue(Grid.ColumnProperty, 2);
        actionsPanel.SetValue(FrameworkElement.VerticalAlignmentProperty, WpfVerticalAlignment.Center);

        var pinBtn = new FrameworkElementFactory(typeof(WpfButton));
        pinBtn.SetBinding(WpfButton.ContentProperty, new WpfBinding(nameof(ItemViewModel.PinButtonLabel)));
        pinBtn.SetValue(WpfButton.BackgroundProperty, MediaBrushes.Transparent);
        pinBtn.SetValue(WpfButton.BorderThicknessProperty, new Thickness(0));
        pinBtn.SetValue(WpfButton.ForegroundProperty, new MediaBrush(MediaColor.FromRgb(245, 158, 11)));
        pinBtn.SetValue(WpfButton.FontSizeProperty, 15.0);
        pinBtn.SetValue(WpfButton.PaddingProperty, new Thickness(4));
        pinBtn.SetValue(WpfButton.CursorProperty, WpfCursors.Hand);
        pinBtn.AddHandler(WpfButton.ClickEvent, new RoutedEventHandler(async (s, _) =>
        {
            if (s is FrameworkElement fe && fe.DataContext is ItemViewModel vm)
            {
                await _vm.TogglePinAsync(vm);
            }
        }));
        actionsPanel.AppendChild(pinBtn);

        var delBtn = new FrameworkElementFactory(typeof(WpfButton));
        delBtn.SetValue(WpfButton.ContentProperty, "✕");
        delBtn.SetValue(WpfButton.BackgroundProperty, MediaBrushes.Transparent);
        delBtn.SetValue(WpfButton.BorderThicknessProperty, new Thickness(0));
        delBtn.SetValue(WpfButton.ForegroundProperty, new MediaBrush(MediaColor.FromRgb(239, 68, 68)));
        delBtn.SetValue(WpfButton.FontSizeProperty, 13.0);
        delBtn.SetValue(WpfButton.PaddingProperty, new Thickness(4));
        delBtn.SetValue(WpfButton.CursorProperty, WpfCursors.Hand);
        delBtn.AddHandler(WpfButton.ClickEvent, new RoutedEventHandler(async (s, _) =>
        {
            if (s is FrameworkElement fe && fe.DataContext is ItemViewModel vm)
            {
                await _vm.DeleteItemAsync(vm);
            }
        }));
        actionsPanel.AppendChild(delBtn);

        rowGrid.AppendChild(actionsPanel);
        factory.AppendChild(rowGrid);
        template.VisualTree = factory;

        listBox.ItemTemplate = template;
        return listBox;
    }

    private FrameworkElement BuildRightDetailPanel()
    {
        var border = new Border
        {
            Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42)),
            Padding = new Thickness(20)
        };

        _detailHost = new ContentControl();
        border.Child = _detailHost;

        UpdateDetailView(_vm.SelectedItem);
        return border;
    }

    private void UpdateDetailView(ItemViewModel? item)
    {
        if (item == null)
        {
            _detailHost.Content = new TextBlock
            {
                Text = "Select an item to view details",
                Foreground = new MediaBrush(MediaColor.FromRgb(100, 116, 139)),
                FontSize = 14,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = WpfVerticalAlignment.Center
            };
            return;
        }

        var mainPanel = new Grid();
        mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header & actions
        mainPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
        mainPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Metadata footer

        // Header
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var metaStack = new StackPanel();
        var titleText = new TextBlock
        {
            Text = $"{item.TypeIcon} {item.Type} • {item.SourceApp}",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = MediaBrushes.White
        };
        metaStack.Children.Add(titleText);

        var timeText = new TextBlock
        {
            Text = $"Copied: {item.LastUsedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} ({item.RelativeTime})",
            FontSize = 12,
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 4, 0, 0)
        };
        metaStack.Children.Add(timeText);
        headerGrid.Children.Add(metaStack);

        // Action buttons
        var actionPanel = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            VerticalAlignment = WpfVerticalAlignment.Center
        };

        if (item.IsSensitive)
        {
            var eyeBtn = new WpfButton
            {
                Content = item.EyeIcon + " Reveal",
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Cursor = WpfCursors.Hand
            };
            eyeBtn.Click += (_, _) =>
            {
                item.IsMasked = !item.IsMasked;
                UpdateDetailView(item);
            };
            actionPanel.Children.Add(eyeBtn);

            var notSensBtn = new WpfButton
            {
                Content = "Not sensitive",
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Cursor = WpfCursors.Hand
            };
            notSensBtn.Click += async (_, _) =>
            {
                await _vm.MarkNotSensitiveAsync(item);
                UpdateDetailView(item);
            };
            actionPanel.Children.Add(notSensBtn);
        }

        var copyBtn = new WpfButton
        {
            Content = "📋 Copy",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 11,
            Background = new MediaBrush(MediaColor.FromRgb(13, 148, 136)),
            Foreground = MediaBrushes.White,
            Cursor = WpfCursors.Hand
        };
        copyBtn.Click += (_, _) =>
        {
            _vm.CopyItem(item);
            WpfMessageBox.Show("Copied to clipboard!", "DeeClipper", MessageBoxButton.OK, MessageBoxImage.Information);
        };
        actionPanel.Children.Add(copyBtn);

        Grid.SetColumn(actionPanel, 1);
        headerGrid.Children.Add(actionPanel);
        Grid.SetRow(headerGrid, 0);
        mainPanel.Children.Add(headerGrid);

        // Content
        FrameworkElement contentElement;
        if (item.Type == ClipboardItemType.Image)
        {
            var imgPath = !string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath)
                ? item.ImagePath
                : item.ThumbPath;

            if (imgPath != null)
            {
                var img = new WpfImage
                {
                    Source = new BitmapImage(new Uri(Path.GetFullPath(imgPath))),
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = WpfHorizontalAlignment.Center,
                    VerticalAlignment = WpfVerticalAlignment.Center
                };
                contentElement = img;
            }
            else
            {
                contentElement = new TextBlock { Text = "Image file not found on disk.", Foreground = MediaBrushes.Red };
            }
        }
        else
        {
            var textBox = new WpfTextBox
            {
                Text = item.DisplayFullText,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
                Foreground = MediaBrushes.White,
                BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
                Padding = new Thickness(12),
                FontSize = 13
            };
            contentElement = textBox;
        }

        Grid.SetRow(contentElement, 1);
        mainPanel.Children.Add(contentElement);

        // Footer Metadata
        var footer = new Border
        {
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 10, 0, 0),
            Margin = new Thickness(0, 12, 0, 0)
        };
        var footerText = new TextBlock
        {
            Text = $"Size: {item.ByteSize:N0} bytes • Used: {item.UseCount} time(s) • Pinned: {(item.IsPinned ? "Yes" : "No")}",
            FontSize = 12,
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184))
        };
        footer.Child = footerText;
        Grid.SetRow(footer, 2);
        mainPanel.Children.Add(footer);

        _detailHost.Content = mainPanel;
    }

    private FrameworkElement BuildStatusBar()
    {
        var border = new Border
        {
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 6, 16, 6)
        };

        _statusTextBlock = new TextBlock
        {
            Text = _vm.StatusText,
            FontSize = 12,
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184))
        };

        border.Child = _statusTextBlock;
        return border;
    }
}
