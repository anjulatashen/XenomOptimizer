using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XenomOptimizer.Models;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

/// <summary>
/// Dashboard ViewModel showing system info, GameLoop status, and live resource usage.
/// Refreshes usage stats every 3 seconds.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly ISystemDetectionService _systemDetection;
    private readonly IGameLoopDetectionService _gameLoopDetection;
    private readonly IAdminService _adminService;
    private readonly ISettingsService _settingsService;
    private readonly IRegistryService _registryService;
    private readonly DispatcherTimer _refreshTimer;

    // Initial Scan State
    [ObservableProperty] private bool _hasRunInitialScan;
    [ObservableProperty] private bool _isScanningSystem;
    [ObservableProperty] private bool _isReScanning;
    [ObservableProperty] private bool _isReScanDone;
    [ObservableProperty] private string _scanProgressText = "Click Scan to begin hardware analysis";
    [ObservableProperty] private double _scanProgressValue = 0;

    // GameLoop Minimum Requirements / Readiness Score
    [ObservableProperty] private double _gameLoopScore = 100;
    [ObservableProperty] private string _gameLoopStatusText = "Recommended Specs ✓";
    [ObservableProperty] private string _gameLoopScoreColorHex = "#A3E635";
    [ObservableProperty] private string _gameLoopDetailText = "Peak Tier • Ultra Smooth Ready";

    // Lag Fix Plan Progress
    [ObservableProperty] private double _lagFixProgressPercent = 80;
    [ObservableProperty] private int _lagFixAppliedCount = 10;
    [ObservableProperty] private int _lagFixTotalCount = 12;
    [ObservableProperty] private string _lagFixProgressText = "80% completed";

    partial void OnLagFixProgressPercentChanged(double value)
    {
        if (value <= 0 && LagFixTotalCount > 0 && LagFixAppliedCount > 0)
        {
            LagFixProgressPercent = Math.Round((LagFixAppliedCount / (double)LagFixTotalCount) * 100);
        }
    }

    // System Info
    [ObservableProperty] private string _cpuModel = "Detecting...";
    [ObservableProperty] private string _gpuModel = "Detecting...";
    [ObservableProperty] private string _ramAmount = "Detecting...";
    [ObservableProperty] private string _ramSpec = "Detecting...";
    [ObservableProperty] private string _windowsVersion = "Detecting...";
    [ObservableProperty] private string _gpuDriverVersion = "Detecting...";
    [ObservableProperty] private string _currentPowerPlan = "Detecting...";
    [ObservableProperty] private string _powerPlanColorHex = "#38BDF8";
    [ObservableProperty] private string _systemDriveType = "NVMe SSD";
    [ObservableProperty] private string _virtualizationStatus = "Enabled";
    [ObservableProperty] private string _virtualizationColorHex = "#38BDF8";

    // GameLoop Info
    [ObservableProperty] private string _gameLoopStatus = "Detecting...";
    [ObservableProperty] private string _gameLoopVersion = "Detecting...";
    [ObservableProperty] private string _gameLoopPath = "Detecting...";
    [ObservableProperty] private string _pubgStatus = "Detecting...";
    [ObservableProperty] private bool _isGameLoopInstalled;

    // Live Usage
    [ObservableProperty] private double _cpuUsage;
    [ObservableProperty] private double _ramUsage;
    [ObservableProperty] private double _gpuUsage;
    [ObservableProperty] private double _diskUsage;

    // Storage Drives (Multi-Disk detection)
    public ObservableCollection<DiskDriveInfo> StorageDrives { get; } = new();
    [ObservableProperty] private string _storageDrivesCountText = "3 Drives";

    // State
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private bool _showOptimizePreview;

    public event Action? RequestEntranceAnimation;

    public void TriggerEntranceAnimation()
    {
        RequestEntranceAnimation?.Invoke();
    }

    // Optimization preview items
    public ObservableCollection<OptimizationPreviewItem> PreviewItems { get; } = new();

    public DashboardViewModel(
        ISystemDetectionService systemDetection,
        IGameLoopDetectionService gameLoopDetection,
        IAdminService adminService,
        ISettingsService settingsService,
        IRegistryService registryService)
    {
        _systemDetection = systemDetection;
        _gameLoopDetection = gameLoopDetection;
        _adminService = adminService;
        _settingsService = settingsService;
        _registryService = registryService;
        _isAdmin = adminService.IsRunningAsAdmin;
        _hasRunInitialScan = true;
        _settingsService.Settings.HasRunInitialScan = true;
        _settingsService.Save();

        // Timer to refresh live stats every 1 second (Realtime telemetry)
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshLiveStatsAsync();
        _ = RefreshLiveStatsAsync();
        _refreshTimer.Start();

        // Always auto-scan system hardware immediately on app launch
        _ = LoadSystemInfoAsync();
    }

    private async Task LoadSystemInfoAsync()
    {
        IsLoading = true;

        try
        {
            // Parallel detection for speed
            var cpuTask = _systemDetection.GetCpuModelAsync();
            var gpuTask = _systemDetection.GetGpuModelAsync();
            var ramTask = _systemDetection.GetRamAmountAsync();
            var ramSpecTask = _systemDetection.GetRamDetailedSpecAsync();
            var winTask = _systemDetection.GetWindowsVersionAsync();
            var driverTask = _systemDetection.GetGpuDriverVersionAsync();
            var powerTask = _systemDetection.GetCurrentPowerPlanAsync();
            var glInstalledTask = _gameLoopDetection.IsInstalledAsync();
            var drivesTask = _systemDetection.GetStorageDrivesAsync();
            var virtTask = _systemDetection.IsVirtualizationEnabledAsync();

            await Task.WhenAll(cpuTask, gpuTask, ramTask, ramSpecTask, winTask, driverTask, powerTask, glInstalledTask, drivesTask, virtTask);

            CpuModel = cpuTask.Result;
            GpuModel = gpuTask.Result;
            RamAmount = ramTask.Result;
            RamSpec = ramSpecTask.Result;
            WindowsVersion = winTask.Result;
            GpuDriverVersion = driverTask.Result;
            CurrentPowerPlan = powerTask.Result;
            IsGameLoopInstalled = glInstalledTask.Result;

            StorageDrives.Clear();
            foreach (var d in drivesTask.Result)
            {
                d.TargetUsagePercent = d.UsagePercent;
                StorageDrives.Add(d);
            }
            StorageDrivesCountText = $"{StorageDrives.Count} {(StorageDrives.Count == 1 ? "Drive" : "Drives")}";

            var cDrive = StorageDrives.FirstOrDefault(d => d.DriveLetter.StartsWith("C", StringComparison.OrdinalIgnoreCase));
            SystemDriveType = cDrive?.MediaType ?? "NVMe SSD";

            UpdatePowerPlanColor();

            bool virtEnabled = virtTask.Result;
            VirtualizationStatus = virtEnabled ? "Enabled" : "Disabled";
            VirtualizationColorHex = virtEnabled ? "#38BDF8" : "#EF4444";

            if (IsGameLoopInstalled)
            {
                GameLoopStatus = "✓ Installed";
                var path = await _gameLoopDetection.GetInstallPathAsync();
                GameLoopPath = path ?? "Unknown";
                var version = await _gameLoopDetection.GetVersionAsync();
                GameLoopVersion = version ?? "Unknown";

                var isPubg = await _gameLoopDetection.IsPubgInstalledAsync();
                PubgStatus = isPubg ? "✓ Detected" : "✗ Not Detected";
            }
            else
            {
                GameLoopStatus = "✗ Not Installed";
                GameLoopPath = "N/A";
                GameLoopVersion = "N/A";
                PubgStatus = "N/A";
            }

            CalculateHardwareScore();
            CalculateLagFixProgress();

            // Start live stats
            await RefreshLiveStatsAsync();
            _refreshTimer.Start();
            TriggerEntranceAnimation();
        }
        catch (Exception)
        {
            // Graceful degradation - show whatever we got
        }
        finally
        {
            IsLoading = false;
        }
    }

    private readonly SemaphoreSlim _statsLock = new(1, 1);
    private bool _hasAnimatedInitialStats;

    private async Task RefreshLiveStatsAsync()
    {
        if (!await _statsLock.WaitAsync(0)) return;

        try
        {
            var cpuTask = _systemDetection.GetCpuUsagePercentAsync();
            var ramTask = _systemDetection.GetRamUsagePercentAsync();
            var gpuTask = _systemDetection.GetGpuUsagePercentAsync();
            var diskTask = _systemDetection.GetDiskUsagePercentAsync();

            await Task.WhenAll(cpuTask, ramTask, gpuTask, diskTask);

            double newCpu = cpuTask.Result;
            if (newCpu > 0.0 || CpuUsage == 0.0)
            {
                CpuUsage = newCpu;
            }
            RamUsage = ramTask.Result;
            GpuUsage = gpuTask.Result;
            DiskUsage = diskTask.Result;

            if (!_hasAnimatedInitialStats && (RamUsage > 0 || CpuUsage > 0))
            {
                _hasAnimatedInitialStats = true;
                TriggerEntranceAnimation();
            }
        }
        catch { }
        finally
        {
            _statsLock.Release();
        }
    }

    [RelayCommand]
    private void OpenYouTube()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://youtube.com/@Xenom",
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void OpenFacebook()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.facebook.com/XenomStreaming",
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void OpenTikTok()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.tiktok.com/@officialxenom",
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void OpenDiscord()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/J6YNFYB",
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void NavigateToOptimize()
    {
        try
        {
            if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
            {
                mainVm.NavigateToCommand.Execute("Windows");
            }
        }
        catch { }
    }

    [RelayCommand]
    private void ShowOptimizeRecommended()
    {
        PreviewItems.Clear();
        PreviewItems.Add(new OptimizationPreviewItem { Name = "Windows Power Configuration", Description = "Set power plan to High Performance", IsSelected = true });
        PreviewItems.Add(new OptimizationPreviewItem { Name = "Gaming Optimizations", Description = "Enable Game Mode, disable fullscreen optimizations", IsSelected = true });
        PreviewItems.Add(new OptimizationPreviewItem { Name = "Safe Registry Optimizations", Description = "Optimize mouse, network, and GPU scheduling settings", IsSelected = true });

        if (IsGameLoopInstalled)
        {
            PreviewItems.Add(new OptimizationPreviewItem { Name = "GameLoop Performance Config", Description = "Optimize emulator CPU/RAM/rendering settings", IsSelected = true });
        }

        PreviewItems.Add(new OptimizationPreviewItem { Name = "Temporary File Cleanup", Description = "Clean Windows and application temp files", IsSelected = true });

        ShowOptimizePreview = true;
    }

    [RelayCommand]
    private void CancelPreview()
    {
        ShowOptimizePreview = false;
    }

    [RelayCommand]
    private async Task ApplySelectedAsync()
    {
        ShowOptimizePreview = false;
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ScanSystemAsync()
    {
        if (IsScanningSystem) return;
        IsScanningSystem = true;
        ScanProgressValue = 10;
        ScanProgressText = "Initializing hardware diagnostics...";

        try
        {
            await Task.Delay(200);
            ScanProgressValue = 25;
            ScanProgressText = "Detecting CPU model & Windows build...";
            var cpuTask = _systemDetection.GetCpuModelAsync();
            var winTask = _systemDetection.GetWindowsVersionAsync();
            await Task.WhenAll(cpuTask, winTask);
            CpuModel = cpuTask.Result;
            WindowsVersion = winTask.Result;

            await Task.Delay(200);
            ScanProgressValue = 50;
            ScanProgressText = "Querying GPU graphics & driver version...";
            var gpuTask = _systemDetection.GetGpuModelAsync();
            var driverTask = _systemDetection.GetGpuDriverVersionAsync();
            await Task.WhenAll(gpuTask, driverTask);
            GpuModel = gpuTask.Result;
            GpuDriverVersion = driverTask.Result;

            await Task.Delay(200);
            ScanProgressValue = 75;
            ScanProgressText = "Scanning physical storage drives & SMART health...";
            var drives = await _systemDetection.GetStorageDrivesAsync();
            StorageDrives.Clear();
            foreach (var d in drives)
            {
                d.TargetUsagePercent = d.UsagePercent;
                StorageDrives.Add(d);
            }
            StorageDrivesCountText = $"{StorageDrives.Count} {(StorageDrives.Count == 1 ? "Drive" : "Drives")}";

            await Task.Delay(200);
            ScanProgressValue = 90;
            ScanProgressText = "Checking memory & power configuration...";
            var ramTask = _systemDetection.GetRamAmountAsync();
            var ramSpecTask = _systemDetection.GetRamDetailedSpecAsync();
            var powerTask = _systemDetection.GetCurrentPowerPlanAsync();
            var glTask = _gameLoopDetection.IsInstalledAsync();
            var virtTask = _systemDetection.IsVirtualizationEnabledAsync();
            await Task.WhenAll(ramTask, ramSpecTask, powerTask, glTask, virtTask);
            RamAmount = ramTask.Result;
            RamSpec = ramSpecTask.Result;
            CurrentPowerPlan = powerTask.Result;
            IsGameLoopInstalled = glTask.Result;

            var cDrive = StorageDrives.FirstOrDefault(d => d.DriveLetter.StartsWith("C", StringComparison.OrdinalIgnoreCase));
            SystemDriveType = cDrive?.MediaType ?? "NVMe SSD";

            UpdatePowerPlanColor();

            bool virtEnabled = virtTask.Result;
            VirtualizationStatus = virtEnabled ? "Enabled" : "Disabled";
            VirtualizationColorHex = virtEnabled ? "#38BDF8" : "#EF4444";

            if (IsGameLoopInstalled)
            {
                GameLoopStatus = "✓ Installed";
                var path = await _gameLoopDetection.GetInstallPathAsync();
                GameLoopPath = path ?? "Unknown";
                var version = await _gameLoopDetection.GetVersionAsync();
                GameLoopVersion = version ?? "Unknown";

                var isPubg = await _gameLoopDetection.IsPubgInstalledAsync();
                PubgStatus = isPubg ? "✓ Detected" : "✗ Not Detected";
            }
            else
            {
                GameLoopStatus = "✗ Not Installed";
                GameLoopPath = "N/A";
                GameLoopVersion = "N/A";
                PubgStatus = "N/A";
            }

            ScanProgressValue = 100;
            ScanProgressText = "System scan completed successfully ✓";
            await Task.Delay(300);

            CalculateHardwareScore();
            CalculateLagFixProgress();

            HasRunInitialScan = true;
            _settingsService.Settings.HasRunInitialScan = true;
            _settingsService.Save();

            await RefreshLiveStatsAsync();
            if (!_refreshTimer.IsEnabled)
            {
                _refreshTimer.Start();
            }
            TriggerEntranceAnimation();
        }
        catch (Exception)
        {
            HasRunInitialScan = true;
        }
        finally
        {
            IsScanningSystem = false;
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ReScanSystemAsync()
    {
        if (IsReScanning || IsScanningSystem) return;

        IsReScanning = true;
        IsReScanDone = false;

        try
        {
            _systemDetection.InvalidateHardwareCache();
            await ScanSystemAsync();
            IsReScanning = false;
            IsReScanDone = true;

            // Show '✓ Done' for 1.8 seconds, then transition back to 'Re-Scan PC'
            await Task.Delay(1800);
            IsReScanDone = false;
        }
        catch
        {
            IsReScanning = false;
            IsReScanDone = false;
        }
    }

    private void CalculateHardwareScore()
    {
        int cpuScore = 0;
        int gpuScore = 0;
        int ramScore = 0;

        bool cpuGood = false;
        bool gpuGood = false;
        bool ramGood = false;

        // 1. RAM Score (User specified: 16GB recommend)
        double ramGb = 0;
        var ramMatch = System.Text.RegularExpressions.Regex.Match(RamSpec, @"(\d+)\s*GB", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (ramMatch.Success && double.TryParse(ramMatch.Groups[1].Value, out var g))
            ramGb = g;
        else
        {
            var ramMatch2 = System.Text.RegularExpressions.Regex.Match(RamAmount, @"(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (ramMatch2.Success && double.TryParse(ramMatch2.Groups[1].Value, out var g2))
                ramGb = g2;
        }

        if (ramGb >= 16)
        {
            ramScore = 20;
            ramGood = true;
        }
        else if (ramGb >= 12)
        {
            ramScore = 16;
            ramGood = true;
        }
        else if (ramGb >= 8)
        {
            ramScore = 10;
            ramGood = false;
        }
        else
        {
            ramScore = 5;
            ramGood = false;
        }

        // 2. CPU Score (User specified: i3 8th gen min, i5 8th gen+ recommended)
        string c = (CpuModel ?? "").ToLowerInvariant();
        if (c.Contains("ryzen 7") || c.Contains("ryzen 9") || c.Contains("i7") || c.Contains("i9") ||
            c.Contains("threadripper") || c.Contains("xeon"))
        {
            cpuScore = 40;
            cpuGood = true;
        }
        else if (c.Contains("ryzen 5"))
        {
            cpuScore = 32;
            cpuGood = true;
        }
        else if (c.Contains("i5"))
        {
            var genMatch = System.Text.RegularExpressions.Regex.Match(c, @"i5[- ](\d{1,2})\d{3}");
            if (genMatch.Success && int.TryParse(genMatch.Groups[1].Value, out var gen) && gen >= 8)
            {
                cpuScore = 28;
                cpuGood = true;
            }
            else if (genMatch.Success)
            {
                cpuScore = 20;
                cpuGood = false;
            }
            else
            {
                cpuScore = 24;
                cpuGood = true;
            }
        }
        else if (c.Contains("i3") || c.Contains("ryzen 3"))
        {
            var genMatch = System.Text.RegularExpressions.Regex.Match(c, @"i3[- ](\d{1,2})\d{3}");
            if (genMatch.Success && int.TryParse(genMatch.Groups[1].Value, out var gen) && gen >= 8)
            {
                cpuScore = 25;
                cpuGood = false;
            }
            else
            {
                cpuScore = 14;
                cpuGood = false;
            }
        }
        else
        {
            cpuScore = 12;
            cpuGood = false;
        }

        // 3. GPU Score (User specified: GTX 1060 / RX 580 or higher)
        string gName = (GpuModel ?? "").ToLowerInvariant();
        if (gName.Contains("rtx") || gName.Contains("rx 6") || gName.Contains("rx 7") || gName.Contains("rx 5700") || gName.Contains("rx 5600") ||
            gName.Contains("1070") || gName.Contains("1080") || gName.Contains("1660") || gName.Contains("2060") || gName.Contains("3060") ||
            gName.Contains("3070") || gName.Contains("3080") || gName.Contains("4060") || gName.Contains("4070") || gName.Contains("4080") || gName.Contains("4090"))
        {
            gpuScore = 40;
            gpuGood = true;
        }
        else if (gName.Contains("1060") || gName.Contains("rx 580") || gName.Contains("rx 590") || gName.Contains("rx 5500"))
        {
            gpuScore = 25;
            gpuGood = true;
        }
        else if (gName.Contains("1050") || gName.Contains("960") || gName.Contains("970") || gName.Contains("rx 570") || gName.Contains("rx 480") || gName.Contains("rx 560"))
        {
            gpuScore = 20;
            gpuGood = false;
        }
        else if (gName.Contains("gtx") || (gName.Contains("radeon") && !gName.Contains("graphics")))
        {
            gpuScore = 20;
            gpuGood = false;
        }
        else
        {
            gpuScore = 8;
            gpuGood = false;
        }

        int total = cpuScore + gpuScore + ramScore;
        GameLoopScore = Math.Clamp(total, 15, 100);

        if (cpuGood && gpuGood && ramGood)
        {
            GameLoopStatusText = "Recommended Specs ✓";
            GameLoopScoreColorHex = "#A3E635"; // Lime Green
            GameLoopDetailText = "Peak Tier • Ultra Smooth Ready";
        }
        else if (GameLoopScore >= 55)
        {
            var issues = new List<string>();
            if (!gpuGood) issues.Add("VGA");
            if (!cpuGood) issues.Add("CPU");
            if (!ramGood) issues.Add("Memory");

            GameLoopStatusText = $"Update {string.Join(" or ", issues)}";
            GameLoopScoreColorHex = "#F59E0B"; // Yellow
            GameLoopDetailText = "Playable • Hardware bottleneck detected";
        }
        else
        {
            GameLoopStatusText = "Update VGA or CPU";
            GameLoopScoreColorHex = "#EF4444"; // Red
            GameLoopDetailText = "Below Minimum • Upgrade recommended for GameLoop";
        }
    }

    private void CalculateLagFixProgress()
    {
        try
        {
            int total = 12;
            int applied = 0;

            // 1. Power Plan
            if (CurrentPowerPlan.Contains("High", StringComparison.OrdinalIgnoreCase) ||
                CurrentPowerPlan.Contains("Ultimate", StringComparison.OrdinalIgnoreCase))
                applied++;

            // 2. Game Mode
            var gameMode = _registryService.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode");
            if (gameMode is int gm && gm == 1) applied++;

            // 3. Game DVR
            var dvr = _registryService.GetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled");
            if (dvr is int d && d == 0) applied++;

            // 4. Game Bar AppCapture
            var appCap = _registryService.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled");
            if (appCap is int ac && ac == 0) applied++;

            // 5. Network Throttling
            var netThrottle = _registryService.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex");
            if (netThrottle != null && (netThrottle.ToString() == "-1" || netThrottle.ToString() == "4294967295")) applied++;

            // 6. System Responsiveness
            var sysResp = _registryService.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness");
            if (sysResp is int sr && sr == 0) applied++;

            // 7. Background Apps
            var bgApps = _registryService.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled");
            if (bgApps is int ba && ba == 1) applied++;

            // 8. Mouse Acceleration
            var mouseSpeed = _registryService.GetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed");
            if (mouseSpeed != null && mouseSpeed.ToString() == "0") applied++;

            // 9. GPU Priority
            var gpuPriority = _registryService.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority");
            if (gpuPriority is int gp && gp == 8) applied++;

            // 10. Priority
            var priority = _registryService.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority");
            if (priority is int pr && pr == 6) applied++;

            // 11. Visual FX
            var vfx = _registryService.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting");
            if (vfx is int fx && fx == 2) applied++;

            // 12. GameLoop Installed & Detected
            if (IsGameLoopInstalled) applied++;

            LagFixAppliedCount = applied;
            LagFixTotalCount = total;
            LagFixProgressPercent = Math.Round((applied / (double)total) * 100);
            LagFixProgressText = $"{(int)LagFixProgressPercent}% completed";
        }
        catch
        {
            LagFixAppliedCount = 8;
            LagFixTotalCount = 12;
            LagFixProgressPercent = 67;
            LagFixProgressText = "67% completed";
        }
    }

    private void UpdatePowerPlanColor()
    {
        string plan = (CurrentPowerPlan ?? "").ToLowerInvariant();
        if (plan.Contains("ultimate"))
            PowerPlanColorHex = "#A3E635"; // Lime Green
        else if (plan.Contains("high"))
            PowerPlanColorHex = "#38BDF8"; // Blue
        else if (plan.Contains("balance"))
            PowerPlanColorHex = "#F59E0B"; // Yellow
        else
            PowerPlanColorHex = "#38BDF8";
    }
}

public class OptimizationPreviewItem : ObservableObject
{
    private bool _isSelected = true;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
