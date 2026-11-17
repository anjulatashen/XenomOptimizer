using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using XenomOptimizer.ViewModels;

namespace XenomOptimizer;

public partial class MainWindow : Window
{
    private readonly List<RadioButton> _navButtons = new();
    private readonly Dictionary<string, int> _pageIndexMap = new()
    {
        { "Dashboard", 0 },
        { "Windows", 1 },
        { "Registry", 2 },
        { "GameLoop", 3 },
        { "PUBG", 4 },
        { "Network", 5 },
        { "Cleanup", 6 },
        { "Backup", 7 },
        { "System", 8 },
        { "Settings", 9 }
    };
    private int _currentIndex = 0;

    public MainWindow()
    {
        InitializeComponent();
        StateChanged += MainWindow_StateChanged;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Smooth entrance animation - buttery fade
        var fadeIn = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(280),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };
        MainRootGrid.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        _navButtons.Clear();
        _navButtons.AddRange(new[]
        {
            NavDashboard,
            NavWindows,
            NavRegistry,
            NavGameLoop,
            NavPubg,
            NavNetwork,
            NavCleanup,
            NavBackup,
            NavSystem,
            NavSettings
        });

        // Initialize button states: Dashboard is active by default
        for (int i = 0; i < _navButtons.Count; i++)
        {
            if (i == 0)
                SetButtonActiveDirectly(_navButtons[i]);
            else
                ResetButtonToInactive(_navButtons[i]);
        }

        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += Vm_PropertyChanged;
        }

        // Check for updates in the background without blocking the UI
        XenomOptimizer.Services.UpdateService.CheckForUpdatesInBackground(
            "https://raw.githubusercontent.com/anjulatashen/XenomOptimizer/main/update.json"
        );
    }

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPageTitle) && DataContext is MainViewModel vm)
        {
            if (_pageIndexMap.TryGetValue(vm.CurrentPageTitle, out int newIndex))
            {
                if (newIndex != _currentIndex)
                {
                    AnimateNavTransition(_currentIndex, newIndex);
                    _currentIndex = newIndex;
                }
            }
        }
    }

    private void ResetButtonToInactive(RadioButton btn)
    {
        btn.ApplyTemplate();
        if (btn.Template.FindName("WhiteFill", btn) is Border whiteFill)
        {
            whiteFill.BeginAnimation(UIElement.OpacityProperty, null);
            whiteFill.Opacity = 0.0;
        }
        if (btn.Template.FindName("WhiteSlide", btn) is TranslateTransform whiteSlide)
        {
            whiteSlide.BeginAnimation(TranslateTransform.YProperty, null);
            whiteSlide.Y = 0;
        }
        if (btn.Template.FindName("WhiteScale", btn) is ScaleTransform whiteScale)
        {
            whiteScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            whiteScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            whiteScale.ScaleX = 1.0;
            whiteScale.ScaleY = 1.0;
        }
        if (btn.Template.FindName("IconScale", btn) is ScaleTransform iconScale)
        {
            iconScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            iconScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            iconScale.ScaleX = 1.0;
            iconScale.ScaleY = 1.0;
        }
        if (btn.Template.FindName("IconGlyph", btn) is TextBlock iconGlyph)
        {
            iconGlyph.BeginAnimation(TextBlock.ForegroundProperty, null);
            iconGlyph.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    private void SetButtonActiveDirectly(RadioButton btn)
    {
        btn.ApplyTemplate();
        if (btn.Template.FindName("WhiteFill", btn) is Border whiteFill)
        {
            whiteFill.BeginAnimation(UIElement.OpacityProperty, null);
            whiteFill.Opacity = 1.0;
        }
        if (btn.Template.FindName("WhiteSlide", btn) is TranslateTransform whiteSlide)
        {
            whiteSlide.BeginAnimation(TranslateTransform.YProperty, null);
            whiteSlide.Y = 0;
        }
        if (btn.Template.FindName("WhiteScale", btn) is ScaleTransform whiteScale)
        {
            whiteScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            whiteScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            whiteScale.ScaleX = 1.0;
            whiteScale.ScaleY = 1.0;
        }
        if (btn.Template.FindName("IconScale", btn) is ScaleTransform iconScale)
        {
            iconScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            iconScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            iconScale.ScaleX = 1.0;
            iconScale.ScaleY = 1.0;
        }
        if (btn.Template.FindName("IconGlyph", btn) is TextBlock iconGlyph)
        {
            iconGlyph.BeginAnimation(TextBlock.ForegroundProperty, null);
            iconGlyph.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0x11, 0x14));
        }
    }

    private void AnimateNavTransition(int oldIndex, int newIndex)
    {
        if (newIndex < 0 || newIndex >= _navButtons.Count)
            return;

        // Clean up any other buttons that might have been animating or interrupted
        for (int i = 0; i < _navButtons.Count; i++)
        {
            if (i != oldIndex && i != newIndex)
            {
                ResetButtonToInactive(_navButtons[i]);
            }
        }

        if (oldIndex < 0 || oldIndex >= _navButtons.Count || oldIndex == newIndex)
        {
            SetButtonActiveDirectly(_navButtons[newIndex]);
            return;
        }

        RadioButton oldBtn = _navButtons[oldIndex];
        RadioButton newBtn = _navButtons[newIndex];

        AnimateExit(oldBtn);
        AnimateEnter(newBtn);
    }

    private void AnimateExit(RadioButton btn)
    {
        btn.ApplyTemplate();
        if (btn.Template.FindName("WhiteFill", btn) is not Border whiteFill ||
            btn.Template.FindName("WhiteScale", btn) is not ScaleTransform whiteScale ||
            btn.Template.FindName("IconGlyph", btn) is not TextBlock iconGlyph)
        {
            return;
        }

        ScaleTransform? iconScale = btn.Template.FindName("IconScale", btn) as ScaleTransform;

        // 1. Pop out: Scale down smoothly from center (1.0 -> 0.4)
        var scaleAnim = new DoubleAnimation
        {
            To = 0.4,
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        whiteScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim, HandoffBehavior.SnapshotAndReplace);
        whiteScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim, HandoffBehavior.SnapshotAndReplace);

        if (iconScale != null)
        {
            var iconExitAnim = new DoubleAnimation
            {
                To = 0.4,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            iconScale.BeginAnimation(ScaleTransform.ScaleXProperty, iconExitAnim, HandoffBehavior.SnapshotAndReplace);
            iconScale.BeginAnimation(ScaleTransform.ScaleYProperty, iconExitAnim, HandoffBehavior.SnapshotAndReplace);
        }

        // 2. Fade Opacity: 1.0 -> 0.0
        var opacityAnim = new DoubleAnimation
        {
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        opacityAnim.Completed += (s, e) =>
        {
            ResetButtonToInactive(btn);
        };
        whiteFill.BeginAnimation(UIElement.OpacityProperty, opacityAnim, HandoffBehavior.SnapshotAndReplace);

        // 3. Glyph Color -> Silver (#7D8390)
        var colorAnim = new ColorAnimation
        {
            To = (Color)ColorConverter.ConvertFromString("#7D8390"),
            Duration = TimeSpan.FromMilliseconds(140)
        };
        var glyphBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#101114"));
        iconGlyph.Foreground = glyphBrush;
        glyphBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim, HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateEnter(RadioButton btn)
    {
        btn.ApplyTemplate();
        if (btn.Template.FindName("WhiteFill", btn) is not Border whiteFill ||
            btn.Template.FindName("WhiteSlide", btn) is not TranslateTransform whiteSlide ||
            btn.Template.FindName("WhiteScale", btn) is not ScaleTransform whiteScale ||
            btn.Template.FindName("IconGlyph", btn) is not TextBlock iconGlyph)
        {
            return;
        }

        ScaleTransform? iconScale = btn.Template.FindName("IconScale", btn) as ScaleTransform;

        // Ensure baseline is clean and centered
        whiteSlide.BeginAnimation(TranslateTransform.YProperty, null);
        whiteSlide.Y = 0;
        whiteScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        whiteScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        whiteFill.BeginAnimation(UIElement.OpacityProperty, null);

        if (iconScale != null)
        {
            iconScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            iconScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            iconScale.ScaleX = 0.35;
            iconScale.ScaleY = 0.35;
        }

        whiteScale.ScaleX = 0.35;
        whiteScale.ScaleY = 0.35;
        whiteFill.Opacity = 0.0;

        // 1. Pop in White Circle: Spring bloom scale (0.35 -> 1.0 with punchy elastic overshoot)
        var popAnim = new DoubleAnimation
        {
            From = 0.35,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(270),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 }
        };
        whiteScale.BeginAnimation(ScaleTransform.ScaleXProperty, popAnim, HandoffBehavior.SnapshotAndReplace);
        whiteScale.BeginAnimation(ScaleTransform.ScaleYProperty, popAnim, HandoffBehavior.SnapshotAndReplace);

        // 2. Pop in Icon Glyph: Scales forward together with the circle (0.35 -> 1.0 with elastic bounce)
        if (iconScale != null)
        {
            var iconPopAnim = new DoubleAnimation
            {
                From = 0.35,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(270),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 }
            };
            iconScale.BeginAnimation(ScaleTransform.ScaleXProperty, iconPopAnim, HandoffBehavior.SnapshotAndReplace);
            iconScale.BeginAnimation(ScaleTransform.ScaleYProperty, iconPopAnim, HandoffBehavior.SnapshotAndReplace);
        }

        // 3. Fade Opacity: 0.0 -> 1.0
        var opacityAnim = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        opacityAnim.Completed += (s, e) =>
        {
            SetButtonActiveDirectly(btn);
        };
        whiteFill.BeginAnimation(UIElement.OpacityProperty, opacityAnim, HandoffBehavior.SnapshotAndReplace);

        // 4. Glyph Color -> Dark Charcoal (#101114)
        var colorAnim = new ColorAnimation
        {
            To = (Color)ColorConverter.ConvertFromString("#101114"),
            Duration = TimeSpan.FromMilliseconds(180)
        };
        var glyphBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7D8390"));
        iconGlyph.Foreground = glyphBrush;
        glyphBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim, HandoffBehavior.SnapshotAndReplace);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // DWMWA_USE_IMMERSIVE_DARK_MODE = 20
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));

            // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2 (Windows 11 Native Rounded Corners)
            int roundCorner = 2;
            DwmSetWindowAttribute(hwnd, 33, ref roundCorner, sizeof(int));
        }
        catch { }
    }

    private DispatcherTimer? _sidebarLockTimer;
    private FrameworkElement? _currentLockedElement;

    private void NavButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && (vm.WindowsVm?.IsScanningDrivers == true || vm.WindowsVm?.IsRescanningDrivers == true || vm.WindowsVm?.IsRescanCompleted == true))
        {
            string? page = null;
            if (sender is Button btn && btn.CommandParameter is string p1) page = p1;
            else if (sender is RadioButton rb && rb.CommandParameter is string p2) page = p2;

            if (!string.IsNullOrEmpty(page) && page != "Windows")
            {
                e.Handled = true;
                PlaySidebarLockAnimation(sender as FrameworkElement);
            }
        }
    }

    private void PlaySidebarLockAnimation(FrameworkElement? element)
    {
        if (element == null) return;

        try
        {
            if (_currentLockedElement != null && _currentLockedElement != element)
            {
                ResetSidebarLockVisuals(_currentLockedElement);
            }
            _currentLockedElement = element;
            _sidebarLockTimer?.Stop();

            element.ApplyTemplate();

            if (element is RadioButton rb)
            {
                var flyoutPopup = rb.Template.FindName("FlyoutPopup", rb) as Border;
                if (flyoutPopup != null)
                {
                    flyoutPopup.BeginAnimation(UIElement.OpacityProperty, null);
                    flyoutPopup.Visibility = Visibility.Collapsed;
                    flyoutPopup.Opacity = 0.0;
                }

                var lockBox = rb.Template.FindName("LockBox", rb) as Border;
                var iconGlyph = rb.Template.FindName("IconGlyph", rb) as TextBlock;
                var lockScale = rb.Template.FindName("LockScale", rb) as ScaleTransform;
                var lockShake = rb.Template.FindName("LockShake", rb) as TranslateTransform;
                var lockPopup = rb.Template.FindName("LockPopup", rb) as Border;
                var lockPopupSlide = rb.Template.FindName("LockPopupSlide", rb) as TranslateTransform;

                // Snappy Shake KeyFrames for the Lock
                var shakeAnim = new DoubleAnimationUsingKeyFrames
                {
                    KeyFrames =
                    {
                        new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                        new LinearDoubleKeyFrame(-4.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(35))),
                        new LinearDoubleKeyFrame(4.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(75))),
                        new LinearDoubleKeyFrame(-3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(115))),
                        new LinearDoubleKeyFrame(3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(155))),
                        new LinearDoubleKeyFrame(-1.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(195))),
                        new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(235)))
                    }
                };

                // If lock is ALREADY showing on this element, simply re-shake it and extend timer
                if (lockBox != null && lockBox.Visibility == Visibility.Visible && lockBox.Opacity > 0.3)
                {
                    lockShake?.BeginAnimation(TranslateTransform.XProperty, null);
                    lockShake?.BeginAnimation(TranslateTransform.XProperty, shakeAnim);

                    _sidebarLockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(850) };
                    _sidebarLockTimer.Tick += (s, ev) =>
                    {
                        _sidebarLockTimer.Stop();
                        ResetSidebarLockVisuals(element);
                    };
                    _sidebarLockTimer.Start();
                    return;
                }

                // 1. Immediately hide the original icon completely so it NEVER shows underneath!
                if (iconGlyph != null)
                {
                    iconGlyph.BeginAnimation(UIElement.OpacityProperty, null);
                    iconGlyph.Opacity = 0.0;
                    iconGlyph.Visibility = Visibility.Collapsed;
                }

                // 2. Show the pure red lock icon
                if (lockBox != null)
                {
                    lockBox.Visibility = Visibility.Visible;
                    lockBox.BeginAnimation(UIElement.OpacityProperty, null);
                    var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(110));
                    lockBox.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                    if (lockScale != null)
                    {
                        lockScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                        lockScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                        var popAnim = new DoubleAnimation(0.6, 1.0, TimeSpan.FromMilliseconds(130))
                        {
                            EasingFunction = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut }
                        };
                        lockScale.BeginAnimation(ScaleTransform.ScaleXProperty, popAnim);
                        lockScale.BeginAnimation(ScaleTransform.ScaleYProperty, popAnim);
                    }

                    if (lockShake != null)
                    {
                        lockShake.BeginAnimation(TranslateTransform.XProperty, null);
                        lockShake.BeginAnimation(TranslateTransform.XProperty, shakeAnim);
                    }
                }

                // 3. Show lock popup badge beside button
                if (lockPopup != null)
                {
                    lockPopup.Visibility = Visibility.Visible;
                    lockPopup.BeginAnimation(UIElement.OpacityProperty, null);
                    var fadeInPopup = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(120));
                    lockPopup.BeginAnimation(UIElement.OpacityProperty, fadeInPopup);

                    if (lockPopupSlide != null)
                    {
                        lockPopupSlide.BeginAnimation(TranslateTransform.XProperty, null);
                        var slideAnim = new DoubleAnimation(-8, 6, TimeSpan.FromMilliseconds(140))
                        {
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };
                        lockPopupSlide.BeginAnimation(TranslateTransform.XProperty, slideAnim);
                    }
                }
            }
            else if (element is Button btn)
            {
                var logoContainer = btn.Template.FindName("LogoContainer", btn) as Border;
                if (logoContainer != null)
                {
                    logoContainer.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
                }
            }

            _sidebarLockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(950) };
            _sidebarLockTimer.Tick += (s, ev) =>
            {
                _sidebarLockTimer.Stop();
                ResetSidebarLockVisuals(element);
            };
            _sidebarLockTimer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SidebarLock] Error: {ex.Message}");
        }
    }

    private void ResetSidebarLockVisuals(FrameworkElement element)
    {
        try
        {
            if (element is RadioButton rb)
            {
                var lockBox = rb.Template.FindName("LockBox", rb) as Border;
                var iconGlyph = rb.Template.FindName("IconGlyph", rb) as TextBlock;
                var lockPopup = rb.Template.FindName("LockPopup", rb) as Border;
                var flyoutPopup = rb.Template.FindName("FlyoutPopup", rb) as Border;

                // 1. Smoothly fade out lock popup
                if (lockPopup != null && lockPopup.Visibility == Visibility.Visible)
                {
                    var fadeOutPopup = new DoubleAnimation
                    {
                        From = lockPopup.Opacity,
                        To = 0.0,
                        Duration = TimeSpan.FromMilliseconds(160),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    fadeOutPopup.Completed += (s, e) =>
                    {
                        lockPopup.Visibility = Visibility.Collapsed;
                        lockPopup.BeginAnimation(UIElement.OpacityProperty, null);
                        lockPopup.Opacity = 0.0;
                    };
                    lockPopup.BeginAnimation(UIElement.OpacityProperty, fadeOutPopup);
                }

                // 2. Smoothly fade out lock box; icon remains COLLAPSED until lock is 100% gone
                if (lockBox != null && lockBox.Visibility == Visibility.Visible)
                {
                    var fadeOut = new DoubleAnimation
                    {
                        From = lockBox.Opacity,
                        To = 0.0,
                        Duration = TimeSpan.FromMilliseconds(160),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    fadeOut.Completed += (s, e) =>
                    {
                        lockBox.Visibility = Visibility.Collapsed;
                        lockBox.BeginAnimation(UIElement.OpacityProperty, null);
                        lockBox.Opacity = 0.0;

                        // Clean gap (50ms) after lock disappears before fading normal icon back in
                        var gapTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                        gapTimer.Tick += (st, et) =>
                        {
                            gapTimer.Stop();
                            if (iconGlyph != null)
                            {
                                iconGlyph.Visibility = Visibility.Visible;
                                iconGlyph.BeginAnimation(UIElement.OpacityProperty, null);
                                var fadeInIcon = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(160))
                                {
                                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                                };
                                fadeInIcon.Completed += (s2, e2) =>
                                {
                                    iconGlyph.BeginAnimation(UIElement.OpacityProperty, null);
                                    iconGlyph.Opacity = 1.0;
                                };
                                iconGlyph.BeginAnimation(UIElement.OpacityProperty, fadeInIcon);
                            }

                            if (flyoutPopup != null && rb.IsMouseOver)
                            {
                                flyoutPopup.Visibility = Visibility.Visible;
                                flyoutPopup.BeginAnimation(UIElement.OpacityProperty, null);
                                var fadeInFlyout = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(120));
                                flyoutPopup.BeginAnimation(UIElement.OpacityProperty, fadeInFlyout);
                            }
                        };
                        gapTimer.Start();
                    };
                    lockBox.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                }
                else
                {
                    if (iconGlyph != null)
                    {
                        iconGlyph.BeginAnimation(UIElement.OpacityProperty, null);
                        iconGlyph.Visibility = Visibility.Visible;
                        iconGlyph.Opacity = 1.0;
                    }
                }
            }
            else if (element is Button btn)
            {
                var logoContainer = btn.Template.FindName("LogoContainer", btn) as Border;
                if (logoContainer != null)
                {
                    logoContainer.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2D3C"));
                }
            }
        }
        catch { }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Maximize is disabled
            return;
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        // Executes the 100% native Windows OS DWM minimize animation
        SystemCommands.MinimizeWindow(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        SystemCommands.CloseWindow(this);
    }
}
