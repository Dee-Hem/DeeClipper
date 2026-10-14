using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DeeClipper.App.Services;
using DeeClipper.App.ViewModels;
using DeeClipper.Core.Backup;
using DeeClipper.Core.Common;
using DeeClipper.Core.Settings;
using DeeClipper.Core.Storage;
using Microsoft.Win32;
using MediaBrush = System.Windows.Media.SolidColorBrush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaFontFamily = System.Windows.Media.FontFamily;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfCursors = System.Windows.Input.Cursors;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfListBox = System.Windows.Controls.ListBox;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace DeeClipper.App.Dialogs;

public class SettingsWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private readonly AppSettings _settings;
    private readonly DatabaseService _db;
    private readonly DatabasePaths _paths;
    private readonly ClipboardEngine _clipboardEngine;
    private readonly HotkeyManager _hotkeyManager;
    private readonly BackupService _backupService;

    // Navigation buttons
    private readonly List<WpfButton> _navButtons = new();
    private readonly ContentControl _contentArea = new();

    // Section controls
    // General
    private WpfCheckBox _startWithWindowsCheck = null!;

    // Hotkeys
    private WpfTextBox _hotkeyDisplay = null!;
    private TextBlock _hotkeyStatusText = null!;
    private WpfButton _hotkeyRecordBtn = null!;
    private bool _isRecordingHotkey = false;
    private WpfCheckBox _quickSlotsCheck = null!;

    // Popup
    private WpfRadioButton _posCursorRadio = null!;
    private WpfRadioButton _posCenterRadio = null!;
    private WpfTextBox _popupWidthBox = null!;
    private WpfTextBox _popupHeightBox = null!;
    private WpfCheckBox _showPreviewCheck = null!;
    private WpfRadioButton _pasteDirectRadio = null!;
    private WpfRadioButton _copyOnlyRadio = null!;

    // History
    private WpfComboBox _historyLimitCombo = null!;
    private WpfComboBox _maxAgeCombo = null!;
    private WpfComboBox _maxTextSizeCombo = null!;
    private WpfComboBox _maxImageSizeCombo = null!;

    // Privacy
    private WpfCheckBox _pauseMonitoringCheck = null!;
    private WpfCheckBox _sensitiveDetectionCheck = null!;
    private WpfComboBox _sensitiveWipeCombo = null!;
    private WpfListBox _blocklistListBox = null!;
    private WpfTextBox _newBlocklistAppBox = null!;

    // Appearance
    private WpfComboBox _themeCombo = null!;
    private WpfCheckBox _compactModeCheck = null!;
    private WpfComboBox _fontSizeCombo = null!;

    // Data
    private TextBlock _dbSizeText = null!;
    private TextBlock _imagesSizeText = null!;

    public SettingsWindow(
        SettingsManager settingsManager,
        DatabaseService db,
        DatabasePaths paths,
        ClipboardEngine clipboardEngine,
        HotkeyManager hotkeyManager,
        Window? owner = null)
    {
        _settingsManager = settingsManager;
        _settings = settingsManager.Current;
        _db = db;
        _paths = paths;
        _clipboardEngine = clipboardEngine;
        _hotkeyManager = hotkeyManager;
        _backupService = new BackupService(db, paths);
        Owner = owner;

        Title = "DeeClipper Settings";
        Width = 780;
        Height = 560;
        MinWidth = 700;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42)); // Slate-900
        Foreground = new MediaBrush(MediaColor.FromRgb(248, 250, 252));
        FontFamily = new MediaFontFamily("Segoe UI, Segoe UI Variable, sans-serif");

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.ApplyModernWindowRounding(hwnd);
        };

        BuildUI();
        SelectTab(0);
    }

    private void BuildUI()
    {
        var mainGrid = new Grid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) }); // Nav sidebar
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Pixel) }); // Divider
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Content area

        // Left Navigation Bar
        var navBorder = new Border
        {
            Background = new MediaBrush(MediaColor.FromRgb(24, 34, 49)), // Slate-850
            Padding = new Thickness(12, 16, 12, 16)
        };
        var navPanel = new StackPanel();

        var navTitle = new TextBlock
        {
            Text = "Settings",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(8, 0, 0, 16)
        };
        navPanel.Children.Add(navTitle);

        var tabNames = new[]
        {
            "⚙️ General",
            "⌨️ Hotkeys",
            "🪟 Popup",
            "🕒 History",
            "🛡️ Privacy",
            "🎨 Appearance",
            "💾 Data",
            "ℹ️ About"
        };

        for (int i = 0; i < tabNames.Length; i++)
        {
            var tabIndex = i;
            var btn = new WpfButton
            {
                Content = tabNames[i],
                HorizontalContentAlignment = WpfHorizontalAlignment.Left,
                Padding = new Thickness(12, 8, 12, 8),
                FontSize = 13,
                Margin = new Thickness(0, 2, 0, 2),
                Background = MediaBrushes.Transparent,
                Foreground = new MediaBrush(MediaColor.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(0),
                Cursor = WpfCursors.Hand
            };
            btn.Click += (_, _) => SelectTab(tabIndex);
            _navButtons.Add(btn);
            navPanel.Children.Add(btn);
        }

        navBorder.Child = navPanel;
        Grid.SetColumn(navBorder, 0);
        mainGrid.Children.Add(navBorder);

        // Divider
        var divider = new Border { Background = new MediaBrush(MediaColor.FromRgb(51, 65, 85)) };
        Grid.SetColumn(divider, 1);
        mainGrid.Children.Add(divider);

        // Right Content Area
        _contentArea.Margin = new Thickness(24, 20, 24, 20);
        Grid.SetColumn(_contentArea, 2);
        mainGrid.Children.Add(_contentArea);

        Content = mainGrid;
    }

    private void SelectTab(int index)
    {
        for (int i = 0; i < _navButtons.Count; i++)
        {
            var isSelected = i == index;
            _navButtons[i].Background = isSelected
                ? new MediaBrush(MediaColor.FromRgb(13, 148, 136)) // Teal-600
                : MediaBrushes.Transparent;
            _navButtons[i].Foreground = isSelected ? MediaBrushes.White : new MediaBrush(MediaColor.FromRgb(203, 213, 225));
            _navButtons[i].FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
        }

        _contentArea.Content = index switch
        {
            0 => BuildGeneralTab(),
            1 => BuildHotkeysTab(),
            2 => BuildPopupTab(),
            3 => BuildHistoryTab(),
            4 => BuildPrivacyTab(),
            5 => BuildAppearanceTab(),
            6 => BuildDataTab(),
            7 => BuildAboutTab(),
            _ => BuildGeneralTab()
        };
    }

    #region Tab 1: General
    private FrameworkElement BuildGeneralTab()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel();

        AddSectionTitle(panel, "General Settings");

        _startWithWindowsCheck = new WpfCheckBox
        {
            Content = "Start DeeClipper when Windows starts",
            IsChecked = _settings.StartWithWindows,
            Foreground = MediaBrushes.White,
            FontSize = 13,
            Cursor = WpfCursors.Hand,
            Margin = new Thickness(0, 0, 0, 6)
        };
        _startWithWindowsCheck.Checked += (_, _) => OnStartWithWindowsToggled(true);
        _startWithWindowsCheck.Unchecked += (_, _) => OnStartWithWindowsToggled(false);
        panel.Children.Add(_startWithWindowsCheck);

        AddSubText(panel, "Creates an HKCU Run entry that launches DeeClipper quietly with the --background argument into the system tray.");

        var sep = new Border
        {
            Height = 1,
            Background = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            Margin = new Thickness(0, 20, 0, 20)
        };
        panel.Children.Add(sep);

        AddSectionTitle(panel, "First-Run Onboarding");
        AddSubText(panel, "Want to revisit the initial introduction tour, shortcut tips, and privacy rules?");

        var onboardingBtn = new WpfButton
        {
            Content = "Launch Onboarding Tour...",
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(71, 85, 105)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 8, 14, 8),
            FontSize = 12,
            HorizontalAlignment = WpfHorizontalAlignment.Left,
            Cursor = WpfCursors.Hand,
            Margin = new Thickness(0, 10, 0, 0)
        };
        onboardingBtn.Click += (_, _) =>
        {
            var tour = new OnboardingWindow(_settingsManager, this);
            tour.ShowDialog();
            _startWithWindowsCheck.IsChecked = _settings.StartWithWindows;
        };
        panel.Children.Add(onboardingBtn);

        scroll.Content = panel;
        return scroll;
    }

    private void OnStartWithWindowsToggled(bool enable)
    {
        _settings.StartWithWindows = enable;
        AutoStartService.SetAutoStart(enable);
        _settingsManager.Save(_settings);
    }
    #endregion

    #region Tab 2: Hotkeys
    private FrameworkElement BuildHotkeysTab()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel();

        AddSectionTitle(panel, "Global Popup Hotkey");
        AddSubText(panel, "Press this global hotkey from any window to summon the fast clipboard palette near your cursor.");

        var hotkeyRow = new Grid { Margin = new Thickness(0, 10, 0, 8) };
        hotkeyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        hotkeyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        hotkeyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hotkeyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        hotkeyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _hotkeyDisplay = new WpfTextBox
        {
            Text = _settings.PopupHotkey,
            IsReadOnly = true,
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(71, 85, 105)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 6, 10, 6),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold
        };
        _hotkeyDisplay.PreviewKeyDown += OnHotkeyInputKeyDown;
        Grid.SetColumn(_hotkeyDisplay, 0);
        hotkeyRow.Children.Add(_hotkeyDisplay);

        _hotkeyRecordBtn = new WpfButton
        {
            Content = "Record Hotkey",
            Background = new MediaBrush(MediaColor.FromRgb(13, 148, 136)),
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 6, 14, 6),
            FontSize = 12,
            Cursor = WpfCursors.Hand
        };
        _hotkeyRecordBtn.Click += (_, _) =>
        {
            _isRecordingHotkey = true;
            _hotkeyDisplay.Text = "Press keys (e.g. Ctrl+Alt+V)...";
            _hotkeyDisplay.Focus();
            _hotkeyStatusText.Text = "Recording: press your key combination...";
            _hotkeyStatusText.Foreground = new MediaBrush(MediaColor.FromRgb(45, 212, 191));
        };
        Grid.SetColumn(_hotkeyRecordBtn, 2);
        hotkeyRow.Children.Add(_hotkeyRecordBtn);

        var resetBtn = new WpfButton
        {
            Content = "Reset (Ctrl+Alt+V)",
            Background = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 6, 12, 6),
            FontSize = 12,
            Cursor = WpfCursors.Hand
        };
        resetBtn.Click += (_, _) =>
        {
            _isRecordingHotkey = false;
            _hotkeyDisplay.Text = "Ctrl+Alt+V";
            ApplyHotkey("Ctrl+Alt+V");
        };
        Grid.SetColumn(resetBtn, 4);
        hotkeyRow.Children.Add(resetBtn);

        panel.Children.Add(hotkeyRow);

        _hotkeyStatusText = new TextBlock
        {
            Text = _hotkeyManager.IsRegistered ? "✓ Hotkey registered and active." : "⚠️ Hotkey registration failed or in use.",
            FontSize = 12,
            Foreground = _hotkeyManager.IsRegistered
                ? new MediaBrush(MediaColor.FromRgb(34, 197, 94))
                : new MediaBrush(MediaColor.FromRgb(239, 68, 68)),
            Margin = new Thickness(0, 0, 0, 20)
        };
        panel.Children.Add(_hotkeyStatusText);

        var sep = new Border
        {
            Height = 1,
            Background = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            Margin = new Thickness(0, 10, 0, 20)
        };
        panel.Children.Add(sep);

        AddSectionTitle(panel, "Quick-Paste Slots (1–9)");
        _quickSlotsCheck = new WpfCheckBox
        {
            Content = "Enable global Ctrl+Alt+1 to Ctrl+Alt+9 quick-paste slots",
            IsChecked = _settings.QuickPasteSlots,
            Foreground = MediaBrushes.White,
            FontSize = 13,
            Cursor = WpfCursors.Hand,
            Margin = new Thickness(0, 0, 0, 6)
        };
        _quickSlotsCheck.Checked += (_, _) => OnQuickSlotsToggled(true);
        _quickSlotsCheck.Unchecked += (_, _) => OnQuickSlotsToggled(false);
        panel.Children.Add(_quickSlotsCheck);

        AddSubText(panel, "When enabled, pressing Ctrl+Alt+1 through Ctrl+Alt+9 instantly pastes the matching pinned item by pin order. Pinned items show their slot indicator.");

        scroll.Content = panel;
        return scroll;
    }

    private void OnHotkeyInputKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (!_isRecordingHotkey) return;

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Skip lone modifiers
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        if (key == Key.Escape)
        {
            _isRecordingHotkey = false;
            _hotkeyDisplay.Text = _settings.PopupHotkey;
            _hotkeyStatusText.Text = "Recording cancelled.";
            _hotkeyStatusText.Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184));
            return;
        }

        var parts = new List<string>();
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");

        if (parts.Count == 0)
        {
            _hotkeyStatusText.Text = "Please include at least one modifier key (Ctrl, Alt, Shift).";
            _hotkeyStatusText.Foreground = new MediaBrush(MediaColor.FromRgb(239, 68, 68));
            return;
        }

        var keyName = key.ToString();
        if (key is >= Key.D0 and <= Key.D9) keyName = ((char)('0' + (key - Key.D0))).ToString();

        parts.Add(keyName);
        var hotkeyStr = string.Join("+", parts);

        _isRecordingHotkey = false;
        _hotkeyDisplay.Text = hotkeyStr;
        ApplyHotkey(hotkeyStr);
    }

    private void ApplyHotkey(string hotkeyStr)
    {
        if (string.Equals(hotkeyStr, "Win+V", StringComparison.OrdinalIgnoreCase))
        {
            _hotkeyStatusText.Text = "Win+V is reserved by Windows. Please choose another combination.";
            _hotkeyStatusText.Foreground = new MediaBrush(MediaColor.FromRgb(239, 68, 68));
            return;
        }

        var success = _hotkeyManager.Register(hotkeyStr);
        if (success)
        {
            _settings.PopupHotkey = hotkeyStr;
            _settingsManager.Save(_settings);
            _hotkeyStatusText.Text = $"✓ Registered '{hotkeyStr}' successfully.";
            _hotkeyStatusText.Foreground = new MediaBrush(MediaColor.FromRgb(34, 197, 94));
        }
        else
        {
            _hotkeyStatusText.Text = $"⚠️ Hotkey '{hotkeyStr}' is already in use by another application.";
            _hotkeyStatusText.Foreground = new MediaBrush(MediaColor.FromRgb(239, 68, 68));
        }
    }

    private void OnQuickSlotsToggled(bool enable)
    {
        _settings.QuickPasteSlots = enable;
        _settingsManager.Save(_settings);

        if (enable)
        {
            _hotkeyManager.RegisterQuickPasteSlots();
        }
        else
        {
            _hotkeyManager.UnregisterQuickPasteSlots();
        }
    }
    #endregion

    #region Tab 3: Popup
    private FrameworkElement BuildPopupTab()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel();

        AddSectionTitle(panel, "Popup Position");
        _posCursorRadio = new WpfRadioButton
        {
            Content = "Open near mouse cursor (recommended)",
            IsChecked = _settings.PopupPositionNearCursor,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 4, 0, 4)
        };
        _posCursorRadio.Checked += (_, _) =>
        {
            _settings.PopupPositionNearCursor = true;
            _settingsManager.Save(_settings);
        };
        panel.Children.Add(_posCursorRadio);

        _posCenterRadio = new WpfRadioButton
        {
            Content = "Center on active monitor",
            IsChecked = !_settings.PopupPositionNearCursor,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 4, 0, 16)
        };
        _posCenterRadio.Checked += (_, _) =>
        {
            _settings.PopupPositionNearCursor = false;
            _settingsManager.Save(_settings);
        };
        panel.Children.Add(_posCenterRadio);

        AddSectionTitle(panel, "Default Window Size");
        var sizeRow = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 4, 0, 16) };

        sizeRow.Children.Add(new TextBlock { Text = "Width:", VerticalAlignment = WpfVerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _popupWidthBox = new WpfTextBox
        {
            Text = ((int)_settings.PopupWidth).ToString(),
            Width = 60,
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = MediaBrushes.White,
            Padding = new Thickness(6, 4, 6, 4)
        };
        _popupWidthBox.LostFocus += (_, _) =>
        {
            if (double.TryParse(_popupWidthBox.Text, out var w) && w >= 400 && w <= 1600)
            {
                _settings.PopupWidth = w;
                _settingsManager.Save(_settings);
            }
        };
        sizeRow.Children.Add(_popupWidthBox);

        sizeRow.Children.Add(new TextBlock { Text = "Height:", VerticalAlignment = WpfVerticalAlignment.Center, Margin = new Thickness(16, 0, 6, 0) });
        _popupHeightBox = new WpfTextBox
        {
            Text = ((int)_settings.PopupHeight).ToString(),
            Width = 60,
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = MediaBrushes.White,
            Padding = new Thickness(6, 4, 6, 4)
        };
        _popupHeightBox.LostFocus += (_, _) =>
        {
            if (double.TryParse(_popupHeightBox.Text, out var h) && h >= 300 && h <= 1200)
            {
                _settings.PopupHeight = h;
                _settingsManager.Save(_settings);
            }
        };
        sizeRow.Children.Add(_popupHeightBox);

        panel.Children.Add(sizeRow);

        AddSectionTitle(panel, "Preview Pane");
        _showPreviewCheck = new WpfCheckBox
        {
            Content = "Show preview pane on the right side of popup",
            IsChecked = _settings.ShowPreviewPane,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 4, 0, 16)
        };
        _showPreviewCheck.Click += (_, _) =>
        {
            _settings.ShowPreviewPane = _showPreviewCheck.IsChecked == true;
            _settingsManager.Save(_settings);
        };
        panel.Children.Add(_showPreviewCheck);

        AddSectionTitle(panel, "Paste Action on Enter / Click");
        _pasteDirectRadio = new WpfRadioButton
        {
            Content = "Paste directly into active window",
            IsChecked = _settings.PasteDirectly,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 4, 0, 4)
        };
        _pasteDirectRadio.Checked += (_, _) =>
        {
            _settings.PasteDirectly = true;
            _settingsManager.Save(_settings);
        };
        panel.Children.Add(_pasteDirectRadio);

        _copyOnlyRadio = new WpfRadioButton
        {
            Content = "Copy to clipboard only (do not simulate Ctrl+V)",
            IsChecked = !_settings.PasteDirectly,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 4, 0, 16)
        };
        _copyOnlyRadio.Checked += (_, _) =>
        {
            _settings.PasteDirectly = false;
            _settingsManager.Save(_settings);
        };
        panel.Children.Add(_copyOnlyRadio);

        scroll.Content = panel;
        return scroll;
    }
    #endregion

    #region Tab 4: History
    private FrameworkElement BuildHistoryTab()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel();

        AddSectionTitle(panel, "History Limits");

        // Live explanation callout box
        var callout = new Border
        {
            Background = new MediaBrush(MediaColor.FromArgb(40, 13, 148, 136)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(13, 148, 136)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 16)
        };
        callout.Child = new TextBlock
        {
            Text = "📌 Pinned items are NEVER automatically removed, regardless of the history count limit or age limit settings below.",
            Foreground = new MediaBrush(MediaColor.FromRgb(45, 212, 191)),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(callout);

        // Count limit
        panel.Children.Add(new TextBlock { Text = "Maximum unpinned history items:", Foreground = MediaBrushes.White, Margin = new Thickness(0, 4, 0, 4) });
        _historyLimitCombo = new WpfComboBox { Width = 180, HorizontalAlignment = WpfHorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12) };
        AddComboItem(_historyLimitCombo, "50 items", 50);
        AddComboItem(_historyLimitCombo, "100 items", 100);
        AddComboItem(_historyLimitCombo, "500 items (default)", 500);
        AddComboItem(_historyLimitCombo, "1000 items", 1000);
        AddComboItem(_historyLimitCombo, "Unlimited", -1);
        SelectComboByTag(_historyLimitCombo, _settings.HistoryLimit);
        _historyLimitCombo.SelectionChanged += (_, _) =>
        {
            if (_historyLimitCombo.SelectedItem is WpfComboBoxItem item && item.Tag is int limit)
            {
                _settings.HistoryLimit = limit;
                _settingsManager.Save(_settings);
            }
        };
        panel.Children.Add(_historyLimitCombo);

        // Age limit
        panel.Children.Add(new TextBlock { Text = "Maximum unpinned item age:", Foreground = MediaBrushes.White, Margin = new Thickness(0, 4, 0, 4) });
        _maxAgeCombo = new WpfComboBox { Width = 180, HorizontalAlignment = WpfHorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16) };
        AddComboItem(_maxAgeCombo, "Never (keep forever)", (int?)null);
        AddComboItem(_maxAgeCombo, "7 days", 7);
        AddComboItem(_maxAgeCombo, "14 days", 14);
        AddComboItem(_maxAgeCombo, "30 days", 30);
        AddComboItem(_maxAgeCombo, "90 days", 90);
        SelectComboByTag(_maxAgeCombo, _settings.MaxAgeDays);
        _maxAgeCombo.SelectionChanged += (_, _) =>
        {
            if (_maxAgeCombo.SelectedItem is WpfComboBoxItem item)
            {
                _settings.MaxAgeDays = item.Tag as int?;
                _settingsManager.Save(_settings);
            }
        };
        panel.Children.Add(_maxAgeCombo);

        AddSectionTitle(panel, "Capture Size Limits");
        var sizeGrid = new Grid { Margin = new Thickness(0, 4, 0, 16) };
        sizeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        sizeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        sizeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });

        var textPanel = new StackPanel();
        textPanel.Children.Add(new TextBlock { Text = "Max text size to capture:", Foreground = MediaBrushes.White, Margin = new Thickness(0, 0, 0, 4) });
        _maxTextSizeCombo = new WpfComboBox();
        AddComboItem(_maxTextSizeCombo, "512 KB", 512L * 1024);
        AddComboItem(_maxTextSizeCombo, "1 MB (default)", 1024L * 1024);
        AddComboItem(_maxTextSizeCombo, "2 MB", 2L * 1024 * 1024);
        AddComboItem(_maxTextSizeCombo, "5 MB", 5L * 1024 * 1024);
        AddComboItem(_maxTextSizeCombo, "10 MB", 10L * 1024 * 1024);
        SelectComboByTag(_maxTextSizeCombo, _settings.MaxTextSizeBytes);
        _maxTextSizeCombo.SelectionChanged += (_, _) =>
        {
            if (_maxTextSizeCombo.SelectedItem is WpfComboBoxItem item && item.Tag is long bytes)
            {
                _settings.MaxTextSizeBytes = bytes;
                _settingsManager.Save(_settings);
            }
        };
        textPanel.Children.Add(_maxTextSizeCombo);
        Grid.SetColumn(textPanel, 0);
        sizeGrid.Children.Add(textPanel);

        var imgPanel = new StackPanel();
        imgPanel.Children.Add(new TextBlock { Text = "Max image size to capture:", Foreground = MediaBrushes.White, Margin = new Thickness(0, 0, 0, 4) });
        _maxImageSizeCombo = new WpfComboBox();
        AddComboItem(_maxImageSizeCombo, "5 MB", 5L * 1024 * 1024);
        AddComboItem(_maxImageSizeCombo, "10 MB", 10L * 1024 * 1024);
        AddComboItem(_maxImageSizeCombo, "25 MB (default)", 25L * 1024 * 1024);
        AddComboItem(_maxImageSizeCombo, "50 MB", 50L * 1024 * 1024);
        SelectComboByTag(_maxImageSizeCombo, _settings.MaxImageSizeBytes);
        _maxImageSizeCombo.SelectionChanged += (_, _) =>
        {
            if (_maxImageSizeCombo.SelectedItem is WpfComboBoxItem item && item.Tag is long bytes)
            {
                _settings.MaxImageSizeBytes = bytes;
                _settingsManager.Save(_settings);
            }
        };
        imgPanel.Children.Add(_maxImageSizeCombo);
        Grid.SetColumn(imgPanel, 2);
        sizeGrid.Children.Add(imgPanel);

        panel.Children.Add(sizeGrid);

        AddSectionTitle(panel, "History Actions");
        var actionsRow = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 6, 0, 16) };

        var clearBtn = new WpfButton
        {
            Content = "Clear History (keeps pinned)",
            Background = new MediaBrush(MediaColor.FromArgb(40, 239, 68, 68)),
            Foreground = new MediaBrush(MediaColor.FromRgb(248, 113, 113)),
            BorderBrush = new MediaBrush(MediaColor.FromArgb(100, 239, 68, 68)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 6, 14, 6),
            FontSize = 12,
            Cursor = WpfCursors.Hand,
            Margin = new Thickness(0, 0, 12, 0)
        };
        clearBtn.Click += async (_, _) =>
        {
            var res = WpfMessageBox.Show(
                "Are you sure you want to clear unpinned history?\n(All pinned items and groups are preserved).",
                "Clear History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );
            if (res == MessageBoxResult.Yes)
            {
                var count = await _db.ClearHistoryAsync();
                WpfMessageBox.Show($"Cleared {count} unpinned items.", "History Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };
        actionsRow.Children.Add(clearBtn);

        var deleteAllBtn = new WpfButton
        {
            Content = "Delete Everything Including Pinned...",
            Background = new MediaBrush(MediaColor.FromRgb(220, 38, 38)),
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 6, 14, 6),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Cursor = WpfCursors.Hand
        };
        deleteAllBtn.Click += async (_, _) =>
        {
            var dlg = new DeleteAllConfirmDialog(this);
            if (dlg.ShowDialog() == true && dlg.Confirmed)
            {
                var count = await _db.DeleteEverythingAsync("DELETE");
                WpfMessageBox.Show($"Completely erased {count} items from database.", "History Erased", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };
        actionsRow.Children.Add(deleteAllBtn);

        panel.Children.Add(actionsRow);

        scroll.Content = panel;
        return scroll;
    }
    #endregion

    #region Tab 5: Privacy
    private FrameworkElement BuildPrivacyTab()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel();

        AddSectionTitle(panel, "Clipboard Monitoring");
        _pauseMonitoringCheck = new WpfCheckBox
        {
            Content = "Pause clipboard monitoring",
            IsChecked = _settings.IsMonitoringPaused,
            Foreground = MediaBrushes.White,
            FontSize = 13,
            Cursor = WpfCursors.Hand,
            Margin = new Thickness(0, 4, 0, 6)
        };
        _pauseMonitoringCheck.Checked += (_, _) => OnMonitoringPausedToggled(true);
        _pauseMonitoringCheck.Unchecked += (_, _) => OnMonitoringPausedToggled(false);
        panel.Children.Add(_pauseMonitoringCheck);
        AddSubText(panel, "When monitoring is paused, no new clips are captured. The tray icon displays a paused badge.");

        var sep1 = new Border { Height = 1, Background = new MediaBrush(MediaColor.FromRgb(51, 65, 85)), Margin = new Thickness(0, 16, 0, 16) };
        panel.Children.Add(sep1);

        AddSectionTitle(panel, "Sensitive Content Protection");
        _sensitiveDetectionCheck = new WpfCheckBox
        {
            Content = "Enable automatic sensitive detection (passwords, tokens, credit cards)",
            IsChecked = _settings.SensitiveDetectionEnabled,
            Foreground = MediaBrushes.White,
            FontSize = 13,
            Cursor = WpfCursors.Hand,
            Margin = new Thickness(0, 4, 0, 8)
        };
        _sensitiveDetectionCheck.Click += (_, _) =>
        {
            _settings.SensitiveDetectionEnabled = _sensitiveDetectionCheck.IsChecked == true;
            _settingsManager.Save(_settings);
        };
        panel.Children.Add(_sensitiveDetectionCheck);

        var wipePanel = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 4, 0, 16) };
        wipePanel.Children.Add(new TextBlock { Text = "Auto-wipe unpinned sensitive items after:", Foreground = MediaBrushes.White, VerticalAlignment = WpfVerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        _sensitiveWipeCombo = new WpfComboBox { Width = 140 };
        AddComboItem(_sensitiveWipeCombo, "1 minute", 1);
        AddComboItem(_sensitiveWipeCombo, "2 minutes", 2);
        AddComboItem(_sensitiveWipeCombo, "5 minutes (default)", 5);
        AddComboItem(_sensitiveWipeCombo, "15 minutes", 15);
        AddComboItem(_sensitiveWipeCombo, "30 minutes", 30);
        AddComboItem(_sensitiveWipeCombo, "60 minutes", 60);
        SelectComboByTag(_sensitiveWipeCombo, _settings.SensitiveWipeMinutes);
        _sensitiveWipeCombo.SelectionChanged += (_, _) =>
        {
            if (_sensitiveWipeCombo.SelectedItem is WpfComboBoxItem item && item.Tag is int mins)
            {
                _settings.SensitiveWipeMinutes = mins;
                _settingsManager.Save(_settings);
            }
        };
        wipePanel.Children.Add(_sensitiveWipeCombo);
        panel.Children.Add(wipePanel);

        var sep2 = new Border { Height = 1, Background = new MediaBrush(MediaColor.FromRgb(51, 65, 85)), Margin = new Thickness(0, 10, 0, 16) };
        panel.Children.Add(sep2);

        AddSectionTitle(panel, "Application Blocklist");
        AddSubText(panel, "DeeClipper ignores any copied content originating from applications on this list:");

        _blocklistListBox = new WpfListBox
        {
            Height = 120,
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            Margin = new Thickness(0, 8, 0, 8)
        };
        foreach (var app in _settings.AppBlocklist)
        {
            _blocklistListBox.Items.Add(app);
        }
        panel.Children.Add(_blocklistListBox);

        var addAppRow = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        addAppRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addAppRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        addAppRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        addAppRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
        addAppRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _newBlocklistAppBox = new WpfTextBox
        {
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(71, 85, 105)),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 12
        };
        Grid.SetColumn(_newBlocklistAppBox, 0);
        addAppRow.Children.Add(_newBlocklistAppBox);

        var addBtn = new WpfButton
        {
            Content = "Add App",
            Background = new MediaBrush(MediaColor.FromRgb(13, 148, 136)),
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 6, 14, 6),
            FontSize = 12,
            Cursor = WpfCursors.Hand
        };
        addBtn.Click += (_, _) =>
        {
            var text = _newBlocklistAppBox.Text.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    text = text[..^4];

                if (!_settings.AppBlocklist.Contains(text, StringComparer.OrdinalIgnoreCase))
                {
                    _settings.AppBlocklist.Add(text.ToLowerInvariant());
                    _blocklistListBox.Items.Add(text.ToLowerInvariant());
                    _settingsManager.Save(_settings);
                    _newBlocklistAppBox.Text = string.Empty;
                }
            }
        };
        Grid.SetColumn(addBtn, 2);
        addAppRow.Children.Add(addBtn);

        var removeBtn = new WpfButton
        {
            Content = "Remove Selected",
            Background = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 6, 14, 6),
            FontSize = 12,
            Cursor = WpfCursors.Hand
        };
        removeBtn.Click += (_, _) =>
        {
            if (_blocklistListBox.SelectedItem is string selected)
            {
                _settings.AppBlocklist.RemoveAll(a => string.Equals(a, selected, StringComparison.OrdinalIgnoreCase));
                _blocklistListBox.Items.Remove(selected);
                _settingsManager.Save(_settings);
            }
        };
        Grid.SetColumn(removeBtn, 4);
        addAppRow.Children.Add(removeBtn);

        panel.Children.Add(addAppRow);

        scroll.Content = panel;
        return scroll;
    }

    private void OnMonitoringPausedToggled(bool isPaused)
    {
        _settings.IsMonitoringPaused = isPaused;
        _settingsManager.Save(_settings);
        _clipboardEngine.SetPaused(isPaused);
    }
    #endregion

    #region Tab 6: Appearance
    private FrameworkElement BuildAppearanceTab()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel();

        AddSectionTitle(panel, "Application Theme");
        _themeCombo = new WpfComboBox { Width = 180, HorizontalAlignment = WpfHorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 16) };
        AddComboItem(_themeCombo, "System Default", "System");
        AddComboItem(_themeCombo, "Dark Theme", "Dark");
        AddComboItem(_themeCombo, "Light Theme", "Light");
        SelectComboByTag(_themeCombo, _settings.Theme);
        _themeCombo.SelectionChanged += (_, _) =>
        {
            if (_themeCombo.SelectedItem is WpfComboBoxItem item && item.Tag is string th)
            {
                _settings.Theme = th;
                _settingsManager.Save(_settings);
            }
        };
        panel.Children.Add(_themeCombo);

        AddSectionTitle(panel, "Compact Mode");
        _compactModeCheck = new WpfCheckBox
        {
            Content = "Enable compact list view (condensed item margins)",
            IsChecked = _settings.CompactMode,
            Foreground = MediaBrushes.White,
            FontSize = 13,
            Cursor = WpfCursors.Hand,
            Margin = new Thickness(0, 4, 0, 16)
        };
        _compactModeCheck.Click += (_, _) =>
        {
            _settings.CompactMode = _compactModeCheck.IsChecked == true;
            _settingsManager.Save(_settings);
        };
        panel.Children.Add(_compactModeCheck);

        AddSectionTitle(panel, "Interface Font Size");
        _fontSizeCombo = new WpfComboBox { Width = 180, HorizontalAlignment = WpfHorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 16) };
        AddComboItem(_fontSizeCombo, "Small (11 pt)", 11.0);
        AddComboItem(_fontSizeCombo, "Normal (13 pt)", 13.0);
        AddComboItem(_fontSizeCombo, "Large (15 pt)", 15.0);
        SelectComboByTag(_fontSizeCombo, _settings.FontSize);
        _fontSizeCombo.SelectionChanged += (_, _) =>
        {
            if (_fontSizeCombo.SelectedItem is WpfComboBoxItem item && item.Tag is double fs)
            {
                _settings.FontSize = fs;
                _settingsManager.Save(_settings);
            }
        };
        panel.Children.Add(_fontSizeCombo);

        scroll.Content = panel;
        return scroll;
    }
    #endregion

    #region Tab 7: Data
    private FrameworkElement BuildDataTab()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel();

        AddSectionTitle(panel, "Local Data Storage");

        var pathRow = new Grid { Margin = new Thickness(0, 4, 0, 12) };
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var pathText = new TextBlock
        {
            Text = _paths.RootDirectory,
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            FontSize = 12,
            VerticalAlignment = WpfVerticalAlignment.Center
        };
        Grid.SetColumn(pathText, 0);
        pathRow.Children.Add(pathText);

        var openFolderBtn = new WpfButton
        {
            Content = "Open Data Folder",
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(71, 85, 105)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 12, 6),
            FontSize = 12,
            Cursor = WpfCursors.Hand
        };
        openFolderBtn.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _paths.RootDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to open data directory", ex);
            }
        };
        Grid.SetColumn(openFolderBtn, 1);
        pathRow.Children.Add(openFolderBtn);

        panel.Children.Add(pathRow);

        // Size statistics
        long dbSizeBytes = 0;
        if (File.Exists(_paths.DatabasePath))
        {
            try { dbSizeBytes = new FileInfo(_paths.DatabasePath).Length; } catch { }
        }

        long imagesSizeBytes = 0;
        if (Directory.Exists(_paths.ImagesDirectory))
        {
            try
            {
                imagesSizeBytes = Directory.GetFiles(_paths.ImagesDirectory).Sum(f => new FileInfo(f).Length);
            }
            catch { }
        }

        var statsPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 20) };
        _dbSizeText = new TextBlock
        {
            Text = $"Database size: {FormatBytes(dbSizeBytes)}",
            Foreground = new MediaBrush(MediaColor.FromRgb(203, 213, 225)),
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 4)
        };
        statsPanel.Children.Add(_dbSizeText);

        _imagesSizeText = new TextBlock
        {
            Text = $"Images storage size: {FormatBytes(imagesSizeBytes)}",
            Foreground = new MediaBrush(MediaColor.FromRgb(203, 213, 225)),
            FontSize = 13
        };
        statsPanel.Children.Add(_imagesSizeText);
        panel.Children.Add(statsPanel);

        var sep = new Border { Height = 1, Background = new MediaBrush(MediaColor.FromRgb(51, 65, 85)), Margin = new Thickness(0, 10, 0, 20) };
        panel.Children.Add(sep);

        AddSectionTitle(panel, "Backup & Restore");
        AddSubText(panel, "Export your items, groups, pin order, and labels into an offline ZIP archive, or import an existing archive.");

        var backupBtns = new StackPanel { Orientation = WpfOrientation.Horizontal, Margin = new Thickness(0, 10, 0, 16) };

        var exportBtn = new WpfButton
        {
            Content = "Export Backup (ZIP)...",
            Background = new MediaBrush(MediaColor.FromRgb(13, 148, 136)),
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(16, 8, 16, 8),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Cursor = WpfCursors.Hand,
            Margin = new Thickness(0, 0, 12, 0)
        };
        exportBtn.Click += async (_, _) =>
        {
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export DeeClipper Backup",
                Filter = "ZIP Archive (*.zip)|*.zip",
                FileName = $"DeeClipper_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
            };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    await _backupService.ExportAsync(sfd.FileName);
                    WpfMessageBox.Show(
                        $"Backup successfully exported to:\n{sfd.FileName}",
                        "Export Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                catch (Exception ex)
                {
                    Logger.LogError("Export failed", ex);
                    WpfMessageBox.Show($"Export failed:\n{ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        };
        backupBtns.Children.Add(exportBtn);

        var importBtn = new WpfButton
        {
            Content = "Import Backup (ZIP)...",
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            Foreground = MediaBrushes.White,
            BorderBrush = new MediaBrush(MediaColor.FromRgb(71, 85, 105)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 8, 16, 8),
            FontSize = 12,
            Cursor = WpfCursors.Hand
        };
        importBtn.Click += async (_, _) =>
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import DeeClipper Backup",
                Filter = "ZIP Archive (*.zip)|*.zip"
            };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var summary = await _backupService.ImportAsync(ofd.FileName);
                    WpfMessageBox.Show(
                        $"Import completed successfully!\n\nAdded: {summary.AddedCount} new items\nSkipped: {summary.SkippedCount} duplicates (already present)",
                        "Import Summary",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                catch (Exception ex)
                {
                    Logger.LogError("Import failed", ex);
                    WpfMessageBox.Show($"Import failed:\n{ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        };
        backupBtns.Children.Add(importBtn);

        panel.Children.Add(backupBtns);

        scroll.Content = panel;
        return scroll;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F2} KB";
        return $"{(bytes / (1024.0 * 1024.0)):F2} MB";
    }
    #endregion

    #region Tab 8: About
    private FrameworkElement BuildAboutTab()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var panel = new StackPanel();

        AddSectionTitle(panel, "About DeeClipper");

        var ver = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        var appTitle = new TextBlock
        {
            Text = $"DeeClipper v{ver}",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 4, 0, 4)
        };
        panel.Children.Add(appTitle);

        var subTitle = new TextBlock
        {
            Text = "Persistent, offline-first clipboard manager for Windows 10 & 11.",
            FontSize = 13,
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            Margin = new Thickness(0, 0, 0, 20)
        };
        panel.Children.Add(subTitle);

        var netCard = new Border
        {
            Background = new MediaBrush(MediaColor.FromArgb(40, 16, 185, 129)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(16, 185, 129)), // Emerald-500
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 20)
        };

        var netStack = new StackPanel();
        netStack.Children.Add(new TextBlock
        {
            Text = "🔒 Zero Network Connections Guarantee",
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = new MediaBrush(MediaColor.FromRgb(52, 211, 153)),
            Margin = new Thickness(0, 0, 0, 8)
        });
        netStack.Children.Add(new TextBlock
        {
            Text = "DeeClipper is completely 100% offline. The codebase contains no network libraries, makes no HTTP or socket connections, transmits no analytics, and never connects to the internet under any circumstances.\n\nAll your clipboard data, images, search indices, and preferences remain strictly and permanently on your local computer.",
            FontSize = 12,
            Foreground = MediaBrushes.White,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18
        });
        netCard.Child = netStack;
        panel.Children.Add(netCard);

        scroll.Content = panel;
        return scroll;
    }
    #endregion

    #region Helper Methods
    private static void AddSectionTitle(StackPanel panel, string title)
    {
        var text = new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 0, 0, 6)
        };
        panel.Children.Add(text);
    }

    private static void AddSubText(StackPanel panel, string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 12,
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 16,
            Margin = new Thickness(0, 0, 0, 10)
        };
        panel.Children.Add(block);
    }

    private static void AddComboItem(WpfComboBox combo, string text, object? tag)
    {
        combo.Items.Add(new WpfComboBoxItem
        {
            Content = text,
            Tag = tag,
            Foreground = MediaBrushes.Black
        });
    }

    private static void SelectComboByTag(WpfComboBox combo, object? tag)
    {
        foreach (WpfComboBoxItem item in combo.Items)
        {
            if (Equals(item.Tag, tag))
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }
    #endregion
}
