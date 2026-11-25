using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace XenomOptimizer.Services;

public class WindowPlacementConfig
{
    public string? LastScreenDeviceName { get; set; }
    public double LastWindowLeft { get; set; }
    public double LastWindowTop { get; set; }
    public double LastWindowWidth { get; set; }
    public double LastWindowHeight { get; set; }
    public bool HasSavedPosition { get; set; }
}

public static class WindowPlacementService
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XenomOptimizer");

    private static readonly string ConfigFilePath =
        Path.Combine(ConfigDir, "window_placement.json");

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    public static WindowPlacementConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<WindowPlacementConfig>(json);
                if (config != null) return config;
            }
        }
        catch { }
        return new WindowPlacementConfig();
    }

    public static void Save(Window window)
    {
        try
        {
            if (window.WindowState != WindowState.Normal) return;

            Forms.Screen? currentScreen = null;
            var helper = new WindowInteropHelper(window);
            if (helper.Handle != IntPtr.Zero)
            {
                currentScreen = Forms.Screen.FromHandle(helper.Handle);
            }
            else
            {
                var pt = new System.Drawing.Point((int)window.Left + 10, (int)window.Top + 10);
                currentScreen = Forms.Screen.FromPoint(pt);
            }

            var config = new WindowPlacementConfig
            {
                LastScreenDeviceName = currentScreen?.DeviceName ?? Forms.Screen.PrimaryScreen?.DeviceName,
                LastWindowLeft = window.Left,
                LastWindowTop = window.Top,
                LastWindowWidth = window.ActualWidth > 0 ? window.ActualWidth : window.Width,
                LastWindowHeight = window.ActualHeight > 0 ? window.ActualHeight : window.Height,
                HasSavedPosition = true
            };

            if (!Directory.Exists(ConfigDir)) Directory.CreateDirectory(ConfigDir);
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
        }
        catch { }
    }

    public static Forms.Screen GetTargetScreen()
    {
        var allScreens = Forms.Screen.AllScreens;
        Forms.Screen primary = Forms.Screen.PrimaryScreen ?? (allScreens.Length > 0 ? allScreens[0] : null!);

        if (allScreens.Length <= 1)
        {
            return primary;
        }

        var config = Load();
        if (string.IsNullOrEmpty(config.LastScreenDeviceName))
        {
            // Default / first time launch with multiple monitors: launch on primary monitor
            return primary;
        }

        // Try to match the last used monitor by DeviceName
        var matched = allScreens.FirstOrDefault(s =>
            string.Equals(s.DeviceName, config.LastScreenDeviceName, StringComparison.OrdinalIgnoreCase));

        if (matched != null)
        {
            return matched;
        }

        // Target screen was disconnected, fallback safely to primary monitor
        return primary;
    }

    public static double GetScreenDpiScale(Forms.Screen screen)
    {
        try
        {
            var pt = new POINT { X = screen.WorkingArea.X + 10, Y = screen.WorkingArea.Y + 10 };
            IntPtr hMon = MonitorFromPoint(pt, 2); // MONITOR_DEFAULTTONEAREST
            if (hMon != IntPtr.Zero && GetDpiForMonitor(hMon, 0, out uint dpiX, out _) == 0)
            {
                return (double)dpiX / 96.0;
            }
        }
        catch { }
        return 1.0;
    }

    public static Rect GetScreenWorkingAreaDips(Forms.Screen screen)
    {
        double scale = GetScreenDpiScale(screen);
        if (scale <= 0) scale = 1.0;

        return new Rect(
            screen.WorkingArea.Left / scale,
            screen.WorkingArea.Top / scale,
            screen.WorkingArea.Width / scale,
            screen.WorkingArea.Height / scale
        );
    }

    public static void PositionSplashScreen(Window splashWindow)
    {
        var screen = GetTargetScreen();
        CenterWindowOnScreen(splashWindow, screen);
    }

    public static void PositionMainWindow(Window mainWindow)
    {
        mainWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        var screen = GetTargetScreen();
        var config = Load();

        double winW = mainWindow.Width > 0 ? mainWindow.Width : 1340;
        double winH = mainWindow.Height > 0 ? mainWindow.Height : 885;
        Rect areaDips = GetScreenWorkingAreaDips(screen);

        if (config.HasSavedPosition &&
            string.Equals(config.LastScreenDeviceName, screen.DeviceName, StringComparison.OrdinalIgnoreCase) &&
            !double.IsNaN(config.LastWindowLeft) && !double.IsNaN(config.LastWindowTop))
        {
            double left = config.LastWindowLeft;
            double top = config.LastWindowTop;

            // Ensure window is safely inside the target monitor working area
            if (left + winW > areaDips.Right) left = areaDips.Right - winW;
            if (left < areaDips.Left) left = areaDips.Left;
            if (top + winH > areaDips.Bottom) top = areaDips.Bottom - winH;
            if (top < areaDips.Top) top = areaDips.Top;

            mainWindow.Left = left;
            mainWindow.Top = top;
        }
        else
        {
            // Center on target screen
            CenterWindowOnScreen(mainWindow, screen);
        }
    }

    public static void CenterWindowOnScreen(Window window, Forms.Screen screen)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        Rect areaDips = GetScreenWorkingAreaDips(screen);

        double winW = window.Width > 0 ? window.Width : (window.ActualWidth > 0 ? window.ActualWidth : 1340);
        double winH = window.Height > 0 ? window.Height : (window.ActualHeight > 0 ? window.ActualHeight : 885);

        double left = areaDips.Left + (areaDips.Width - winW) / 2.0;
        double top = areaDips.Top + (areaDips.Height - winH) / 2.0;

        if (left + winW > areaDips.Right) left = areaDips.Right - winW;
        if (left < areaDips.Left) left = areaDips.Left;
        if (top + winH > areaDips.Bottom) top = areaDips.Bottom - winH;
        if (top < areaDips.Top) top = areaDips.Top;

        window.Left = left;
        window.Top = top;
    }

    public static void EnsureWindowIsOnVisibleScreen(Window window)
    {
        var allScreens = Forms.Screen.AllScreens;
        if (allScreens.Length == 0) return;

        Forms.Screen? currentScreen = null;
        var helper = new WindowInteropHelper(window);
        if (helper.Handle != IntPtr.Zero)
        {
            currentScreen = Forms.Screen.FromHandle(helper.Handle);
        }
        else
        {
            var pt = new System.Drawing.Point((int)window.Left + 10, (int)window.Top + 10);
            currentScreen = Forms.Screen.FromPoint(pt);
        }

        bool isVisibleOnAny = allScreens.Any(s =>
            string.Equals(s.DeviceName, currentScreen?.DeviceName, StringComparison.OrdinalIgnoreCase));

        if (!isVisibleOnAny)
        {
            var primary = Forms.Screen.PrimaryScreen ?? allScreens[0];
            CenterWindowOnScreen(window, primary);
        }
    }
}
