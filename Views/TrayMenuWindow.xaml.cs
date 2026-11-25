using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace XenomOptimizer.Views;

public partial class TrayMenuWindow : Window
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool PtInRect(ref RECT lprc, POINT pt);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

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

    private void ApplyNoActivateStyle()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            long currentStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            long newStyle = currentStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(newStyle));
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyNoActivateStyle();
    }

    public Action? OnToggleFps { get; set; }
    public Action? OnCacheClean { get; set; }
    public Action? OnFlushRam { get; set; }
    public Action? OnCloseGameLoop { get; set; }
    public Action? OnOpenApp { get; set; }
    public Action? OnExitApp { get; set; }

    private DispatcherTimer? _clickWatcher;
    private bool _actionExecuted;
    private DateTime _openedTime;

    public TrayMenuWindow()
    {
        InitializeComponent();
        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                HideMenu();
            }
        };
    }

    public void ShowMenu(bool isFpsOn, int screenX, int screenY)
    {
        _actionExecuted = false;
        _openedTime = DateTime.UtcNow;
        TxtFps.Text = isFpsOn ? "Turn Off FPS" : "Turn On FPS";

        // Ensure window handle exists so DPI and layout can be queried accurately
        _ = new WindowInteropHelper(this).EnsureHandle();
        ApplyNoActivateStyle();

        UpdateLayout();
        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(0, 0, DesiredSize.Width, DesiredSize.Height));

        var dpi = VisualTreeHelper.GetDpi(this);
        double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        double mouseX = screenX / dpiX;
        double mouseY = screenY / dpiY;

        double winWidth = DesiredSize.Width > 0 ? DesiredSize.Width : 194.0;
        double winHeight = DesiredSize.Height > 0 ? DesiredSize.Height : 240.0;

        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(screenX, screenY));
        double screenLeft = screen.WorkingArea.Left / dpiX;
        double screenRight = screen.WorkingArea.Right / dpiX;
        double screenTop = screen.WorkingArea.Top / dpiY;
        double screenBottom = screen.WorkingArea.Bottom / dpiY;

        // Position above the cursor
        double targetX = mouseX - (winWidth / 2.0);
        double targetY = mouseY - winHeight - 4;

        // Keep inside screen bounds
        if (targetX + winWidth > screenRight - 8)
            targetX = screenRight - winWidth - 8;
        if (targetX < screenLeft + 8)
            targetX = screenLeft + 8;

        if (targetY < screenTop + 8)
            targetY = mouseY + 8; // show below cursor if not enough room on top
        if (targetY + winHeight > screenBottom - 8)
            targetY = screenBottom - winHeight - 8;

        Left = targetX;
        Top = targetY;

        Show();
        StartClickWatcher();
    }

    public void HideMenu()
    {
        StopClickWatcher();
        Hide();
    }

    private void StartClickWatcher()
    {
        if (_clickWatcher == null)
        {
            _clickWatcher = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(40)
            };
            _clickWatcher.Tick += ClickWatcher_Tick;
        }
        _clickWatcher.Start();
    }

    private void StopClickWatcher()
    {
        _clickWatcher?.Stop();
    }

    private void ClickWatcher_Tick(object? sender, EventArgs e)
    {
        if (!IsVisible)
        {
            StopClickWatcher();
            return;
        }

        // Check if Escape key (0x1B) is pressed
        if ((GetAsyncKeyState(0x1B) & 0x8000) != 0)
        {
            HideMenu();
            return;
        }

        // Ignore clicks during initial 350ms so that the opening right-click or
        // tray icon release is never mistaken for a click-outside!
        if ((DateTime.UtcNow - _openedTime).TotalMilliseconds < 350)
        {
            return;
        }

        // Check if mouse left (0x01) or right (0x02) button is pressed
        bool leftDown = (GetAsyncKeyState(0x01) & 0x8000) != 0;
        bool rightDown = (GetAsyncKeyState(0x02) & 0x8000) != 0;

        if (leftDown || rightDown)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero && GetCursorPos(out POINT pt) && GetWindowRect(hwnd, out RECT rect))
            {
                // If cursor is OUTSIDE this menu window bounds, user clicked away -> hide menu
                if (!PtInRect(ref rect, pt))
                {
                    HideMenu();
                }
            }
        }
    }

    private void ExecuteAction(Action? action)
    {
        if (_actionExecuted) return;
        _actionExecuted = true;
        HideMenu();
        action?.Invoke();
    }

    private void BtnFps_Click(object sender, RoutedEventArgs e) => ExecuteAction(OnToggleFps);
    private void BtnCacheClean_Click(object sender, RoutedEventArgs e) => ExecuteAction(OnCacheClean);
    private void BtnFlushRam_Click(object sender, RoutedEventArgs e) => ExecuteAction(OnFlushRam);
    private void BtnCloseGameLoop_Click(object sender, RoutedEventArgs e) => ExecuteAction(OnCloseGameLoop);
    private void BtnOpen_Click(object sender, RoutedEventArgs e) => ExecuteAction(OnOpenApp);
    private void BtnExit_Click(object sender, RoutedEventArgs e) => ExecuteAction(OnExitApp);

    private void BtnFps_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ExecuteAction(OnToggleFps);
    private void BtnCacheClean_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ExecuteAction(OnCacheClean);
    private void BtnFlushRam_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ExecuteAction(OnFlushRam);
    private void BtnCloseGameLoop_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ExecuteAction(OnCloseGameLoop);
    private void BtnOpen_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ExecuteAction(OnOpenApp);
    private void BtnExit_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ExecuteAction(OnExitApp);
}
