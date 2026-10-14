using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeeClipper.App.Dialogs;
using DeeClipper.App.Services;
using DeeClipper.App.ViewModels;
using DeeClipper.Core.Models;
using DeeClipper.Core.Settings;
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
using WpfBinding = System.Windows.Data.Binding;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfSeparator = System.Windows.Controls.Separator;
using WpfBrush = System.Windows.Media.Brush;

namespace DeeClipper.App;

public class PopupWindow : Window
{
    private readonly PopupViewModel _vm;
    private readonly PasteEngine _pasteEngine;
    private readonly AppSettings _settings;

    private WpfTextBox _searchBox = null!;
    private WpfListBox _listBox = null!;
    private Border _previewPanel = null!;
    private ContentControl _previewContent = null!;
    private ColumnDefinition _previewColDef = null!;
    private ColumnDefinition _sepColDef = null!;
    private StackPanel _groupChipsPanel = null!;

    public PopupWindow(PopupViewModel vm, PasteEngine pasteEngine, AppSettings settings)
    {
        _vm = vm;
        _pasteEngine = pasteEngine;
        _settings = settings;
        DataContext = _vm;

        InitializeWindow();
        BuildUI();

        SourceInitialized += OnSourceInitialized;
        Deactivated += OnDeactivated;
        SizeChanged += OnSizeChanged;
        PreviewKeyDown += OnWindowPreviewKeyDown;
    }

