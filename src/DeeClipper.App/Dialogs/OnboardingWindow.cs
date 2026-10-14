using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using DeeClipper.App.Services;
using DeeClipper.Core.Settings;
using MediaBrush = System.Windows.Media.SolidColorBrush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaFontFamily = System.Windows.Media.FontFamily;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfCursors = System.Windows.Input.Cursors;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfVerticalAlignment = System.Windows.VerticalAlignment;

namespace DeeClipper.App.Dialogs;

public class OnboardingWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private int _currentStep = 1;
    private const int TotalSteps = 3;

    private ContentControl _stepContainer = null!;
    private TextBlock _stepIndicatorText = null!;
    private WpfButton _prevButton = null!;
    private WpfButton _nextButton = null!;
    private WpfCheckBox? _startWithWindowsCheckBox;

    public OnboardingWindow(SettingsManager settingsManager, Window? owner = null)
    {
        _settingsManager = settingsManager;
        Owner = owner;

        Title = "Welcome to DeeClipper";
        Width = 620;
        Height = 490;
        MinHeight = 450;
        MinWidth = 550;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new MediaBrush(MediaColor.FromRgb(15, 23, 42)); // Slate-900
        Foreground = new MediaBrush(MediaColor.FromRgb(248, 250, 252));
        FontFamily = new MediaFontFamily("Segoe UI, Segoe UI Variable, sans-serif");
        ResizeMode = ResizeMode.NoResize;

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.ApplyModernWindowRounding(hwnd);
        };

        BuildUI();
        ShowStep(1);
    }

    private void BuildUI()
    {
        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Step Content
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer Navigation

        // 1. Header
        var headerBorder = new Border
        {
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)), // Slate-800
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(24, 16, 24, 16)
        };
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleBlock = new TextBlock
        {
            Text = "Getting Started with DeeClipper",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = MediaBrushes.White,
            VerticalAlignment = WpfVerticalAlignment.Center
        };
        Grid.SetColumn(titleBlock, 0);
        headerGrid.Children.Add(titleBlock);

        _stepIndicatorText = new TextBlock
        {
            Text = "Step 1 of 3",
            FontSize = 13,
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            VerticalAlignment = WpfVerticalAlignment.Center
        };
        Grid.SetColumn(_stepIndicatorText, 1);
        headerGrid.Children.Add(_stepIndicatorText);

        headerBorder.Child = headerGrid;
        Grid.SetRow(headerBorder, 0);
        rootGrid.Children.Add(headerBorder);

        // 2. Step Container
        _stepContainer = new ContentControl
        {
            Margin = new Thickness(32, 24, 32, 24)
        };
        Grid.SetRow(_stepContainer, 1);
        rootGrid.Children.Add(_stepContainer);

        // 3. Footer Navigation
        var footerBorder = new Border
        {
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 16, 24, 16)
        };

        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _prevButton = new WpfButton
        {
            Content = "Back",
            Background = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(18, 8, 18, 8),
            FontSize = 13,
            Cursor = WpfCursors.Hand,
            Visibility = Visibility.Collapsed
        };
        _prevButton.Click += (_, _) =>
        {
            if (_currentStep > 1)
            {
                ShowStep(_currentStep - 1);
            }
        };
        Grid.SetColumn(_prevButton, 0);
        footerGrid.Children.Add(_prevButton);

        _nextButton = new WpfButton
        {
            Content = "Next",
            Background = new MediaBrush(MediaColor.FromRgb(13, 148, 136)), // Teal-600
            Foreground = MediaBrushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(24, 8, 24, 8),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Cursor = WpfCursors.Hand
        };
        _nextButton.Click += OnNextOrFinishClick;
        Grid.SetColumn(_nextButton, 2);
        footerGrid.Children.Add(_nextButton);

        footerBorder.Child = footerGrid;
        Grid.SetRow(footerBorder, 2);
        rootGrid.Children.Add(footerBorder);

        Content = rootGrid;
    }

    private void ShowStep(int step)
    {
        _currentStep = step;
        _stepIndicatorText.Text = $"Step {_currentStep} of {TotalSteps}";

        _prevButton.Visibility = _currentStep > 1 ? Visibility.Visible : Visibility.Collapsed;
        _nextButton.Content = _currentStep == TotalSteps ? "Get Started" : "Next";

        _stepContainer.Content = _currentStep switch
        {
            1 => BuildStep1Content(),
            2 => BuildStep2Content(),
            3 => BuildStep3Content(),
            _ => BuildStep1Content()
        };
    }

    private FrameworkElement BuildStep1Content()
    {
        var panel = new StackPanel();

        var badge = new Border
        {
            Background = new MediaBrush(MediaColor.FromArgb(40, 13, 148, 136)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(13, 148, 136)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            HorizontalAlignment = WpfHorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 14)
        };
        badge.Child = new TextBlock
        {
            Text = "📋 Step 1: Intelligent Clipboard Memory",
            Foreground = new MediaBrush(MediaColor.FromRgb(45, 212, 191)),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold
        };
        panel.Children.Add(badge);

        var title = new TextBlock
        {
            Text = "Never lose anything you copy again",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 0, 0, 12)
        };
        panel.Children.Add(title);

        var desc = new TextBlock
        {
            Text = "DeeClipper runs silently in your system tray and remembers everything you copy across all your applications:",
            FontSize = 13,
            Foreground = new MediaBrush(MediaColor.FromRgb(203, 213, 225)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
            LineHeight = 20
        };
        panel.Children.Add(desc);

        var bullets = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
        AddBullet(bullets, "⚡ Capture Everything", "Plain text, code snippets, rich links, images, screenshots, and copied files.");
        AddBullet(bullets, "📌 Pinned Items Protection", "Pin important clips, rename them with custom labels, and organize into custom colored groups. Pinned items are NEVER deleted.");
        AddBullet(bullets, "🔍 Fast Local Search", "Full-text search indexed with SQLite FTS5 for instantaneous filtering as you type.");

        panel.Children.Add(bullets);
        return panel;
    }

    private FrameworkElement BuildStep2Content()
    {
        var panel = new StackPanel();

        var badge = new Border
        {
            Background = new MediaBrush(MediaColor.FromArgb(40, 59, 130, 246)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(59, 130, 246)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            HorizontalAlignment = WpfHorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 14)
        };
        badge.Child = new TextBlock
        {
            Text = "⌨️ Step 2: Global Hotkey & Quick-Paste",
            Foreground = new MediaBrush(MediaColor.FromRgb(96, 165, 250)),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold
        };
        panel.Children.Add(badge);

        var title = new TextBlock
        {
            Text = "Instant access anywhere with Ctrl+Alt+V",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 0, 0, 12)
        };
        panel.Children.Add(title);

        var desc = new TextBlock
        {
            Text = "Work faster without breaking your typing flow:",
            FontSize = 13,
            Foreground = new MediaBrush(MediaColor.FromRgb(203, 213, 225)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        };
        panel.Children.Add(desc);

        var bullets = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
        AddBullet(bullets, "🎯 Quick Popup Palette (Ctrl+Alt+V)", "Pops up instantly near your mouse cursor. Select with arrows and hit Enter to paste directly into the active window.");
        AddBullet(bullets, "🔢 Quick-Paste Slots 1–9", "Enable Ctrl+Alt+1 through Ctrl+Alt+9 to instantly paste your top pinned clips with a single keystroke.");
        AddBullet(bullets, "✂️ Paste As Transforms (Ctrl+T)", "Paste as UPPERCASE, lowercase, Title Case, Single line, Trimmed, or Clean link (strips URL tracking tokens).");

        panel.Children.Add(bullets);
        return panel;
    }

    private FrameworkElement BuildStep3Content()
    {
        var panel = new StackPanel();

        var badge = new Border
        {
            Background = new MediaBrush(MediaColor.FromArgb(40, 168, 85, 247)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(168, 85, 247)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            HorizontalAlignment = WpfHorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 14)
        };
        badge.Child = new TextBlock
        {
            Text = "🔒 Step 3: Zero Network & Privacy Guarantee",
            Foreground = new MediaBrush(MediaColor.FromRgb(192, 132, 252)),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold
        };
        panel.Children.Add(badge);

        var title = new TextBlock
        {
            Text = "Your data stays 100% on your device",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = MediaBrushes.White,
            Margin = new Thickness(0, 0, 0, 12)
        };
        panel.Children.Add(title);

        var bullets = new StackPanel { Margin = new Thickness(6, 0, 0, 16) };
        AddBullet(bullets, "🌐 100% Offline by Design", "DeeClipper has no network code, no servers, and no telemetry. Your clipboard is stored purely on your local machine.");
        AddBullet(bullets, "🛡️ Password Manager Blocklist", "Automatically ignores copies from 1Password, Bitwarden, KeePass, LastPass, Dashlane, Enpass, and NordPass.");
        AddBullet(bullets, "🔑 Sensitive Data Detection", "Automatically identifies credit cards, JWTs, and private tokens, with auto-wipe after 5 minutes.");

        panel.Children.Add(bullets);

        // Startup Option CheckBox Box
        var optBorder = new Border
        {
            Background = new MediaBrush(MediaColor.FromRgb(30, 41, 59)),
            BorderBrush = new MediaBrush(MediaColor.FromRgb(51, 65, 85)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 8, 0, 0)
        };

        _startWithWindowsCheckBox = new WpfCheckBox
        {
            Content = "Start DeeClipper automatically when Windows starts (Recommended)",
            IsChecked = true,
            Foreground = MediaBrushes.White,
            FontSize = 13,
            Cursor = WpfCursors.Hand,
            VerticalContentAlignment = WpfVerticalAlignment.Center
        };
        optBorder.Child = _startWithWindowsCheckBox;
        panel.Children.Add(optBorder);

        return panel;
    }

    private static void AddBullet(StackPanel panel, string heading, string description)
    {
        var block = new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 12),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18
        };
        block.Inlines.Add(new System.Windows.Documents.Run(heading + "\n")
        {
            FontWeight = FontWeights.SemiBold,
            Foreground = MediaBrushes.White
        });
        block.Inlines.Add(new System.Windows.Documents.Run(description)
        {
            Foreground = new MediaBrush(MediaColor.FromRgb(148, 163, 184)),
            FontSize = 12
        });
        panel.Children.Add(block);
    }

    private void OnNextOrFinishClick(object sender, RoutedEventArgs e)
    {
        if (_currentStep < TotalSteps)
        {
            ShowStep(_currentStep + 1);
            return;
        }

        // Finish
        var startWithWindows = _startWithWindowsCheckBox?.IsChecked == true;
        _settingsManager.Update(s =>
        {
            s.FirstRunCompleted = true;
            s.StartWithWindows = startWithWindows;
        });

        AutoStartService.SetAutoStart(startWithWindows);

        DialogResult = true;
        Close();
    }
}
