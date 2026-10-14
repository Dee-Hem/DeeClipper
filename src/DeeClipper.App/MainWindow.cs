using System.IO;
using System.Windows;
using System.Windows.Controls;
using WpfBinding = System.Windows.Data.Binding;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeeClipper.App.Dialogs;
using DeeClipper.App.Services;
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
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfSeparator = System.Windows.Controls.Separator;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfPoint = System.Windows.Point;
using WpfBrush = System.Windows.Media.Brush;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfSelectionMode = System.Windows.Controls.SelectionMode;

namespace DeeClipper.App;

public class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private WpfListBox _pinnedListBox = null!;
    private WpfListBox _unpinnedListBox = null!;
    private ContentControl _detailHost = null!;
    private TextBlock _statusTextBlock = null!;
    private StackPanel _groupChipsPanel = null!;
    private Border _bulkActionBar = null!;
    private TextBlock _bulkCountText = null!;

    // Drag-and-drop state
    private WpfPoint _dragStartPoint;
    private ItemViewModel? _draggedItem;

    public bool IsExplicitExit { get; set; }

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        DataContext = _vm;

        InitializeWindow();
        BuildUI();

        Loaded += async (_, _) => await _vm.RefreshAsync();
        Closing += OnWindowClosing;
        PreviewKeyDown += OnWindowPreviewKeyDown;
    }

    private void InitializeWindow()
    {
        Title = "DeeClipper";
        Width = 1050;
        Height = 680;
        MinHeight = 480;
        MinWidth = 800;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42)); // Slate-900
        Foreground = new MediaBrush(MediaColor.FromRgb(248, 250, 252));
        FontFamily = new MediaFontFamily("Segoe UI, Segoe UI Variable, sans-serif");

        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            NativeMethods.ApplyModernWindowRounding(hwnd);
        };
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!IsExplicitExit)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void BuildUI()
    {
        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Top Bar
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Bulk Actions Bar
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Status Bar

        // 1. TOP HEADER (Search, Filters, Groups, Clear History)
        var topHeader = BuildTopHeader();
        Grid.SetRow(topHeader, 0);
        mainGrid.Children.Add(topHeader);

        // 2. BULK ACTIONS BAR (visible when multiple items are selected)
        _bulkActionBar = BuildBulkActionBar();
        Grid.SetRow(_bulkActionBar, 1);
        mainGrid.Children.Add(_bulkActionBar);

        // 3. MAIN BODY (List on Left, Detail on Right)
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

        Grid.SetRow(bodyGrid, 2);
        mainGrid.Children.Add(bodyGrid);

        // 4. STATUS BAR
        var statusBar = BuildStatusBar();
        Grid.SetRow(statusBar, 3);
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

        _vm.Groups.CollectionChanged += (_, _) => UpdateGroupChips();
    }

    private FrameworkElement BuildTopHeader()
    {
        var border = new Border
        {
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)), // Slate-800
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 10, 16, 10)
        };

        var stack = new StackPanel();

        // Row 1: Search box + Clear history + New Group + Settings
        var row1 = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) }); // 0: Search
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 1: Space
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 2: + Group
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) }); // 3: Gap
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 4: Clear History
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) }); // 5: Gap
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 6: ⚙ Settings

        var searchBox = new WpfTextBox
        {
            Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(71, 85, 105)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 6, 10, 6),
            FontSize = 13,
            VerticalContentAlignment = WpfVerticalAlignment.Center,
            ToolTip = "Search clips by text, URL, file name, or tag"
        };
        searchBox.TextChanged += (_, _) => _vm.SearchText = searchBox.Text;
        Grid.SetColumn(searchBox, 0);
        row1.Children.Add(searchBox);

        var newGroupBtn = new WpfButton
        {
            Content = "+ Group",
            Background = new MediaBrush(MediaColor.FromRgb(13, 148, 136)),
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 6, 12, 6),
            FontSize = 12,
            Cursor = WpfCursors.Hand,
            ToolTip = "Create a new custom organization group"
        };
        newGroupBtn.Click += async (_, _) =>
        {
            var dlg = new GroupDialog(owner: this);
            if (dlg.ShowDialog() == true)
            {
                await _vm.CreateGroupAsync(dlg.GroupName, dlg.GroupColor);
            }
        };
        Grid.SetColumn(newGroupBtn, 2);
        row1.Children.Add(newGroupBtn);

        var clearBtn = new WpfButton
        {
            Content = "Clear History",
            Background = new MediaBrush(MediaColor.FromArgb(50, 239, 68, 68)),
            Foreground = new MediaBrush(MediaColor.FromRgb(248, 113, 113)),
            BorderBrush = new MediaBrush(MediaColor.FromArgb(100, 239, 68, 68)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 12, 6),
            FontSize = 12,
            Cursor = WpfCursors.Hand,
            ToolTip = "Clear unpinned clipboard history (keeps all pinned items)"
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
        Grid.SetColumn(clearBtn, 4);
        row1.Children.Add(clearBtn);

        var settingsBtn = new WpfButton
        {
            Content = "⚙ Settings",
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(71, 85, 105)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 12, 6),
            FontSize = 12,
            Cursor = WpfCursors.Hand,
            ToolTip = "Open DeeClipper Settings (Hotkeys, Startup, Privacy, Data)"
        };
        settingsBtn.Click += (_, _) => App.OpenSettingsWindow();
        Grid.SetColumn(settingsBtn, 6);
        row1.Children.Add(settingsBtn);

        stack.Children.Add(row1);

        // Row 2: Standard Filter Chips + Group Chips
        var chipsScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

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

        // Group chips container
        _groupChipsPanel = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            Margin = new Thickness(12, 0, 0, 0)
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
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(12, 4, 12, 4),
            FontSize = 12,
            Cursor = WpfCursors.Hand,
            BorderThickness = new Thickness(1)
        };

        void UpdateStyle()
        {
            var isSelected = _vm.SelectedFilter == filter && !_vm.SelectedGroupId.HasValue;
            btn.Background = isSelected
                ? new MediaBrush(MediaColor.FromRgb(13, 148, 136)) // Teal-600
                : new MediaBrush(MediaColor.FromRgb(15, 23, 42));
            btn.Foreground = isSelected ? MediaBrushes.White : new MediaBrush(MediaColor.FromRgb(203, 213, 225));
            btn.BorderBrush = isSelected
                ? new MediaBrush(MediaColor.FromRgb(20, 184, 166))
                : new MediaBrush(MediaColor.FromRgb(51, 65, 85));
        }

        UpdateStyle();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.SelectedFilter) or nameof(MainViewModel.SelectedGroupId))
            {
                UpdateStyle();
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
            Height = 18,
            Background = new MediaBrush(MediaColor.FromRgb(71, 85, 105)),
            Margin = new Thickness(4, 0, 12, 0)
        };
        _groupChipsPanel.Children.Add(sep);

        foreach (var group in _vm.Groups)
        {
            var g = group;
            var chipBtn = new WpfButton
            {
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 12,
                Cursor = WpfCursors.Hand,
                BorderThickness = new Thickness(1),
                AllowDrop = true
            };

            // Support drag and drop onto group chip to assign item
            chipBtn.DragOver += (s, e) =>
            {
                if (e.Data.GetDataPresent(typeof(ItemViewModel)))
                {
                    e.Effects = WpfDragDropEffects.Move;
                    e.Handled = true;
                }
            };
            chipBtn.Drop += async (s, e) =>
            {
                if (e.Data.GetData(typeof(ItemViewModel)) is ItemViewModel droppedItem)
                {
                    await _vm.SetItemGroupAsync(droppedItem, g.Id);
                    e.Handled = true;
                }
            };

            var stack = new StackPanel { Orientation = WpfOrientation.Horizontal };
            var dot = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 0, 6, 0),
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
                if (e.PropertyName == nameof(MainViewModel.SelectedGroupId))
                {
                    UpdateChipStyle();
                }
            };

            chipBtn.Click += (_, _) =>
            {
                _vm.SelectedGroupId = _vm.SelectedGroupId == g.Id ? null : g.Id;
            };

            // Right click context menu for group: Edit, Delete
            var cm = new WpfContextMenu();
            var editItem = new WpfMenuItem { Header = $"Edit '{g.Name}'..." };
            editItem.Click += async (_, _) =>
            {
                var dlg = new GroupDialog(g.Name, g.Color, owner: this);
                if (dlg.ShowDialog() == true)
                {
                    await _vm.UpdateGroupAsync(g.Id, dlg.GroupName, dlg.GroupColor);
                }
            };
            cm.Items.Add(editItem);

            var deleteItem = new WpfMenuItem { Header = $"Delete '{g.Name}'" };
            deleteItem.Click += async (_, _) =>
            {
                var res = WpfMessageBox.Show(
                    $"Delete group '{g.Name}'?\n\nItems in this group will NOT be deleted; they will become ungrouped.",
                    "Confirm Group Deletion",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );
                if (res == MessageBoxResult.Yes)
                {
                    await _vm.DeleteGroupAsync(g.Id);
                }
            };
            cm.Items.Add(deleteItem);

            chipBtn.ContextMenu = cm;
            _groupChipsPanel.Children.Add(chipBtn);
        }
    }

    private Border BuildBulkActionBar()
    {
        var border = new Border
        {
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(71, 85, 105)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 6, 16, 6),
            Visibility = Visibility.Collapsed
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _bulkCountText = new TextBlock
        {
            Text = "0 items selected",
            Foreground = new MediaBrush(MediaColor.FromRgb(245, 158, 11)),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            VerticalAlignment = WpfVerticalAlignment.Center
        };
        Grid.SetColumn(_bulkCountText, 0);
        grid.Children.Add(_bulkCountText);

        var actions = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right
        };

        var bulkPinBtn = new WpfButton
        {
            Content = "📌 Pin",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 11,
            Cursor = WpfCursors.Hand
        };
        bulkPinBtn.Click += async (_, _) =>
        {
            var sel = GetSelectedItems();
            await _vm.BulkSetPinAsync(sel, true);
        };
        actions.Children.Add(bulkPinBtn);

        var bulkUnpinBtn = new WpfButton
        {
            Content = "📍 Unpin",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 11,
            Cursor = WpfCursors.Hand
        };
        bulkUnpinBtn.Click += async (_, _) =>
        {
            var sel = GetSelectedItems();
            await _vm.BulkSetPinAsync(sel, false);
        };
        actions.Children.Add(bulkUnpinBtn);

        var bulkGroupBtn = new WpfButton
        {
            Content = "📁 Move to Group...",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 11,
            Cursor = WpfCursors.Hand
        };
        bulkGroupBtn.Click += (_, _) => ShowBulkGroupMenu(bulkGroupBtn);
        actions.Children.Add(bulkGroupBtn);

        var bulkMergeBtn = new WpfButton
        {
            Content = "📑 Merge Selected",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 11,
            Cursor = WpfCursors.Hand
        };
        bulkMergeBtn.Click += async (_, _) =>
        {
            var sel = GetSelectedItems();
            var textItems = sel.Where(x => !string.IsNullOrEmpty(x.ContentText)).ToList();
            if (textItems.Count < 2)
            {
                WpfMessageBox.Show("Please select at least 2 text items to merge.", "Merge Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dlg = new MergeDialog(textItems.Select(x => x.ContentText!).ToList(), owner: this);
            if (dlg.ShowDialog() == true)
            {
                await _vm.MergeSelectedAsync(textItems, dlg.ResultText);
            }
        };
        actions.Children.Add(bulkMergeBtn);

        var bulkDelBtn = new WpfButton
        {
            Content = "🗑️ Delete",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 11,
            Background = new MediaBrush(MediaColor.FromArgb(50, 239, 68, 68)),
            Foreground = new MediaBrush(MediaColor.FromRgb(248, 113, 113)),
            Cursor = WpfCursors.Hand
        };
        bulkDelBtn.Click += async (_, _) =>
        {
            var sel = GetSelectedItems();
            var res = WpfMessageBox.Show(
                $"Delete {sel.Count} selected items?",
                "Confirm Bulk Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );
            if (res == MessageBoxResult.Yes)
            {
                await _vm.BulkDeleteAsync(sel);
            }
        };
        actions.Children.Add(bulkDelBtn);

        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);

        border.Child = grid;
        return border;
    }

    private List<ItemViewModel> GetSelectedItems()
    {
        var list = new List<ItemViewModel>();
        foreach (var item in _pinnedListBox.SelectedItems)
        {
            if (item is ItemViewModel vm && !list.Contains(vm)) list.Add(vm);
        }
        foreach (var item in _unpinnedListBox.SelectedItems)
        {
            if (item is ItemViewModel vm && !list.Contains(vm)) list.Add(vm);
        }
        return list;
    }

    private void UpdateBulkActionVisibility()
    {
        var count = GetSelectedItems().Count;
        if (count > 1)
        {
            _bulkCountText.Text = $"{count} items selected";
            _bulkActionBar.Visibility = Visibility.Visible;
        }
        else
        {
            _bulkActionBar.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowBulkGroupMenu(FrameworkElement target)
    {
        var cm = new WpfContextMenu();
        var noneItem = new WpfMenuItem { Header = "None (Ungroup)" };
        noneItem.Click += async (_, _) =>
        {
            var sel = GetSelectedItems();
            await _vm.BulkSetGroupAsync(sel, null);
        };
        cm.Items.Add(noneItem);
        cm.Items.Add(new WpfSeparator());

        foreach (var group in _vm.Groups)
        {
            var g = group;
            var mi = new WpfMenuItem { Header = g.Name };
            mi.Icon = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = TryParseColorBrush(g.Color)
            };
            mi.Click += async (_, _) =>
            {
                var sel = GetSelectedItems();
                await _vm.BulkSetGroupAsync(sel, g.Id);
            };
            cm.Items.Add(mi);
        }

        cm.Items.Add(new WpfSeparator());
        var newGrp = new WpfMenuItem { Header = "+ New Group..." };
        newGrp.Click += async (_, _) =>
        {
            var dlg = new GroupDialog(owner: this);
            if (dlg.ShowDialog() == true)
            {
                await _vm.CreateGroupAsync(dlg.GroupName, dlg.GroupColor);
                var created = _vm.Groups.FirstOrDefault(x => x.Name == dlg.GroupName);
                if (created != null)
                {
                    var sel = GetSelectedItems();
                    await _vm.BulkSetGroupAsync(sel, created.Id);
                }
            }
        };
        cm.Items.Add(newGrp);

        cm.PlacementTarget = target;
        cm.IsOpen = true;
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
            Text = "PINNED ITEMS (Drag to Reorder)",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = new MediaBrush(MediaColor.FromRgb(245, 158, 11)), // Amber-500
            Margin = new Thickness(4, 4, 4, 8)
        };
        stack.Children.Add(pinnedHeader);

        // Pinned empty state
        var pinnedEmptyState = new Border
        {
            Background = new MediaBrush(MediaColor.FromArgb(20, 245, 158, 11)),
            BorderBrush = new MediaBrush(MediaColor.FromArgb(60, 245, 158, 11)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(4, 0, 4, 8)
        };
        pinnedEmptyState.Child = new TextBlock
        {
            Text = "📌 No pinned items yet. Click 📌 on any clip to pin it forever.",
            FontSize = 11,
            Foreground = new MediaBrush(MediaColor.FromRgb(251, 191, 36))
        };
        pinnedEmptyState.Visibility = _vm.PinnedItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _vm.PinnedItems.CollectionChanged += (_, _) =>
        {
            pinnedEmptyState.Visibility = _vm.PinnedItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        };
        stack.Children.Add(pinnedEmptyState);

        // Pinned ListBox
        _pinnedListBox = CreateVirtualizedListBox(_vm.PinnedItems, isPinnedList: true);
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

        // History empty state
        var historyEmptyState = new Border
        {
            Background = new MediaBrush(MediaColor.FromArgb(20, 51, 65, 85)),
            BorderBrush = new MediaBrush(MediaColor.FromArgb(60, 71, 85, 105)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(4, 0, 4, 8)
        };
        historyEmptyState.Child = new TextBlock
        {
            Text = "📋 Clipboard history is empty. Copy any text, image, or link to get started.",
            FontSize = 11,
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184))
        };
        historyEmptyState.Visibility = _vm.UnpinnedItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _vm.UnpinnedItems.CollectionChanged += (_, _) =>
        {
            historyEmptyState.Visibility = _vm.UnpinnedItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        };
        stack.Children.Add(historyEmptyState);

        // Unpinned ListBox
        _unpinnedListBox = CreateVirtualizedListBox(_vm.UnpinnedItems, isPinnedList: false);
        stack.Children.Add(_unpinnedListBox);

        scrollViewer.Content = stack;
        return scrollViewer;
    }

    private WpfListBox CreateVirtualizedListBox(System.Collections.IEnumerable itemsSource, bool isPinnedList)
    {
        var listBox = new WpfListBox
        {
            ItemsSource = itemsSource,
            Background = MediaBrushes.Transparent,
            BorderThickness = new Thickness(0),
            SelectionMode = WpfSelectionMode.Extended, // Multi-select support
            AllowDrop = isPinnedList
        };

        ScrollViewer.SetHorizontalScrollBarVisibility(listBox, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(listBox, ScrollBarVisibility.Disabled);
        ScrollViewer.SetCanContentScroll(listBox, true);
        VirtualizingPanel.SetIsVirtualizing(listBox, true);
        VirtualizingPanel.SetVirtualizationMode(listBox, VirtualizationMode.Recycling);

        listBox.SelectionChanged += (sender, _) =>
        {
            UpdateBulkActionVisibility();
            if (sender is WpfListBox lb && lb.SelectedItem is ItemViewModel vm)
            {
                _vm.SelectedItem = vm;
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

        // Drag & drop support
        listBox.PreviewMouseLeftButtonDown += (s, e) =>
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedItem = (e.OriginalSource as FrameworkElement)?.DataContext as ItemViewModel;
        };

        listBox.MouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                var currentPos = e.GetPosition(null);
                var diff = _dragStartPoint - currentPos;
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    DragDrop.DoDragDrop(listBox, _draggedItem, WpfDragDropEffects.Move);
                    _draggedItem = null;
                }
            }
        };

        if (isPinnedList)
        {
            listBox.DragOver += (s, e) =>
            {
                if (e.Data.GetDataPresent(typeof(ItemViewModel)))
                {
                    e.Effects = WpfDragDropEffects.Move;
                    e.Handled = true;
                }
            };

            listBox.Drop += async (s, e) =>
            {
                if (e.Data.GetData(typeof(ItemViewModel)) is ItemViewModel droppedItem && droppedItem.IsPinned)
                {
                    // Find target index in pinned list
                    var targetElement = e.OriginalSource as FrameworkElement;
                    var targetItem = targetElement?.DataContext as ItemViewModel;

                    var oldIndex = _vm.PinnedItems.IndexOf(droppedItem);
                    var newIndex = targetItem != null ? _vm.PinnedItems.IndexOf(targetItem) : _vm.PinnedItems.Count - 1;

                    if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
                    {
                        _vm.PinnedItems.Move(oldIndex, newIndex);
                        var orderedIds = _vm.PinnedItems.Select(x => x.Id).ToList();
                        await _vm.ReorderPinsAsync(orderedIds);
                    }
                    e.Handled = true;
                }
            };
        }

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

        // Slot Badge if pinned and slot is 1..9
        if (isPinnedList)
        {
            var slotBadgeText = new FrameworkElementFactory(typeof(TextBlock));
            slotBadgeText.SetBinding(TextBlock.TextProperty, new WpfBinding(nameof(ItemViewModel.SlotBadge)));
            slotBadgeText.SetValue(TextBlock.FontSizeProperty, 10.0);
            slotBadgeText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            slotBadgeText.SetValue(TextBlock.ForegroundProperty, new MediaBrush(MediaColor.FromRgb(245, 158, 11)));
            slotBadgeText.SetValue(TextBlock.MarginProperty, new Thickness(10, 0, 0, 0));
            metaPanel.AppendChild(slotBadgeText);
        }

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

        // Context Menu on row
        listBox.ContextMenuOpening += (s, e) =>
        {
            if (listBox.SelectedItem is ItemViewModel vm)
            {
                listBox.ContextMenu = BuildItemContextMenu(vm);
            }
            else
            {
                e.Handled = true;
            }
        };

        return listBox;
    }

    private WpfContextMenu BuildItemContextMenu(ItemViewModel item)
    {
        var cm = new WpfContextMenu();

        // 1. Copy
        var copyMi = new WpfMenuItem { Header = "📋 Copy" };
        copyMi.Click += (_, _) => _vm.CopyItem(item);
        cm.Items.Add(copyMi);

        // 2. Paste As Submenu
        var pasteAsSub = new WpfMenuItem { Header = "Paste as" };
        AddPasteAsSubmenu(pasteAsSub, item);
        cm.Items.Add(pasteAsSub);

        cm.Items.Add(new WpfSeparator());

        // 3. Pin / Unpin
        var pinMi = new WpfMenuItem { Header = item.IsPinned ? "📍 Unpin" : "📌 Pin" };
        pinMi.Click += async (_, _) => await _vm.TogglePinAsync(item);
        cm.Items.Add(pinMi);

        // 4. Rename / Set Label (especially for pinned items)
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

        // 5. Move to Group Submenu
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

        groupSub.Items.Add(new WpfSeparator());
        var newGrp = new WpfMenuItem { Header = "+ New Group..." };
        newGrp.Click += async (_, _) =>
        {
            var dlg = new GroupDialog(owner: this);
            if (dlg.ShowDialog() == true)
            {
                await _vm.CreateGroupAsync(dlg.GroupName, dlg.GroupColor);
                var created = _vm.Groups.FirstOrDefault(x => x.Name == dlg.GroupName);
                if (created != null)
                {
                    await _vm.SetItemGroupAsync(item, created.Id);
                }
            }
        };
        groupSub.Items.Add(newGrp);
        cm.Items.Add(groupSub);

        cm.Items.Add(new WpfSeparator());

        // 6. Edit item (Ctrl+E)
        if (!string.IsNullOrEmpty(item.ContentText))
        {
            var editMi = new WpfMenuItem { Header = "✏️ Edit Content (Ctrl+E)..." };
            editMi.Click += async (_, _) =>
            {
                var dlg = new EditItemDialog(item.ContentText, owner: this);
                if (dlg.ShowDialog() == true && dlg.UserAction != EditItemAction.Cancel)
                {
                    await _vm.EditTextItemAsync(item, dlg.EditedText, dlg.UserAction == EditItemAction.SaveAsNew);
                }
            };
            cm.Items.Add(editMi);
        }

        // 7. Merge Selected (if multiple text items selected)
        var selected = GetSelectedItems().Where(x => !string.IsNullOrEmpty(x.ContentText)).ToList();
        if (selected.Count >= 2)
        {
            var mergeMi = new WpfMenuItem { Header = $"📑 Merge {selected.Count} Selected..." };
            mergeMi.Click += async (_, _) =>
            {
                var dlg = new MergeDialog(selected.Select(x => x.ContentText!).ToList(), owner: this);
                if (dlg.ShowDialog() == true)
                {
                    await _vm.MergeSelectedAsync(selected, dlg.ResultText);
                }
            };
            cm.Items.Add(mergeMi);
        }

        cm.Items.Add(new WpfSeparator());

        // 8. Delete
        var delMi = new WpfMenuItem { Header = "🗑️ Delete" };
        delMi.Click += async (_, _) => await _vm.DeleteItemAsync(item);
        cm.Items.Add(delMi);

        return cm;
    }

    private void AddPasteAsSubmenu(WpfMenuItem menu, ItemViewModel item)
    {
        void AddTransformItem(string header, PasteAsTransform transform)
        {
            var mi = new WpfMenuItem { Header = header };
            mi.Click += async (_, _) => await _vm.PasteAsAsync(item, transform);
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

        if (!string.IsNullOrEmpty(item.Label))
        {
            var labelText = new TextBlock
            {
                Text = $"🏷️ {item.Label}",
                FontSize = 13,
                Foreground = new MediaBrush(MediaColor.FromRgb(245, 158, 11)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            metaStack.Children.Add(labelText);
        }

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

        // Pin/Unpin
        var pinBtn = new WpfButton
        {
            Content = item.IsPinned ? "📍 Unpin" : "📌 Pin",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(8, 4, 8, 4),
            FontSize = 11,
            Cursor = WpfCursors.Hand
        };
        pinBtn.Click += async (_, _) =>
        {
            await _vm.TogglePinAsync(item);
            UpdateDetailView(item);
        };
        actionPanel.Children.Add(pinBtn);

        // Rename / Label
        var renameBtn = new WpfButton
        {
            Content = "🏷️ Label",
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(8, 4, 8, 4),
            FontSize = 11,
            Cursor = WpfCursors.Hand
        };
        renameBtn.Click += async (_, _) =>
        {
            var dlg = new LabelDialog(item.Label, owner: this);
            if (dlg.ShowDialog() == true)
            {
                await _vm.SetItemLabelAsync(item, dlg.ResultLabel);
                UpdateDetailView(item);
            }
        };
        actionPanel.Children.Add(renameBtn);

        // Edit
        if (!string.IsNullOrEmpty(item.ContentText))
        {
            var editBtn = new WpfButton
            {
                Content = "✏️ Edit",
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11,
                Cursor = WpfCursors.Hand
            };
            editBtn.Click += async (_, _) =>
            {
                var dlg = new EditItemDialog(item.ContentText, owner: this);
                if (dlg.ShowDialog() == true && dlg.UserAction != EditItemAction.Cancel)
                {
                    await _vm.EditTextItemAsync(item, dlg.EditedText, dlg.UserAction == EditItemAction.SaveAsNew);
                }
            };
            actionPanel.Children.Add(editBtn);
        }

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

        var groupName = item.GroupId.HasValue
            ? (_vm.Groups.FirstOrDefault(x => x.Id == item.GroupId)?.Name ?? "Assigned")
            : "None";

        var footerText = new TextBlock
        {
            Text = $"Size: {item.ByteSize:N0} bytes • Used: {item.UseCount} time(s) • Pinned: {(item.IsPinned ? "Yes" : "No")} • Group: {groupName}",
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

    private async void OnWindowPreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        // Ctrl+E: Edit selected item
        if (e.Key == Key.E && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (_vm.SelectedItem != null && !string.IsNullOrEmpty(_vm.SelectedItem.ContentText))
            {
                var dlg = new EditItemDialog(_vm.SelectedItem.ContentText, owner: this);
                if (dlg.ShowDialog() == true && dlg.UserAction != EditItemAction.Cancel)
                {
                    await _vm.EditTextItemAsync(_vm.SelectedItem, dlg.EditedText, dlg.UserAction == EditItemAction.SaveAsNew);
                }
                e.Handled = true;
            }
            return;
        }

        // Delete: Delete selected items
        if (e.Key == Key.Delete)
        {
            var selected = GetSelectedItems();
            if (selected.Count > 1)
            {
                var res = WpfMessageBox.Show(
                    $"Delete {selected.Count} selected items?",
                    "Confirm Bulk Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );
                if (res == MessageBoxResult.Yes)
                {
                    await _vm.BulkDeleteAsync(selected);
                }
                e.Handled = true;
            }
            else if (_vm.SelectedItem != null)
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
                e.Handled = true;
            }
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
