using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using XenomOptimizer.Services;

namespace XenomOptimizer.Views;

public partial class GameLoopAppDrawerControl : UserControl
{
    private readonly HashSet<string> _hiddenApps = new(StringComparer.OrdinalIgnoreCase);
    private bool _isF9AllAppsVisible = false;
    private Window? _parentWindow;

    private DispatcherTimer? _statusTimer;
    private bool _isDeleteMode = false;
    private bool _isBlinking = false;
    private string _pendingUninstallPackage = "";
    private string _pendingUninstallAppName = "";
    private string _currentEdition = "Detecting...";

    private Button? _activeMenuButton;
    private ContextMenu? _activeContextMenu;
    private bool _isMenuPinned = false;
    private readonly DispatcherTimer _closeDebounceTimer;

    public GameLoopAppDrawerControl()
    {
        InitializeComponent();
        IsVisibleChanged += GameLoopAppDrawerControl_IsVisibleChanged;
        Loaded += GameLoopAppDrawerControl_Loaded;
        Unloaded += GameLoopAppDrawerControl_Unloaded;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _statusTimer.Tick += async (s, e) =>
        {
            await UpdateGameLoopStatusAsync();
            if (_selectedNavTabIndex == 1)
            {
                await UpdateTelemetryStatsAsync();
            }
        };

        _closeDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _closeDebounceTimer.Tick += CloseDebounceTimer_Tick;
    }

    private async void GameLoopAppDrawerControl_Loaded(object sender, RoutedEventArgs e)
    {
        _parentWindow = Window.GetWindow(this);
        if (_parentWindow != null)
        {
            _parentWindow.PreviewKeyDown -= Window_PreviewKeyDown;
            _parentWindow.PreviewKeyDown += Window_PreviewKeyDown;
        }
        await UpdateGameLoopStatusAsync();
        if (!_initialTabSet)
        {
            SelectNavTab(0);
        }
        else
        {
            SelectNavTab(_selectedNavTabIndex);
        }
    }

    private void GameLoopAppDrawerControl_Unloaded(object sender, RoutedEventArgs e)
    {
        _statusTimer?.Stop();
        _closeDebounceTimer.Stop();
        if (_activeContextMenu != null && _activeContextMenu.IsOpen)
        {
            _activeContextMenu.IsOpen = false;
        }
        _isMenuPinned = false;

        if (_parentWindow != null)
        {
            _parentWindow.PreviewKeyDown -= Window_PreviewKeyDown;
            _parentWindow = null;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Visibility == Visibility.Visible && e.Key == Key.F9)
        {
            ToggleF9HiddenApps();
            e.Handled = true;
            return;
        }

        if (Visibility == Visibility.Visible && (e.Key == Key.OemQuestion || e.Key == Key.Divide) && SearchBox != null && !SearchBox.IsFocused)
        {
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    public void ToggleF9HiddenApps()
    {
        _isF9AllAppsVisible = !_isF9AllAppsVisible;
        ApplyAppVisibility();

        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.AppDrawerStatusMessage = _isF9AllAppsVisible
                ? "F9 Mode: All hidden apps revealed (Press F9 to hide)"
                : "F9 Mode: Hidden apps obscured";
        }
    }

    private async void GameLoopAppDrawerControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            ApplyAppVisibility();
            await UpdateGameLoopStatusAsync();
        if (!_initialTabSet)
        {
            SelectNavTab(0);
        }
        else
        {
            SelectNavTab(_selectedNavTabIndex);
        }
            _statusTimer?.Start();

            var args = Environment.GetCommandLineArgs();
            if (args.Any(a => a.Contains("--screenshot")))
            {
                DrawerTranslate.Y = 0;
                CenterAppTitle.Opacity = 1.0;
                return;
            }

            var slideDownAnim = new DoubleAnimation
            {
                From = -740,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            DrawerTranslate.BeginAnimation(TranslateTransform.YProperty, slideDownAnim);

            CenterAppTitle.Opacity = 0.0;
            var fadeInAnim = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(450),
                BeginTime = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            CenterAppTitle.BeginAnimation(UIElement.OpacityProperty, fadeInAnim);
        }
        else
        {
            _statusTimer?.Stop();
            _closeDebounceTimer.Stop();
            if (_activeContextMenu != null && _activeContextMenu.IsOpen)
            {
                _activeContextMenu.IsOpen = false;
            }
            _isMenuPinned = false;
            CenterAppTitle.BeginAnimation(UIElement.OpacityProperty, null);
            CenterAppTitle.Opacity = 0.0;
        }
    }

