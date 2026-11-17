using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using XenomOptimizer.ViewModels;

namespace XenomOptimizer.Services;

public class SystemTrayService : IDisposable
{
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;

    private const int WM_USER = 0x0400;
    public const int WM_TRAYICON = WM_USER + 101;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;

    private readonly Window _window;
    private readonly OptimizerFrameGenViewModel? _optimizerVm;
    private IntPtr _hwnd;
    private IntPtr _hIcon = IntPtr.Zero;
    private bool _isIconCreated;
    private ContextMenu? _trayContextMenu;

    public SystemTrayService(Window window, OptimizerFrameGenViewModel? optimizerVm)
    {
        _window = window;
        _optimizerVm = optimizerVm;

        _window.SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(_window);
        _hwnd = helper.Handle;

        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);

        CreateTrayIcon();
        SetupContextMenu();
    }

    private void CreateTrayIcon()
    {
        try
        {
            var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "xenom.ico");
            if (File.Exists(icoPath))
            {
                _hIcon = LoadImage(IntPtr.Zero, icoPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
            }
        }
        catch
        {
        }

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "Xenom Optimizer - Active"
        };

        _isIconCreated = Shell_NotifyIcon(NIM_ADD, ref nid);
    }

    private void SetupContextMenu()
    {
        _trayContextMenu = new ContextMenu
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(23, 23, 23)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(38, 38, 38)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4)
        };

        var mToggleFg = new MenuItem { Header = "Toggle Frame Gen", Foreground = System.Windows.Media.Brushes.White };
        mToggleFg.Click += (s, e) => _optimizerVm?.ToggleFrameGenCommand.Execute(null);

        var mCleanCache = new MenuItem { Header = "Quick Cache Clean", Foreground = System.Windows.Media.Brushes.White };
        mCleanCache.Click += (s, e) => _optimizerVm?.PurgeCacheCommand.Execute(null);

        var mCloseGameLoop = new MenuItem { Header = "Safe Close GameLoop", Foreground = System.Windows.Media.Brushes.White };
        mCloseGameLoop.Click += (s, e) => _optimizerVm?.KillTelemetryCommand.Execute(null);

        var mRestore = new MenuItem { Header = "Restore Xenom", FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White };
        mRestore.Click += (s, e) => RestoreWindow();

        var mExit = new MenuItem { Header = "Exit Xenom", Foreground = System.Windows.Media.Brushes.LightCoral };
        mExit.Click += (s, e) => ExitApplication();

        _trayContextMenu.Items.Add(mToggleFg);
        _trayContextMenu.Items.Add(mCleanCache);
        _trayContextMenu.Items.Add(mCloseGameLoop);
        _trayContextMenu.Items.Add(new Separator { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(38, 38, 38)) });
        _trayContextMenu.Items.Add(mRestore);
        _trayContextMenu.Items.Add(mExit);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_TRAYICON)
        {
            int eventId = lParam.ToInt32();
            if (eventId == WM_LBUTTONDBLCLK)
            {
                RestoreWindow();
                handled = true;
            }
            else if (eventId == WM_RBUTTONUP)
            {
                ShowContextMenu();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void MinimizeToTray()
    {
        _window.Hide();
    }

    public void RestoreWindow()
    {
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void ShowContextMenu()
    {
        if (_trayContextMenu != null)
        {
            SetForegroundWindow(_hwnd);
            _trayContextMenu.IsOpen = true;
        }
    }

    public void ExitApplication()
    {
        // Safe exit sequence: Resumes all suspended processes and stops frame gen
        _optimizerVm?.CleanExit();
        Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_isIconCreated && _hwnd != IntPtr.Zero)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwnd,
                uID = 1001
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);
            _isIconCreated = false;
        }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }
}