    private void InitializeWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Topmost = true;
        ShowInTaskbar = false;
        Background = MediaBrushes.Transparent;
        Width = Math.Max(500, _settings.PopupWidth);
        Height = Math.Max(350, _settings.PopupHeight);
        FontFamily = new MediaFontFamily("Segoe UI, Segoe UI Variable, sans-serif");
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Set WS_EX_TOOLWINDOW so the popup window never appears in Alt+Tab
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(exStyle));
        NativeMethods.ApplyModernWindowRounding(hwnd);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsVisible && WindowState == WindowState.Normal)
        {
            _settings.PopupWidth = ActualWidth;
            _settings.PopupHeight = ActualHeight;
        }
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (IsVisible)
        {
            ClosePopup();
        }
    }

    public void OpenPopup()
    {
        _pasteEngine.RememberForegroundWindow();

        PositionOnActiveMonitor();

        _searchBox.Text = string.Empty;
        _vm.SearchText = string.Empty;
        _ = _vm.RefreshItemsAsync();

        Show();
        Activate();
        _searchBox.Focus();

        if (_vm.FilteredItems.Count > 0)
        {
            _listBox.SelectedIndex = 0;
        }
    }

    public void ClosePopup()
    {
        Hide();
    }

    private void PositionOnActiveMonitor()
    {
        NativeMethods.GetCursorPos(out var cursorPos);
        var hMonitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);

        var mi = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        NativeMethods.GetMonitorInfo(hMonitor, ref mi);

        var workLeft = mi.rcWork.Left;
        var workTop = mi.rcWork.Top;
        var workRight = mi.rcWork.Right;
        var workBottom = mi.rcWork.Bottom;
        var workWidth = workRight - workLeft;
        var workHeight = workBottom - workTop;

        var targetWidth = Math.Min(Width, workWidth - 40);
        var targetHeight = Math.Min(Height, workHeight - 40);

        Width = targetWidth;
        Height = targetHeight;

        double targetLeft, targetTop;
        if (_settings.PopupPositionNearCursor)
        {
            targetLeft = cursorPos.X - 120;
            targetTop = cursorPos.Y + 24;
        }
        else
        {
            targetLeft = workLeft + (workWidth - targetWidth) / 2.0;
            targetTop = workTop + (workHeight - targetHeight) / 2.0;
        }

        // Keep inside work area
        Left = Math.Max(workLeft + 16, Math.Min(targetLeft, workRight - targetWidth - 16));
        Top = Math.Max(workTop + 16, Math.Min(targetTop, workBottom - targetHeight - 16));
    }

    private void BuildUI()
    {
        // Outer Shadow & Border
        var outerBorder = new Border
        {
            Background = new MediaBrush(MediaColor.FromArgb(250, 15, 23, 42)), // Slate-900 (opaque/translucent)
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),     // Slate-700
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 24,
                ShadowDepth = 6,
                Opacity = 0.5,
                Color = Colors.Black
            }
        };

        var rootGrid = new Grid { Margin = new Thickness(0) };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header (Search & Filter Chips)
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Keyboard hints footer

        // 1. HEADER
        var header = BuildHeader();
        Grid.SetRow(header, 0);
        rootGrid.Children.Add(header);

        // 2. BODY (List + Toggleable Preview Pane)
        var bodyGrid = new Grid();
        bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // List
        _sepColDef = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Pixel) }; // Separator
        _previewColDef = new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) }; // Preview

        bodyGrid.ColumnDefinitions.Add(_sepColDef);
        bodyGrid.ColumnDefinitions.Add(_previewColDef);

        // List with designed empty state
        var listContainer = new Grid();
        _listBox = CreateResultListBox();
        listContainer.Children.Add(_listBox);

        var emptyPanel = new StackPanel
        {
            VerticalAlignment = WpfVerticalAlignment.Center,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            Margin = new Thickness(20),
            IsHitTestVisible = false
        };
        emptyPanel.Children.Add(new TextBlock
        {
            Text = "📋",
            FontSize = 32,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });
        emptyPanel.Children.Add(new TextBlock
        {
            Text = "No matching clips",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = MediaBrushes.White,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        });
        emptyPanel.Children.Add(new TextBlock
        {
            Text = "Copy text, links, or images with Ctrl+C to get started",
            FontSize = 12,
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            HorizontalAlignment = WpfHorizontalAlignment.Center
        });
        emptyPanel.Visibility = _vm.FilteredItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _vm.FilteredItems.CollectionChanged += (_, _) =>
        {
            emptyPanel.Visibility = _vm.FilteredItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        };
        listContainer.Children.Add(emptyPanel);

        Grid.SetColumn(listContainer, 0);
        bodyGrid.Children.Add(listContainer);

        // Separator
        var sep = new Border { Background = new MediaBrush(MediaColor.FromRgb(51, 65, 85)) };
        Grid.SetColumn(sep, 1);
        bodyGrid.Children.Add(sep);

        // Preview Pane
        _previewPanel = new Border
        {
            Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42)),
            Padding = new Thickness(16)
        };
        _previewContent = new ContentControl();
        _previewPanel.Child = _previewContent;

        Grid.SetColumn(_previewPanel, 2);
        bodyGrid.Children.Add(_previewPanel);

        UpdatePreviewVisibility();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PopupViewModel.ShowPreviewPane))
            {
                UpdatePreviewVisibility();
            }
            if (e.PropertyName == nameof(PopupViewModel.SelectedItem))
            {
                UpdatePreviewContent(_vm.SelectedItem);
            }
        };

        _vm.Groups.CollectionChanged += (_, _) => UpdateGroupChips();

        Grid.SetRow(bodyGrid, 1);
        rootGrid.Children.Add(bodyGrid);

        // 3. FOOTER (Keyboard hints)
        var footer = BuildFooter();
        Grid.SetRow(footer, 2);
        rootGrid.Children.Add(footer);

        outerBorder.Child = rootGrid;
        Content = outerBorder;
    }

    private void UpdatePreviewVisibility()
    {
        if (_vm.ShowPreviewPane)
        {
            _sepColDef.Width = new GridLength(1, GridUnitType.Pixel);
            _previewColDef.Width = new GridLength(0.8, GridUnitType.Star);
            _previewPanel.Visibility = Visibility.Visible;
            UpdatePreviewContent(_vm.SelectedItem);
        }
        else
        {
            _sepColDef.Width = new GridLength(0);
            _previewColDef.Width = new GridLength(0);
            _previewPanel.Visibility = Visibility.Collapsed;
        }
    }

    private FrameworkElement BuildHeader()
    {
        var border = new Border
        {
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(12, 12, 0, 0),
            Padding = new Thickness(14, 10, 14, 10)
        };

        var stack = new StackPanel();

        // Row 1: Search Box + Preview Toggle Button
        var searchGrid = new Grid();
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _searchBox = new WpfTextBox
        {
            Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(71, 85, 105)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 6, 10, 6),
            FontSize = 14,
            VerticalContentAlignment = WpfVerticalAlignment.Center
        };
        _searchBox.TextChanged += (_, _) => _vm.SearchText = _searchBox.Text;
        Grid.SetColumn(_searchBox, 0);
        searchGrid.Children.Add(_searchBox);

        var previewToggleBtn = new WpfButton
        {
            Content = "👁 Preview",
            Margin = new Thickness(10, 0, 0, 0),
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 11,
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(71, 85, 105)),
            Cursor = WpfCursors.Hand
        };
        previewToggleBtn.Click += (_, _) => _vm.ShowPreviewPane = !_vm.ShowPreviewPane;
        Grid.SetColumn(previewToggleBtn, 1);
        searchGrid.Children.Add(previewToggleBtn);

        stack.Children.Add(searchGrid);

        // Row 2: Filter Chips + Group Chips (in a horizontal scroll viewer)
        var chipsScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var chipsPanel = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal
        };

        AddFilterChip(chipsPanel, "All", ItemFilter.All);
        AddFilterChip(chipsPanel, "Pinned", ItemFilter.Pinned);
        AddFilterChip(chipsPanel, "Text", ItemFilter.Text);
        AddFilterChip(chipsPanel, "Links", ItemFilter.Links);
        AddFilterChip(chipsPanel, "Images", ItemFilter.Images);
        AddFilterChip(chipsPanel, "Files", ItemFilter.Files);

        _groupChipsPanel = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            Margin = new Thickness(8, 0, 0, 0)
        };
        chipsPanel.Children.Add(_groupChipsPanel);
        UpdateGroupChips();

        chipsScroll.Content = chipsPanel;
        stack.Children.Add(chipsScroll);

        border.Child = stack;
        return border;
    }

    private void AddFilterChip(StackPanel panel, string label, ItemFilter filter)
    {
        var btn = new WpfButton
        {
            Content = label,
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(10, 3, 10, 3),
            FontSize = 11,
            Cursor = WpfCursors.Hand,
            BorderThickness = new Thickness(1)
        };

        void UpdateChipStyle()
        {
            var isSelected = _vm.SelectedFilter == filter && !_vm.SelectedGroupId.HasValue;
            btn.Background = isSelected
                ? new MediaBrush(MediaColor.FromRgb(13, 148, 136))
                : new MediaBrush(MediaColor.FromRgb(15, 23, 42));
            btn.Foreground = isSelected ? MediaBrushes.White : new MediaBrush(MediaColor.FromRgb(203, 213, 225));
            btn.BorderBrush = isSelected
                ? new MediaBrush(MediaColor.FromRgb(20, 184, 166))
                : new MediaBrush(MediaColor.FromRgb(51, 65, 85));
        }

        UpdateChipStyle();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(PopupViewModel.SelectedFilter) or nameof(PopupViewModel.SelectedGroupId))
            {
                UpdateChipStyle();
            }
        };

        btn.Click += (_, _) =>
        {
            _vm.SelectedGroupId = null;
            _vm.SelectedFilter = filter;
        };
        panel.Children.Add(btn);
    }

    private void UpdateGroupChips()
    {
        if (_groupChipsPanel == null) return;
        _groupChipsPanel.Children.Clear();

        if (_vm.Groups.Count == 0) return;

        var sep = new Border
        {
            Width = 1,
            Height = 16,
            Background = new MediaBrush(MediaColor.FromRgb(71, 85, 105)),
            Margin = new Thickness(2, 0, 8, 0)
        };
        _groupChipsPanel.Children.Add(sep);

        foreach (var group in _vm.Groups)
        {
            var g = group;
            var chipBtn = new WpfButton
            {
                Margin = new Thickness(0, 0, 6, 0),
                Padding = new Thickness(8, 3, 8, 3),
                FontSize = 11,
                Cursor = WpfCursors.Hand,
                BorderThickness = new Thickness(1)
            };

            var stack = new StackPanel { Orientation = WpfOrientation.Horizontal };
            var dot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 5, 0),
                Background = TryParseColorBrush(g.Color)
            };
            stack.Children.Add(dot);

            var text = new TextBlock { Text = g.Name };
            stack.Children.Add(text);
            chipBtn.Content = stack;

            void UpdateChipStyle()
            {
                var isSelected = _vm.SelectedGroupId == g.Id;
                chipBtn.Background = isSelected
                    ? new MediaBrush(MediaColor.FromRgb(13, 148, 136))
                    : new MediaBrush(MediaColor.FromRgb(15, 23, 42));
                text.Foreground = isSelected ? MediaBrushes.White : new MediaBrush(MediaColor.FromRgb(203, 213, 225));
                chipBtn.BorderBrush = isSelected
                    ? new MediaBrush(MediaColor.FromRgb(20, 184, 166))
                    : new MediaBrush(MediaColor.FromRgb(51, 65, 85));
            }

            UpdateChipStyle();
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PopupViewModel.SelectedGroupId))
                {
                    UpdateChipStyle();
                }
            };

            chipBtn.Click += (_, _) =>
            {
                _vm.SelectedGroupId = _vm.SelectedGroupId == g.Id ? null : g.Id;
            };

            _groupChipsPanel.Children.Add(chipBtn);
        }
    }

    private WpfListBox CreateResultListBox()
    {
        var listBox = new WpfListBox
        {
            ItemsSource = _vm.FilteredItems,
            Background = MediaBrushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6)
        };

        ScrollViewer.SetHorizontalScrollBarVisibility(listBox, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(listBox, ScrollBarVisibility.Auto);
        ScrollViewer.SetCanContentScroll(listBox, true);
        VirtualizingPanel.SetIsVirtualizing(listBox, true);
        VirtualizingPanel.SetVirtualizationMode(listBox, VirtualizationMode.Recycling);

        listBox.SelectionChanged += (_, _) =>
        {
            if (listBox.SelectedItem is PopupItemViewModel item)
            {
                _vm.SelectedItem = item;
            }
        };

        listBox.MouseDoubleClick += async (_, _) =>
        {
            if (listBox.SelectedItem is PopupItemViewModel item)
            {
                ClosePopup();
                await _vm.PasteSelectedItemAsync(PasteMode.DirectPaste);
            }
        };

        // Template with Number Badge (1..9), icon, preview, tags
        var template = new DataTemplate(typeof(PopupItemViewModel));
        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        factory.SetValue(Border.PaddingProperty, new Thickness(8, 6, 8, 6));
        factory.SetValue(Border.MarginProperty, new Thickness(0, 0, 0, 3));
        factory.SetValue(Border.BackgroundProperty, new MediaBrush(MediaColor.FromRgb(30, 41, 59)));
        factory.SetValue(Border.BorderBrushProperty, new MediaBrush(MediaColor.FromRgb(51, 65, 85)));
        factory.SetValue(Border.BorderThicknessProperty, new Thickness(1));

        var rowGrid = new FrameworkElementFactory(typeof(Grid));
        var c0 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c0.SetValue(ColumnDefinition.WidthProperty, new GridLength(28)); // Number badge
        var c1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c1.SetValue(ColumnDefinition.WidthProperty, new GridLength(24)); // Type icon
        var c2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c2.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star)); // Text & metadata
        var c3 = new FrameworkElementFactory(typeof(ColumnDefinition));
        c3.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto); // Pin indicator

        rowGrid.AppendChild(c0);
        rowGrid.AppendChild(c1);
        rowGrid.AppendChild(c2);
        rowGrid.AppendChild(c3);

        // Number Badge
        var badgeBorder = new FrameworkElementFactory(typeof(Border));
        badgeBorder.SetValue(Border.BackgroundProperty, new MediaBrush(MediaColor.FromArgb(80, 71, 85, 105)));
        badgeBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        badgeBorder.SetValue(Border.WidthProperty, 20.0);
        badgeBorder.SetValue(Border.HeightProperty, 20.0);
        badgeBorder.SetValue(Grid.ColumnProperty, 0);
        badgeBorder.SetValue(FrameworkElement.VerticalAlignmentProperty, WpfVerticalAlignment.Center);

        var badgeText = new FrameworkElementFactory(typeof(TextBlock));
        badgeText.SetBinding(TextBlock.TextProperty, new WpfBinding(nameof(PopupItemViewModel.NumberBadge)));
        badgeText.SetValue(TextBlock.FontSizeProperty, 11.0);
        badgeText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        badgeText.SetValue(TextBlock.ForegroundProperty, new MediaBrush(MediaColor.FromRgb(148, 163, 184)));
        badgeText.SetValue(TextBlock.HorizontalAlignmentProperty, WpfHorizontalAlignment.Center);
        badgeText.SetValue(TextBlock.VerticalAlignmentProperty, WpfVerticalAlignment.Center);
        badgeBorder.AppendChild(badgeText);
        rowGrid.AppendChild(badgeBorder);

        // Icon
        var iconText = new FrameworkElementFactory(typeof(TextBlock));
        iconText.SetBinding(TextBlock.TextProperty, new WpfBinding(nameof(PopupItemViewModel.TypeIcon)));
        iconText.SetValue(TextBlock.FontSizeProperty, 13.0);
        iconText.SetValue(TextBlock.VerticalAlignmentProperty, WpfVerticalAlignment.Center);
        iconText.SetValue(Grid.ColumnProperty, 1);
        rowGrid.AppendChild(iconText);

        // Middle Text
        var textStack = new FrameworkElementFactory(typeof(StackPanel));
        textStack.SetValue(Grid.ColumnProperty, 2);
        textStack.SetValue(StackPanel.MarginProperty, new Thickness(6, 0, 6, 0));

        var previewText = new FrameworkElementFactory(typeof(TextBlock));
        previewText.SetBinding(TextBlock.TextProperty, new WpfBinding(nameof(PopupItemViewModel.DisplayPreview)));
        previewText.SetValue(TextBlock.FontSizeProperty, 12.0);
        previewText.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
        previewText.SetValue(TextBlock.ForegroundProperty, MediaBrushes.White);
        previewText.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        textStack.AppendChild(previewText);

        var metaPanel = new FrameworkElementFactory(typeof(StackPanel));
        metaPanel.SetValue(StackPanel.OrientationProperty, WpfOrientation.Horizontal);
        metaPanel.SetValue(StackPanel.MarginProperty, new Thickness(0, 2, 0, 0));

        var timeText = new FrameworkElementFactory(typeof(TextBlock));
        timeText.SetBinding(TextBlock.TextProperty, new WpfBinding(nameof(PopupItemViewModel.RelativeTime)));
        timeText.SetValue(TextBlock.FontSizeProperty, 10.0);
        timeText.SetValue(TextBlock.ForegroundProperty, new MediaBrush(MediaColor.FromRgb(148, 163, 184)));
        metaPanel.AppendChild(timeText);

        var appText = new FrameworkElementFactory(typeof(TextBlock));
        appText.SetBinding(TextBlock.TextProperty, new WpfBinding(nameof(PopupItemViewModel.SourceApp)));
        appText.SetValue(TextBlock.FontSizeProperty, 10.0);
        appText.SetValue(TextBlock.ForegroundProperty, new MediaBrush(MediaColor.FromRgb(56, 189, 248)));
        appText.SetValue(TextBlock.MarginProperty, new Thickness(8, 0, 0, 0));
        metaPanel.AppendChild(appText);

        textStack.AppendChild(metaPanel);
        rowGrid.AppendChild(textStack);

        // Pin Icon / Slot on Right
        var pinIcon = new FrameworkElementFactory(typeof(TextBlock));
        pinIcon.SetBinding(TextBlock.TextProperty, new WpfBinding(nameof(PopupItemViewModel.PinButtonLabel)));
        pinIcon.SetValue(TextBlock.ForegroundProperty, new MediaBrush(MediaColor.FromRgb(245, 158, 11)));
        pinIcon.SetValue(TextBlock.FontSizeProperty, 13.0);
        pinIcon.SetValue(TextBlock.VerticalAlignmentProperty, WpfVerticalAlignment.Center);
        pinIcon.SetValue(Grid.ColumnProperty, 3);
        rowGrid.AppendChild(pinIcon);

        factory.AppendChild(rowGrid);
        template.VisualTree = factory;
        listBox.ItemTemplate = template;

        // Context menu on popup items
        listBox.ContextMenuOpening += (s, e) =>
        {
            if (listBox.SelectedItem is PopupItemViewModel item)
            {
                listBox.ContextMenu = BuildPopupContextMenu(item);
            }
            else
            {
                e.Handled = true;
            }
        };

        return listBox;
    }

    private WpfContextMenu BuildPopupContextMenu(PopupItemViewModel item)
    {
        var cm = new WpfContextMenu();

        var pasteMi = new WpfMenuItem { Header = "↵ Paste" };
        pasteMi.Click += async (_, _) =>
        {
            ClosePopup();
            await _vm.PasteSelectedItemAsync(PasteMode.DirectPaste);
        };
        cm.Items.Add(pasteMi);

        var pasteAsSub = new WpfMenuItem { Header = "Paste as (Ctrl+T)" };
        AddPopupPasteAsSubmenu(pasteAsSub, item);
        cm.Items.Add(pasteAsSub);

        cm.Items.Add(new WpfSeparator());

        var pinMi = new WpfMenuItem { Header = item.IsPinned ? "📍 Unpin" : "📌 Pin (Ctrl+P)" };
        pinMi.Click += async (_, _) => await _vm.TogglePinAsync(item);
        cm.Items.Add(pinMi);

        var labelMi = new WpfMenuItem { Header = "🏷️ Rename (Set Label)..." };
        labelMi.Click += async (_, _) =>
        {
            var dlg = new LabelDialog(item.Label, owner: this);
            if (dlg.ShowDialog() == true)
            {
                await _vm.SetItemLabelAsync(item, dlg.ResultLabel);
            }
        };
        cm.Items.Add(labelMi);

        var groupSub = new WpfMenuItem { Header = "📁 Move to Group" };
        var noneGrp = new WpfMenuItem { Header = "None (Ungroup)" };
        noneGrp.Click += async (_, _) => await _vm.SetItemGroupAsync(item, null);
        groupSub.Items.Add(noneGrp);
        groupSub.Items.Add(new WpfSeparator());

        foreach (var grp in _vm.Groups)
        {
            var g = grp;
            var gItem = new WpfMenuItem { Header = g.Name };
            gItem.Icon = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = TryParseColorBrush(g.Color)
            };
            gItem.Click += async (_, _) => await _vm.SetItemGroupAsync(item, g.Id);
            groupSub.Items.Add(gItem);
        }
        cm.Items.Add(groupSub);

        if (!string.IsNullOrEmpty(item.Model.ContentText))
        {
            cm.Items.Add(new WpfSeparator());
            var editMi = new WpfMenuItem { Header = "✏️ Edit (Ctrl+E)..." };
            editMi.Click += async (_, _) =>
            {
                var dlg = new EditItemDialog(item.Model.ContentText, owner: this);
                if (dlg.ShowDialog() == true && dlg.UserAction != EditItemAction.Cancel)
                {
                    await _vm.EditTextItemAsync(item, dlg.EditedText, dlg.UserAction == EditItemAction.SaveAsNew);
                }
            };
            cm.Items.Add(editMi);
        }

        cm.Items.Add(new WpfSeparator());
        var delMi = new WpfMenuItem { Header = "🗑️ Delete (Del)" };
        delMi.Click += async (_, _) =>
        {
            if (item.IsPinned)
            {
                var res = WpfMessageBox.Show("Delete this pinned item?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;
            }
            await _vm.DeleteItemAsync(item);
        };
        cm.Items.Add(delMi);

        return cm;
    }

    private void AddPopupPasteAsSubmenu(WpfMenuItem menu, PopupItemViewModel item)
    {
        void AddTransformItem(string header, PasteAsTransform transform)
        {
            var mi = new WpfMenuItem { Header = header };
            mi.Click += async (_, _) =>
            {
                ClosePopup();
                await _vm.PasteAsAsync(item, transform);
            };
            menu.Items.Add(mi);
        }

        AddTransformItem("Plain text", PasteAsTransform.PlainText);
        AddTransformItem("UPPERCASE", PasteAsTransform.UpperCase);
        AddTransformItem("lowercase", PasteAsTransform.LowerCase);
        AddTransformItem("Title Case", PasteAsTransform.TitleCase);
        AddTransformItem("Single line", PasteAsTransform.SingleLine);
        AddTransformItem("Trimmed", PasteAsTransform.Trimmed);
        AddTransformItem("Clean link", PasteAsTransform.CleanLink);
    }

    private void ShowPasteAsMenuForSelectedItem()
    {
        if (_vm.SelectedItem == null) return;
        var cm = new WpfContextMenu();
        AddPopupPasteAsSubmenu(new WpfMenuItem { Header = "Paste as" }, _vm.SelectedItem);

        // Populate items directly in menu
        void AddDirectItem(string header, PasteAsTransform transform)
        {
            var mi = new WpfMenuItem { Header = header };
            mi.Click += async (_, _) =>
            {
                ClosePopup();
                await _vm.PasteAsAsync(_vm.SelectedItem, transform);
            };
            cm.Items.Add(mi);
        }

        AddDirectItem("1. Plain text", PasteAsTransform.PlainText);
        AddDirectItem("2. UPPERCASE", PasteAsTransform.UpperCase);
        AddDirectItem("3. lowercase", PasteAsTransform.LowerCase);
        AddDirectItem("4. Title Case", PasteAsTransform.TitleCase);
        AddDirectItem("5. Single line", PasteAsTransform.SingleLine);
        AddDirectItem("6. Trimmed", PasteAsTransform.Trimmed);
        AddDirectItem("7. Clean link", PasteAsTransform.CleanLink);

        cm.PlacementTarget = _listBox;
        cm.IsOpen = true;
    }

    private void UpdatePreviewContent(PopupItemViewModel? item)
    {
        if (item == null)
        {
            _previewContent.Content = new TextBlock
            {
                Text = "No item selected",
                Foreground = new MediaBrush(MediaColor.FromRgb(100, 116, 139)),
                FontSize = 13,
                HorizontalAlignment = WpfHorizontalAlignment.Center,
                VerticalAlignment = WpfVerticalAlignment.Center
            };
            return;
        }

        var stack = new Grid();
        stack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        stack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
        stack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

        // Header
        var title = new TextBlock
        {
            Text = $"{item.TypeIcon} {item.Type} • {item.SourceApp}",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 0, 0, 4)
        };
        Grid.SetRow(title, 0);
        stack.Children.Add(title);

        if (!string.IsNullOrEmpty(item.Label))
        {
            var label = new TextBlock
            {
                Text = $"🏷️ {item.Label}",
                FontSize = 12,
                Foreground = new MediaBrush(MediaColor.FromRgb(245, 158, 11)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(label, 0);
        }

        // Content
        FrameworkElement contentEl;
        if (item.Type == ClipboardItemType.Image)
        {
            var imgPath = !string.IsNullOrEmpty(item.ImagePath) && File.Exists(item.ImagePath)
                ? item.ImagePath
                : item.ThumbPath;

            if (imgPath != null)
            {
                contentEl = new WpfImage
                {
                    Source = new BitmapImage(new Uri(Path.GetFullPath(imgPath))),
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = WpfHorizontalAlignment.Center,
                    VerticalAlignment = WpfVerticalAlignment.Center
                };
            }
            else
            {
                contentEl = new TextBlock { Text = "Image not found", Foreground = MediaBrushes.Red };
            }
        }
        else
        {
            contentEl = new WpfTextBox
            {
                Text = item.DisplayFullText,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
                Foreground = MediaBrushes.White,
                BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
                Padding = new Thickness(8),
                FontSize = 12
            };
        }
        Grid.SetRow(contentEl, 1);
        stack.Children.Add(contentEl);

        // Footer
        var metaText = new TextBlock
        {
            Text = $"Size: {item.ByteSize:N0} bytes • Used: {item.UseCount}x\nCopied: {item.LastUsedAt.ToLocalTime():yyyy-MM-dd HH:mm}",
            FontSize = 10,
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 8, 0, 0)
        };
        Grid.SetRow(metaText, 2);
        stack.Children.Add(metaText);

        _previewContent.Content = stack;
    }

    private FrameworkElement BuildFooter()
    {
        var border = new Border
        {
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            CornerRadius = new CornerRadius(0, 0, 12, 12),
            Padding = new Thickness(14, 6, 14, 6)
        };

        var text = new TextBlock
        {
            Text = "↵ Paste | Shift+↵ Plain | Ctrl+↵ Copy | Ctrl+T Paste As | Ctrl+E Edit | Ctrl+P Pin | Del Delete | Esc Close",
            FontSize = 10,
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            HorizontalAlignment = WpfHorizontalAlignment.Center
        };

        border.Child = text;
        return border;
    }

    private async void OnWindowPreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        // 1. Esc to close
        if (e.Key == Key.Escape)
        {
            ClosePopup();
            e.Handled = true;
            return;
        }

        // 2. Tab / Shift+Tab to cycle filters
        if (e.Key == Key.Tab)
        {
            var isShift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            _vm.CycleFilterChip(!isShift);
            e.Handled = true;
            return;
        }

        // 3. Up / Down arrow keys
        if (e.Key == Key.Down)
        {
            if (_listBox.SelectedIndex < _listBox.Items.Count - 1)
            {
                _listBox.SelectedIndex++;
                _listBox.ScrollIntoView(_listBox.SelectedItem);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            if (_listBox.SelectedIndex > 0)
            {
                _listBox.SelectedIndex--;
                _listBox.ScrollIntoView(_listBox.SelectedItem);
            }
            e.Handled = true;
            return;
        }

        // 4. Ctrl+T: Paste as menu
        if (e.Key == Key.T && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (_vm.SelectedItem != null)
            {
                ShowPasteAsMenuForSelectedItem();
            }
            e.Handled = true;
            return;
        }

        // 5. Ctrl+E: Edit selected item
        if (e.Key == Key.E && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (_vm.SelectedItem != null && !string.IsNullOrEmpty(_vm.SelectedItem.Model.ContentText))
            {
                var dlg = new EditItemDialog(_vm.SelectedItem.Model.ContentText, owner: this);
                if (dlg.ShowDialog() == true && dlg.UserAction != EditItemAction.Cancel)
                {
                    await _vm.EditTextItemAsync(_vm.SelectedItem, dlg.EditedText, dlg.UserAction == EditItemAction.SaveAsNew);
                }
            }
            e.Handled = true;
            return;
        }

        // 6. Alt+1 to Alt+9 to paste visible item index 0..8
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            int quickIndex = e.Key switch
            {
                Key.D1 or Key.NumPad1 => 0,
                Key.D2 or Key.NumPad2 => 1,
                Key.D3 or Key.NumPad3 => 2,
                Key.D4 or Key.NumPad4 => 3,
                Key.D5 or Key.NumPad5 => 4,
                Key.D6 or Key.NumPad6 => 5,
                Key.D7 or Key.NumPad7 => 6,
                Key.D8 or Key.NumPad8 => 7,
                Key.D9 or Key.NumPad9 => 8,
                _ => -1
            };

            if (quickIndex >= 0)
            {
                ClosePopup();
                await _vm.PasteNthItemAsync(quickIndex, PasteMode.DirectPaste);
                e.Handled = true;
                return;
            }
        }

        // 7. Enter / Shift+Enter / Ctrl+Enter
        if (e.Key == Key.Enter)
        {
            var mode = PasteMode.DirectPaste;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                mode = PasteMode.PasteAsPlainText;
            }
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                mode = PasteMode.CopyOnly;
            }

            ClosePopup();
            await _vm.PasteSelectedItemAsync(mode);
            e.Handled = true;
            return;
        }

        // 8. Ctrl+P to pin/unpin
        if (e.Key == Key.P && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (_vm.SelectedItem != null)
            {
                await _vm.TogglePinAsync(_vm.SelectedItem);
            }
            e.Handled = true;
            return;
        }

        // 9. Delete to delete item
        if (e.Key == Key.Delete)
        {
            if (_vm.SelectedItem != null)
            {
                if (_vm.SelectedItem.IsPinned)
                {
                    var res = WpfMessageBox.Show(
                        "Delete this pinned item?",
                        "Confirm Delete",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );
                    if (res != MessageBoxResult.Yes) return;
                }

                await _vm.DeleteItemAsync(_vm.SelectedItem);
            }
            e.Handled = true;
            return;
        }
    }

    private static WpfBrush TryParseColorBrush(string colorHex)
    {
        try
        {
            return new BrushConverter().ConvertFromString(colorHex) as WpfBrush
                   ?? new MediaBrush(MediaColor.FromRgb(13, 148, 136));
        }
        catch
        {
            return new MediaBrush(MediaColor.FromRgb(13, 148, 136));
        }
    }
}