    public async Task UpdateGameLoopStatusAsync()
    {
        var (edition, isRunning) = await DetectGameLoopStatusAsync();
        _currentEdition = edition;

        StatusText.Text = edition;

        if (isRunning)
        {
            if (_isBlinking)
            {
                _isBlinking = false;
                StatusDot.BeginAnimation(UIElement.OpacityProperty, null);
            }
            StatusDot.Opacity = 1.0;
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)); // Green dot
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)); // Green text
            StatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1E, 0x22)); // Keep original container background
            StatusBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x2B, 0x33)); // Keep original container border
        }
        else
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)); // Orange dot
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)); // Orange text
            StatusBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1E, 0x22)); // Keep original container background
            StatusBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x2B, 0x33)); // Keep original container border

            if (!_isBlinking)
            {
                _isBlinking = true;
                var blinkAnim = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.2,
                    Duration = TimeSpan.FromMilliseconds(750),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                StatusDot.BeginAnimation(UIElement.OpacityProperty, blinkAnim);
            }
        }
    }

    private async Task<(string edition, bool isRunning)> DetectGameLoopStatusAsync()
    {
        return await Task.Run(() =>
        {
            // Interactive GameLoop processes (exclude background service QMEmulatorService)
            string[] procNames = { "AndroidEmulator", "AndroidEmulatorEn", "AndroidEmulatorEx", "aow_exe", "AppMarket" };
            bool isRunning = procNames.Any(p => Process.GetProcessesByName(p).Length > 0);

            bool isChinese = false;
            try
            {
                using var lmKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\WOW6432Node\Tencent\MobileGamePC")
                               ?? Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\Tencent\MobileGamePC");
                if (lmKey?.GetValue("TSyzsVersion") != null) isChinese = true;

                using var cuKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Tencent\MobileGamePC");
                var region = cuKey?.GetValue("Region")?.ToString();
                if (string.Equals(region, "CN", StringComparison.OrdinalIgnoreCase)) isChinese = true;
            }
            catch { }

            var installPaths = new[]
            {
                @"C:\Program Files\TxGameAssistant",
                @"D:\Program Files\TxGameAssistant",
                @"E:\Program Files\TxGameAssistant",
                @"C:\Program Files (x86)\TxGameAssistant"
            };
            string? validPath = installPaths.FirstOrDefault(Directory.Exists);
            bool is64 = false;
            if (validPath != null)
            {
                is64 = Directory.Exists(Path.Combine(validPath, "AOW_Rootfs_100")) ||
                       Directory.Exists(Path.Combine(validPath, "vshell")) ||
                       File.Exists(Path.Combine(validPath, "AOW_Rootfs_100", "init"));
            }

            string edition;
            if (validPath == null && !isChinese)
                edition = "GameLoop Not Installed";
            else if (isChinese)
                edition = is64 ? "Chinese GameLoop 64-bit" : "Chinese GameLoop 32-bit";
            else
                edition = is64 ? "GameLoop Global 64-bit" : "GameLoop Global 32-bit";

            return (edition, isRunning);
        });
    }

    private void SearchBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (SearchPlaceholder != null)
            SearchPlaceholder.Visibility = Visibility.Collapsed;
        if (SearchBorder != null)
            SearchBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (SearchBorder != null)
            SearchBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x26, 0x26, 0x2B));
        if (SearchPlaceholder != null)
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox?.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchPlaceholder != null)
        {
            SearchPlaceholder.Visibility = (SearchBox.IsFocused || !string.IsNullOrEmpty(SearchBox.Text))
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
        ApplyAppFilter();
    }

    private void ApplyAppFilter()
    {
        string query = SearchBox.Text.Trim().ToLowerInvariant();
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;

        bool showPlayStore = string.IsNullOrEmpty(query) || "play store".Contains(query) || "google".Contains(query);
        bool showSettings = string.IsNullOrEmpty(query) || "settings".Contains(query) || "system".Contains(query);
        bool showPubg = string.IsNullOrEmpty(query) || "pubg mobile".Contains(query) || "pubg".Contains(query);
        bool showInstallApk = string.IsNullOrEmpty(query) || "install apk".Contains(query) || "apk".Contains(query) || "add".Contains(query);

        UpdateTileFilteredVisibility(PlayStoreTile, PlayStoreHiddenBadge, "PlayStore", MenuItemHidePlayStore, showPlayStore);
        UpdateTileFilteredVisibility(SettingsTile, SettingsHiddenBadge, "Settings", MenuItemHideSettings, showSettings);
        UpdateTileFilteredVisibility(PubgTile, PubgHiddenBadge, "PUBGMOBILE", MenuItemHidePubg, showPubg);
        if (InstallApkTile != null) InstallApkTile.Visibility = showInstallApk ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateTileFilteredVisibility(UIElement? tile, UIElement? badge, string appKey, MenuItem? menuItem, bool matchesSearch)
    {
        if (tile == null) return;
        bool isHidden = _hiddenApps.Contains(appKey);

        if (menuItem != null) menuItem.Header = isHidden ? "Unhide App" : "Hide App";
        if (badge != null) badge.Visibility = (isHidden && _isF9AllAppsVisible) ? Visibility.Visible : Visibility.Collapsed;

        if (!matchesSearch)
        {
            tile.Visibility = Visibility.Collapsed;
            return;
        }

        if (isHidden)
        {
            tile.Visibility = _isF9AllAppsVisible ? Visibility.Visible : Visibility.Collapsed;
            tile.Opacity = _isF9AllAppsVisible ? 0.55 : 1.0;
        }
        else
        {
            tile.Visibility = Visibility.Visible;
            tile.Opacity = 1.0;
        }
    }

    private void ApplyAppVisibility()
    {
        ApplyAppFilter();
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshBtn.Template.FindName("RefreshRotate", RefreshBtn) is RotateTransform rotate)
        {
            var rotateAnim = new DoubleAnimation
            {
                From = 0,
                To = 720,
                Duration = TimeSpan.FromMilliseconds(1100),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            rotate.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
        }

        // 1. Hide real app titles/icons and show skeleton shimmer cards
        RealAppsScrollViewer.Visibility = Visibility.Collapsed;
        SkeletonAppsScrollViewer.Visibility = Visibility.Visible;

        var shimmerAnim = new DoubleAnimation
        {
            From = -1.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(850),
            RepeatBehavior = RepeatBehavior.Forever
        };
        ShimmerTrans1.BeginAnimation(TranslateTransform.XProperty, shimmerAnim);
        ShimmerTransPill1.BeginAnimation(TranslateTransform.XProperty, shimmerAnim);
        ShimmerTrans2.BeginAnimation(TranslateTransform.XProperty, shimmerAnim);
        ShimmerTransPill2.BeginAnimation(TranslateTransform.XProperty, shimmerAnim);
        ShimmerTrans3.BeginAnimation(TranslateTransform.XProperty, shimmerAnim);
        ShimmerTransPill3.BeginAnimation(TranslateTransform.XProperty, shimmerAnim);
        ShimmerTrans4.BeginAnimation(TranslateTransform.XProperty, shimmerAnim);
        ShimmerTransPill4.BeginAnimation(TranslateTransform.XProperty, shimmerAnim);

        // 2. Refresh GameLoop status
        await UpdateGameLoopStatusAsync();
        await Task.Delay(1100);

        // 3. Stop shimmer animations and hide skeleton
        ShimmerTrans1.BeginAnimation(TranslateTransform.XProperty, null);
        ShimmerTransPill1.BeginAnimation(TranslateTransform.XProperty, null);
        ShimmerTrans2.BeginAnimation(TranslateTransform.XProperty, null);
        ShimmerTransPill2.BeginAnimation(TranslateTransform.XProperty, null);
        ShimmerTrans3.BeginAnimation(TranslateTransform.XProperty, null);
        ShimmerTransPill3.BeginAnimation(TranslateTransform.XProperty, null);
        ShimmerTrans4.BeginAnimation(TranslateTransform.XProperty, null);
        ShimmerTransPill4.BeginAnimation(TranslateTransform.XProperty, null);

        SkeletonAppsScrollViewer.Visibility = Visibility.Collapsed;

        // 4. Smoothly fade in real apps
        RealAppsScrollViewer.Opacity = 0.0;
        RealAppsScrollViewer.Visibility = Visibility.Visible;
        var fadeInAnim = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        RealAppsScrollViewer.BeginAnimation(UIElement.OpacityProperty, fadeInAnim);

        if (DataContext is ViewModels.MainViewModel vm)
            vm.AppDrawerStatusMessage = $"GameLoop environment refreshed ({_currentEdition})";
    }

    public void DeleteModeBtn_Click(object sender, RoutedEventArgs e)
    {
        _isDeleteMode = !_isDeleteMode;
        PlayStoreDeleteBadge.Visibility = _isDeleteMode ? Visibility.Visible : Visibility.Collapsed;
        PubgDeleteBadge.Visibility = _isDeleteMode ? Visibility.Visible : Visibility.Collapsed;

        if (_isDeleteMode)
        {
            DeleteModeBtn.Background = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            DeleteModeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            if (DataContext is ViewModels.MainViewModel vm)
                vm.AppDrawerStatusMessage = "Uninstall Mode Active: Click an app to uninstall it";
        }
        else
        {
            DeleteModeBtn.Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1E, 0x22));
            DeleteModeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x2B, 0x33));
            if (DataContext is ViewModels.MainViewModel vm)
                vm.AppDrawerStatusMessage = "Ready";
        }
    }

    private void PlayStore_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isDeleteMode)
        {
            ShowUninstallModal("Google Play Store", "com.android.vending");
            return;
        }
        (DataContext as ViewModels.MainViewModel)?.LaunchPlayStoreCommand.Execute(null);
    }

    private void Settings_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isDeleteMode)
        {
            MessageBox.Show("Settings is an essential GameLoop system component and cannot be uninstalled.", "System Application", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        (DataContext as ViewModels.MainViewModel)?.LaunchSettingsCommand.Execute(null);
    }

    private async void Pubg_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isDeleteMode)
        {
            string actualPkg = await ResolvePackageAsync("PUBG");
            ShowUninstallModal("PUBG MOBILE", actualPkg);
            return;
        }
        (DataContext as ViewModels.MainViewModel)?.LaunchPubgCommand.Execute(null);
    }

    private void InstallApk_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isDeleteMode)
        {
            return;
        }
        (DataContext as ViewModels.MainViewModel)?.InstallApkFileCommand.Execute(null);
    }

    public void ShowUninstallModal(string appName, string packageName)
    {
        _pendingUninstallAppName = appName;
        _pendingUninstallPackage = packageName;
        ModalAppNameText.Text = $"Uninstall {appName}";
        ModalDescriptionText.Text = $"Are you sure you want to uninstall {appName} ({packageName}) from GameLoop? All associated game files, user data, and cache will be permanently removed.";
        UninstallModalOverlay.Visibility = Visibility.Visible;
    }

    private void ModalCancel_Click(object sender, RoutedEventArgs e)
    {
        UninstallModalOverlay.Visibility = Visibility.Collapsed;
        _pendingUninstallPackage = "";
    }

    private async void ModalConfirmUninstall_Click(object sender, RoutedEventArgs e)
    {
        UninstallModalOverlay.Visibility = Visibility.Collapsed;
        if (!string.IsNullOrEmpty(_pendingUninstallPackage))
        {
            string pkg = _pendingUninstallPackage;
            _pendingUninstallPackage = "";
            await RunAdbCommandAsync($"shell pm uninstall {pkg}");
            if (DataContext is ViewModels.MainViewModel vm)
                vm.AppDrawerStatusMessage = $"Uninstalled: {pkg}";

            _isDeleteMode = false;
            PlayStoreDeleteBadge.Visibility = Visibility.Collapsed;
            PubgDeleteBadge.Visibility = Visibility.Collapsed;
            DeleteModeBtn.Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1E, 0x22));
            DeleteModeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x28, 0x2B, 0x33));
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            Window.GetWindow(this)?.DragMove();
            e.Handled = true;
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        if (window != null)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var window = Window.GetWindow(this);
        window?.Close();
    }

    private void Backdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.CloseAppDrawerCommand.Execute(null);
        }
    }

    private void DrawerPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void PullHandle_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.CloseAppDrawerCommand.Execute(null);
        }
    }

    private void PullHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.CloseAppDrawerCommand.Execute(null);
        }
        e.Handled = true;
    }


    [System.Runtime.InteropServices.DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    private bool _isGamingModeOn = true;
    private int _selectedNavTabIndex = 0;
    private bool _initialTabSet = false;
    private int _currentGamingModeIndex = 1; // 0=Normal, 1=Gaming, 2=Ultra

    private void NavTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string s && int.TryParse(s, out int idx))
        {
            SelectNavTab(idx);
        }
        e.Handled = true;
    }

    public void SelectNavTab(int tabIndex)
    {
        _initialTabSet = true;
        _selectedNavTabIndex = tabIndex;
        if (NavTabPointer != null)
            NavTabPointer.Background = tabIndex == 0 ? new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B)) : Brushes.Transparent;
        if (NavTabPalette != null)
            NavTabPalette.Background = tabIndex == 1 ? new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B)) : Brushes.Transparent;
        if (NavTabPhone != null)
            NavTabPhone.Background = tabIndex == 2 ? new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B)) : Brushes.Transparent;

        if (GamingModeLeftPanel != null)
        {
            GamingModeLeftPanel.Visibility = tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (tabIndex == 1)
        {
            _ = UpdateTelemetryStatsAsync();
        }

        if (DrawerPanel != null)
        {
            double targetHeight = tabIndex == 1 ? 570 : 740;
            var args = Environment.GetCommandLineArgs();
            if (args.Any(a => a.Contains("--screenshot")))
            {
                DrawerPanel.BeginAnimation(FrameworkElement.HeightProperty, null);
                DrawerPanel.Height = targetHeight;
            }
            else
            {
                var anim = new DoubleAnimation
                {
                    To = targetHeight,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                };
                DrawerPanel.BeginAnimation(FrameworkElement.HeightProperty, anim);
            }
        }
    }

    private void NavTab_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border b && b.Tag is string s && int.TryParse(s, out int idx))
        {
            if (idx != _selectedNavTabIndex)
                b.Background = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x1F));
        }
    }

    private void NavTab_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Border b && b.Tag is string s && int.TryParse(s, out int idx))
        {
            if (idx != _selectedNavTabIndex)
                b.Background = Brushes.Transparent;
        }
    }

    private void ModeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int mode = (int)Math.Round(e.NewValue);
        SetGamingMode(mode);
    }

    private void ModeLabel_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string s && int.TryParse(s, out int mode))
        {
            if (ModeSlider != null)
            {
                ModeSlider.Value = mode;
            }
            SetGamingMode(mode);
        }
        e.Handled = true;
    }

    public void SetGamingMode(int mode)
    {
        _currentGamingModeIndex = mode;

        // 1. Update Mode Labels Highlighting
        if (ModeLabelNormal != null)
        {
            ModeLabelNormal.FontWeight = mode == 0 ? FontWeights.Bold : FontWeights.SemiBold;
            ModeLabelNormal.Foreground = mode == 0 ? new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)) : new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
        }
        if (ModeLabelGaming != null)
        {
            ModeLabelGaming.FontWeight = mode == 1 ? FontWeights.Bold : FontWeights.SemiBold;
            ModeLabelGaming.Foreground = mode == 1 ? new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)) : new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
        }
        if (ModeLabelUltra != null)
        {
            ModeLabelUltra.FontWeight = mode == 2 ? FontWeights.Bold : FontWeights.SemiBold;
            ModeLabelUltra.Foreground = mode == 2 ? new SolidColorBrush(Color.FromRgb(0xC0, 0x84, 0xFC)) : new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
        }

        // 2. Update Status Badge (Photo 4 Dynamic Island Style)
        if (ModeBadgeBorder != null && ModeBadgeText != null && ModeHeaderIcon != null)
        {
            switch (mode)
            {
                case 0: // Normal
                    ModeBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x24, 0x30));
                    ModeBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
                    ModeBadgeText.Text = "Standard";
                    ModeBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                    ModeHeaderIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                    break;
                case 1: // Gaming Mode
                    ModeBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x28, 0x22));
                    ModeBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    ModeBadgeText.Text = "Active • Turbo";
                    ModeBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    ModeHeaderIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                    break;
                case 2: // Ultra Gaming
                    ModeBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(0x27, 0x18, 0x38));
                    ModeBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7));
                    ModeBadgeText.Text = "Ultra Peak";
                    ModeBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x84, 0xFC));
                    ModeHeaderIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0x84, 0xFC));
                    break;
            }
        }
    }

    private async Task UpdateTelemetryStatsAsync()
    {
        try
        {
            double cpu = 16.0;
            double ram = 59.0;
            double gpu = 24.0;
            double disk = 42.0;

            if (App.Services != null)
            {
                var detection = App.Services.GetService<ISystemDetectionService>();
                if (detection != null)
                {
                    try
                    {
                        var c = await detection.GetCpuUsagePercentAsync();
                        if (c > 0) cpu = c;
                        var r = await detection.GetRamUsagePercentAsync();
                        if (r > 0) ram = r;
                        var g = await detection.GetGpuUsagePercentAsync();
                        if (g > 0) gpu = g;
                        var d = await detection.GetDiskUsagePercentAsync();
                        if (d > 0) disk = d;
                    }
                    catch { }
                }
            }

            if (TelemetryCpuVal != null) TelemetryCpuVal.Text = $"{Math.Round(cpu)}%";
            if (TelemetryRamVal != null) TelemetryRamVal.Text = $"{Math.Round(ram)}%";
            if (TelemetryGpuVal != null) TelemetryGpuVal.Text = $"{Math.Round(gpu)}%";
            if (TelemetryDiskVal != null) TelemetryDiskVal.Text = $"{Math.Round(disk)}%";
        }
        catch { }
    }

    private void DotsButton_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_isDeleteMode) return;

        if (sender is Button btn && btn.ContextMenu != null)
        {
            _closeDebounceTimer.Stop();

            if (_activeContextMenu != null && _activeContextMenu != btn.ContextMenu && _activeContextMenu.IsOpen)
            {
                _activeContextMenu.IsOpen = false;
                _isMenuPinned = false;
            }

            _activeMenuButton = btn;
            _activeContextMenu = btn.ContextMenu;

            if (!_activeContextMenu.IsOpen)
            {
                _isMenuPinned = false;
                _activeContextMenu.PlacementTarget = btn;
                _activeContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                _activeContextMenu.IsOpen = true;
            }
        }
    }

    private void DotsButton_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isMenuPinned && _activeContextMenu != null && _activeContextMenu.IsOpen)
        {
            _closeDebounceTimer.Stop();
            _closeDebounceTimer.Start();
        }
    }

    private void ContextMenu_MouseEnter(object sender, MouseEventArgs e)
    {
        _closeDebounceTimer.Stop();
    }

    private void ContextMenu_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isMenuPinned && _activeContextMenu != null && _activeContextMenu.IsOpen)
        {
            _closeDebounceTimer.Stop();
            _closeDebounceTimer.Start();
        }
    }

    private void CloseDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _closeDebounceTimer.Stop();

        if (_isMenuPinned || _activeContextMenu == null || !_activeContextMenu.IsOpen)
            return;

        bool isOverButton = _activeMenuButton != null && _activeMenuButton.IsMouseOver;
        bool isOverMenu = _activeContextMenu.IsMouseOver;

        if (!isOverButton && !isOverMenu)
        {
            _activeContextMenu.IsOpen = false;
        }
    }

    public void DotsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            _closeDebounceTimer.Stop();
            _activeMenuButton = btn;
            _activeContextMenu = btn.ContextMenu;

            // Clicking pins / locks the menu open so mouse leave won't close it
            _isMenuPinned = true;

            if (!_activeContextMenu.IsOpen)
            {
                _activeContextMenu.PlacementTarget = btn;
                _activeContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                _activeContextMenu.IsOpen = true;
            }
        }
        e.Handled = true;
    }

    private void ContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        _closeDebounceTimer.Stop();
        _isMenuPinned = false;
        if (ReferenceEquals(_activeContextMenu, sender))
        {
            _activeContextMenu = null;
            _activeMenuButton = null;
        }
    }

    private void LaunchPlayStore_Click(object sender, RoutedEventArgs e)
    {
        (DataContext as ViewModels.MainViewModel)?.LaunchPlayStoreCommand.Execute(null);
    }

    private void LaunchSettings_Click(object sender, RoutedEventArgs e)
    {
        (DataContext as ViewModels.MainViewModel)?.LaunchSettingsCommand.Execute(null);
    }

    private void LaunchPubg_Click(object sender, RoutedEventArgs e)
    {
        (DataContext as ViewModels.MainViewModel)?.LaunchPubgCommand.Execute(null);
    }

    private void HideApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string appKey)
        {
            if (_hiddenApps.Contains(appKey))
            {
                _hiddenApps.Remove(appKey);
                if (DataContext is ViewModels.MainViewModel vm)
                    vm.AppDrawerStatusMessage = $"{appKey} unhidden";
            }
            else
            {
                _hiddenApps.Add(appKey);
                if (DataContext is ViewModels.MainViewModel vm)
                    vm.AppDrawerStatusMessage = $"{appKey} hidden (Press F9 to reveal)";
            }
            ApplyAppVisibility();
        }
    }

    private async void ForceStop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string pkg)
        {
            string actualPkg = await ResolvePackageAsync(pkg);
            await RunAdbCommandAsync($"shell am force-stop {actualPkg}");
            if (DataContext is ViewModels.MainViewModel vm)
                vm.AppDrawerStatusMessage = $"Force stopped: {actualPkg}";
        }
    }

    private async void ClearData_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string pkg)
        {
            string actualPkg = await ResolvePackageAsync(pkg);
            await RunAdbCommandAsync($"shell pm clear {actualPkg}");
            if (DataContext is ViewModels.MainViewModel vm)
                vm.AppDrawerStatusMessage = $"Data cleared for: {actualPkg}";
        }
    }

    private async void AppInfo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string pkg)
        {
            string actualPkg = await ResolvePackageAsync(pkg);
            await RunAdbCommandAsync($"shell am start -a android.settings.APPLICATION_DETAILS_SETTINGS package:{actualPkg}");
            if (DataContext is ViewModels.MainViewModel vm)
                vm.AppDrawerStatusMessage = $"Opening App Info for: {actualPkg}";
        }
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string pkg)
        {
            string actualPkg = await ResolvePackageAsync(pkg);
            string appName = actualPkg == "com.android.vending" ? "Google Play Store" : (pkg.Contains("pubg", StringComparison.OrdinalIgnoreCase) ? "PUBG MOBILE" : actualPkg);
            ShowUninstallModal(appName, actualPkg);
        }
    }

    private async Task<string> ResolvePackageAsync(string pkg)
    {
        if (string.Equals(pkg, "PUBG", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var cuKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Tencent\MobileGamePC");
                string matchApk = cuKey?.OpenSubKey("RunFalgs")?.GetValue("MatchRuleApk")?.ToString() ?? "";
                if (string.IsNullOrEmpty(matchApk))
                    matchApk = cuKey?.OpenSubKey("RunFalgs")?.OpenSubKey("_100")?.GetValue("ApkName")?.ToString() ?? "";

                if (!string.IsNullOrEmpty(matchApk))
                {
                    if (matchApk.Contains("krmobile")) return "com.pubg.krmobile";
                    if (matchApk.Contains("vng")) return "com.vng.pubgmobile";
                    if (matchApk.Contains("imobile")) return "com.pubg.imobile";
                    if (matchApk.Contains("pubgmhd")) return "com.tencent.tmgp.pubgmhd";
                    if (matchApk.Contains("ig")) return "com.tencent.ig";
                }
            }
            catch { }
            await Task.CompletedTask;
            return "com.tencent.ig";
        }
        return pkg;
    }

    private async Task RunAdbCommandAsync(string arguments)
    {
        await Task.Run(() =>
        {
            try
            {
                var candidates = new[]
                {
                    @"C:\Program Files\TxGameAssistant\ui\adb.exe",
                    @"D:\Program Files\TxGameAssistant\ui\adb.exe",
                    @"C:\Program Files (x86)\TxGameAssistant\ui\adb.exe",
                    @"D:\TxGameAssistant\ui\adb.exe"
                };
                string? adbExe = candidates.FirstOrDefault(File.Exists);
                if (adbExe != null)
                {
                    using var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = adbExe,
                        Arguments = arguments,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    p?.WaitForExit(4000);
                }
            }
            catch { }
        });
    }
}
