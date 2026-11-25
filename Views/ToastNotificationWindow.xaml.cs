using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace XenomOptimizer.Views;

public partial class ToastNotificationWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    private static ToastNotificationWindow? _activeToast;
    private readonly Action? _onClick;
    private DispatcherTimer? _autoCloseTimer;
    private bool _isClosing;

    public ToastNotificationWindow(string title, string message, Action? onClick = null)
    {
        InitializeComponent();
        _onClick = onClick;

        TxtTitle.Text = string.IsNullOrWhiteSpace(title) ? "LAG Killer by XENOM" : title;
        TxtMessage.Text = message ?? string.Empty;

        LoadLogo();
        Loaded += ToastNotificationWindow_Loaded;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyNoActivateStyle();
    }

    private void ApplyNoActivateStyle()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                long currentStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
                long newStyle = currentStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(newStyle));
            }
        }
        catch { }
    }

    private void LoadLogo()
    {
        try
        {
            // Try pack URI for logo.png
            var uri = new Uri("pack://application:,,,/Assets/logo.png", UriKind.Absolute);
            ImgLogo.Source = new BitmapImage(uri);
            return;
        }
        catch { }

        try
        {
            // Try pack URI for xenom.ico
            var uri = new Uri("pack://application:,,,/Assets/xenom.ico", UriKind.Absolute);
            ImgLogo.Source = new BitmapImage(uri);
            return;
        }
        catch { }

        try
        {
            // File system fallback
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var paths = new[]
            {
                Path.Combine(baseDir, "Assets", "logo.png"),
                Path.Combine(baseDir, "Assets", "xenom.ico"),
                @"d:\Shop\Xenom Lag Fix\Assets\logo.png",
                @"d:\Shop\Xenom Lag Fix\Assets\xenom.ico"
            };

            foreach (var p in paths)
            {
                if (File.Exists(p))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(p, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    ImgLogo.Source = bmp;
                    break;
                }
            }
        }
        catch { }
    }

    private void ToastNotificationWindow_Loaded(object sender, RoutedEventArgs e)
    {
        PositionAtBottomRight();

        // Animate Slide-Up & Fade-In
        CardBorder.Opacity = 0.0;
        CardTranslate.Y = 25.0;

        var fadeIn = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };

        var slideUp = new DoubleAnimation
        {
            From = 25.0,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };

        CardBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        CardTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideUp);

        // Auto-close after 3.2 seconds
        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(3200)
        };
        _autoCloseTimer.Tick += (s, args) =>
        {
            _autoCloseTimer.Stop();
            Dismiss();
        };
        _autoCloseTimer.Start();
    }

    private void PositionAtBottomRight()
    {
        try
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - ActualWidth - 12;
            Top = wa.Bottom - ActualHeight - 12;
        }
        catch
        {
            Left = SystemParameters.PrimaryScreenWidth - 370;
            Top = SystemParameters.PrimaryScreenHeight - 130;
        }
    }

    private void CardBorder_MouseEnter(object sender, MouseEventArgs e)
    {
        // Pause dismiss on hover so user can read comfortably
        _autoCloseTimer?.Stop();
    }

    private void CardBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isClosing) return;

        // Resume dismiss 1.2s after mouse leaves
        if (_autoCloseTimer != null)
        {
            _autoCloseTimer.Interval = TimeSpan.FromMilliseconds(1200);
            _autoCloseTimer.Start();
        }
    }

    private void CardBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            _onClick?.Invoke();
        }
        catch { }
        finally
        {
            Dismiss();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Dismiss();
    }

    public void Dismiss()
    {
        if (_isClosing) return;
        _isClosing = true;
        _autoCloseTimer?.Stop();

        try
        {
            var fadeOut = new DoubleAnimation
            {
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseIn }
            };

            var slideDown = new DoubleAnimation
            {
                To = 12.0,
                Duration = TimeSpan.FromMilliseconds(160),
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                try
                {
                    if (_activeToast == this)
                        _activeToast = null;
                    Close();
                }
                catch { }
            };

            CardBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            CardTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideDown);
        }
        catch
        {
            try
            {
                if (_activeToast == this)
                    _activeToast = null;
                Close();
            }
            catch { }
        }
    }

    public static void Show(string title, string message, Action? onClick = null)
    {
        if (Application.Current == null) return;

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (_activeToast != null)
                {
                    try
                    {
                        _activeToast.Close();
                    }
                    catch { }
                    _activeToast = null;
                }

                _activeToast = new ToastNotificationWindow(title, message, onClick);
                _activeToast.Show();
            }
            catch { }
        });
    }
}
