using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using XenomOptimizer.Models;
using XenomOptimizer.ViewModels;
using XenomOptimizer.Views;
using Forms = System.Windows.Forms;

namespace XenomOptimizer.Services;

public class SystemTrayService : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    [StructLayout(LayoutKind.Sequential)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    private const int SW_RESTORE = 9;

    private readonly Window _window;
    private readonly OptimizerFrameGenViewModel? _optimizerVm;
    private Forms.NotifyIcon? _notifyIcon;
    private TrayMenuWindow? _trayMenuWindow;
    private bool _isFpsOn;
    private bool _isDisposed;

    public SystemTrayService(Window window, OptimizerFrameGenViewModel? optimizerVm)
    {
        _window = window;
        _optimizerVm = optimizerVm;

        _isFpsOn = GetGameLoopFpsState() || (_optimizerVm?.IsFrameGenActive == true);
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        try
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Text = "LAG Killer by XENOM",
                Visible = true
            };

            // Load best matching icon
            Icon? icon = null;
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "Assets", "xenom.ico"),
                Path.Combine(baseDir, "xenom.ico"),
                @"d:\Shop\Xenom Lag Fix\Assets\xenom.ico"
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        icon = new Icon(path);
                        break;
                    }
                    catch
                    {
                    }
                }
            }

            if (icon == null)
            {
                try
                {
                    var exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        icon = Icon.ExtractAssociatedIcon(exePath);
                    }
                }
                catch
                {
                }
            }

            _notifyIcon.Icon = icon ?? SystemIcons.Application;

            // Setup NZXT CAM-styled WPF Context Window
            _window.Dispatcher.Invoke(() =>
            {
                _trayMenuWindow = new TrayMenuWindow
                {
                    OnToggleFps = () => _window.Dispatcher.Invoke(ToggleFps),
                    OnCacheClean = () => _window.Dispatcher.Invoke(QuickCacheClean),
                    OnFlushRam = () => _window.Dispatcher.Invoke(FlushStandbyRam),
                    OnCloseGameLoop = () => _window.Dispatcher.Invoke(SafeCloseGameLoop),
                    OnOpenApp = () => _window.Dispatcher.Invoke(RestoreWindow),
                    OnExitApp = () => _window.Dispatcher.Invoke(ExitApplication)
                };
            });

            // Handle tray mouse events
            _notifyIcon.MouseUp += (s, e) =>
            {
                if (e.Button == Forms.MouseButtons.Right)
                {
                    _window.Dispatcher.Invoke(() =>
                    {
                        _isFpsOn = GetGameLoopFpsState() || (_optimizerVm?.IsFrameGenActive == true);
                        var pos = Forms.Cursor.Position;
                        _trayMenuWindow?.ShowMenu(_isFpsOn, pos.X, pos.Y);
                    });
                }
                else if (e.Button == Forms.MouseButtons.Left)
                {
                    _window.Dispatcher.Invoke(RestoreWindow);
                }
            };

            _notifyIcon.DoubleClick += (s, e) => _window.Dispatcher.Invoke(RestoreWindow);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize tray icon: {ex}");
        }
    }

    private void ToggleFps()
    {
        try
        {
            _isFpsOn = !_isFpsOn;
            SetGameLoopFpsState(_isFpsOn);
            _optimizerVm?.ToggleFrameGenCommand.Execute(null);

            string status = _isFpsOn ? "GameLoop FPS Counter: Enabled" : "GameLoop FPS Counter: Disabled";
            ShowNotification("LAG Killer by XENOM", status);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleFps error: {ex}");
        }
    }

    private void QuickCacheClean()
    {
        Task.Run(() =>
        {
            try
            {
                var cleaner = new GameLoopCleanerService();
                int purged = cleaner.DeepCachePurge();

                _window.Dispatcher.Invoke(() =>
                {
                    if (_optimizerVm != null)
                    {
                        _optimizerVm.CleanerStatusText = $"Purged {purged} cache and shader objects successfully.";
                    }
                    ShowNotification("LAG Killer by XENOM", $"Quick Cache Clean Complete!\nPurged {purged} temporary cache & shader objects.");
                });
            }
            catch (Exception ex)
            {
                ShowNotification("LAG Killer by XENOM", $"Cache clean error: {ex.Message}");
            }
        });
    }

    private void FlushStandbyRam()
    {
        Task.Run(() =>
        {
            try
            {
                var before = new MEMORYSTATUSEX();
                GlobalMemoryStatusEx(before);
                ulong beforeAvail = before.ullAvailPhys;

                // Purge working sets across running processes
                var processes = System.Diagnostics.Process.GetProcesses();
                foreach (var proc in processes)
                {
                    try
                    {
                        EmptyWorkingSet(proc.Handle);
                    }
                    catch { }
                    finally
                    {
                        proc.Dispose();
                    }
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();

                var after = new MEMORYSTATUSEX();
                GlobalMemoryStatusEx(after);
                ulong afterAvail = after.ullAvailPhys;

                long freedBytes = (long)afterAvail - (long)beforeAvail;
                string msg;
                if (freedBytes > 0)
                {
                    double freedMb = (double)freedBytes / 1048576.0;
                    if (freedMb >= 1024.0)
                        msg = $"Standby RAM purged! Freed {freedMb / 1024.0:0.1} GB physical memory.";
                    else
                        msg = $"Standby RAM purged! Freed {Math.Round(freedMb)} MB physical memory.";
                }
                else
                {
                    msg = "Standby RAM & working sets purged successfully!";
                }

                ShowNotification("LAG Killer by XENOM", msg);
            }
            catch (Exception ex)
            {
                ShowNotification("LAG Killer by XENOM", $"RAM purge error: {ex.Message}");
            }
        });
    }

    private void SafeCloseGameLoop()
    {
        Task.Run(async () =>
        {
            try
            {
                var optimizer = App.Services?.GetService(typeof(IGameLoopOptimizerService)) as IGameLoopOptimizerService;
                OptimizationResult? res = null;
                if (optimizer != null)
                {
                    res = await optimizer.SafeCloseGameLoopAsync();
                }
                else
                {
                    int terminatedCount = 0;
                    var targets = new[] { "AndroidProcess", "aow_exe", "AppMarket", "QMEmulatorService", "TBSGameLive", "AndroidEmulator", "AndroidEmulatorEn", "TSettingCenter", "GameLoader" };
                    foreach (var name in targets)
                    {
                        var procs = System.Diagnostics.Process.GetProcessesByName(name);
                        foreach (var proc in procs)
                        {
                            try
                            {
                                proc.CloseMainWindow();
                                if (!proc.WaitForExit(1000)) proc.Kill();
                                terminatedCount++;
                            }
                            catch { }
                            finally { proc.Dispose(); }
                        }
                    }
                    res = OptimizationResult.Succeeded($"GameLoop safely terminated ({terminatedCount} processes closed).");
                }

                string msg = res?.Message ?? "GameLoop safely terminated.";
                ShowNotification("LAG Killer by XENOM", msg);
            }
            catch (Exception ex)
            {
                ShowNotification("LAG Killer by XENOM", $"Error closing GameLoop: {ex.Message}");
            }
        });
    }

    private void ShowNotification(string title, string message)
    {
        try
        {
            WindowsToastService.ShowToast(title, message);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowNotification error: {ex}");
        }
    }

    private static bool GetGameLoopFpsState()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Tencent\MobileGamePC");
            if (key != null)
            {
                var val = key.GetValue("ShowFPS") ?? key.GetValue("ShowFps") ?? key.GetValue("FpsShow");
                if (val is int intVal && intVal == 1) return true;
                if (val is string strVal && (strVal == "1" || strVal.Equals("true", StringComparison.OrdinalIgnoreCase))) return true;
            }
        }
        catch { }
        return false;
    }

    private static void SetGameLoopFpsState(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Tencent\MobileGamePC");
            if (key != null)
            {
                uint v = enable ? 1u : 0u;
                key.SetValue("ShowFPS", v, RegistryValueKind.DWord);
                key.SetValue("ShowFps", v, RegistryValueKind.DWord);
                key.SetValue("FpsShow", v, RegistryValueKind.DWord);
            }
        }
        catch { }
    }

    public void MinimizeToTray()
    {
        WindowPlacementService.Save(_window);
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = true;
        }

        _window.WindowState = WindowState.Minimized;
        _window.ShowInTaskbar = false;
        _window.Hide();
    }

    public void RestoreWindow()
    {
        try
        {
            WindowPlacementService.EnsureWindowIsOnVisibleScreen(_window);
            _window.Show();
            _window.ShowInTaskbar = true;
            if (_window.WindowState == WindowState.Minimized)
            {
                _window.WindowState = WindowState.Normal;
            }
            _window.Activate();
            _window.Focus();

            var hwnd = new WindowInteropHelper(_window).Handle;
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
            }
        }
        catch { }
    }

    public void ExitApplication()
    {
        try
        {
            WindowPlacementService.Save(_window);
            _optimizerVm?.CleanExit();
            Dispose();
        }
        catch { }
        finally
        {
            Environment.Exit(0);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        _window.Dispatcher.Invoke(() =>
        {
            try
            {
                _trayMenuWindow?.HideMenu();
                _trayMenuWindow?.Close();
            }
            catch { }
            _trayMenuWindow = null;
        });
    }
}
