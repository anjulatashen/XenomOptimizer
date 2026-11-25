using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using XenomOptimizer.Models;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

/// <summary>
/// ViewModel for the newly designed GameLoop Optimization Dashboard (formerly Registry tab).
/// Designed following the modern minimalist card & widget layout reference.
/// </summary>
public partial class RegistryOptimizationViewModel : ObservableObject
{
    private readonly IGameLoopOptimizerService _gameLoopOptimizer;
    private readonly IGameLoopDetectionService _gameLoopDetection;
    private readonly IRegistryService _registry;
    private readonly IBackupService _backup;
    private readonly ILogService _log;
    private readonly IAdminService _admin;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _isGameLoopInstalled;
    [ObservableProperty] private bool _isGameLoopRunning;
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private bool _is120FpsActive;
    [ObservableProperty] private string _selectedProfile = "iPad";
    [ObservableProperty] private string _selectedResolutionPreset = "iPad";
    [ObservableProperty] private string _renderEngineName = "DirectX+";
    [ObservableProperty] private int _selectedRenderEngine = 1; // 0=Auto, 1=DirectX+, 2=OpenGL+

    [ObservableProperty] private bool _isCoreIsolationDisabled;
    [ObservableProperty] private bool _isAppMarketAutoCloseActive;
    [ObservableProperty] private bool _isCpuFrameBufferDisabled;

    [ObservableProperty] private string _optimizationScore = "120";
    [ObservableProperty] private string _optimizationScoreSubtitle = "FPS PEAK";
    [ObservableProperty] private int _optimizationPercent = 95;
    [ObservableProperty] private string _optimizationLevelText = "Ultra Performance";

    [ObservableProperty] private string _cleanerStatusText = "Ready to purge cache, registry, drivers and temp files.";
    [ObservableProperty] private bool _isCleaning;
    [ObservableProperty] private string _uninstallStatusText = "Ready to launch official uninstaller.";

    // ── 2-Page Carousel Navigation (Smart Order: Uninstaller / Installer) ──
    [ObservableProperty] private int _selectedSlideIndex = 0;
    public bool IsPage1Active => SelectedSlideIndex == 0;
    public bool IsPage2Active => SelectedSlideIndex == 1;

    [ObservableProperty] private bool _isUninstallerFirst = true;

    public bool IsUninstallerVisible => (IsUninstallerFirst && SelectedSlideIndex == 0) || (!IsUninstallerFirst && SelectedSlideIndex == 1);
    public bool IsInstallerVisible => (IsUninstallerFirst && SelectedSlideIndex == 1) || (!IsUninstallerFirst && SelectedSlideIndex == 0);

    public string Slide0Tooltip => IsUninstallerFirst ? "Official Uninstaller & 7-Step Wiper" : "GameLoop Multi-Version Installer Hub";
    public string Slide1Tooltip => IsUninstallerFirst ? "GameLoop Multi-Version Installer Hub" : "Official Uninstaller & 7-Step Wiper";

    [RelayCommand]
    public void SelectSlide(string? index)
    {
        if (int.TryParse(index, out int idx) && idx >= 0 && idx <= 1)
        {
            SelectedSlideIndex = idx;
        }
    }

    [RelayCommand]
    public void SelectNextSlide()
    {
        if (SelectedSlideIndex < 1)
            SelectedSlideIndex++;
    }

    [RelayCommand]
    public void SelectPreviousSlide()
    {
        if (SelectedSlideIndex > 0)
            SelectedSlideIndex--;
    }

    partial void OnSelectedSlideIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsPage1Active));
        OnPropertyChanged(nameof(IsPage2Active));
        OnPropertyChanged(nameof(IsUninstallerVisible));
        OnPropertyChanged(nameof(IsInstallerVisible));
    }

    partial void OnIsUninstallerFirstChanged(bool value)
    {
        OnPropertyChanged(nameof(IsUninstallerVisible));
        OnPropertyChanged(nameof(IsInstallerVisible));
        OnPropertyChanged(nameof(Slide0Tooltip));
        OnPropertyChanged(nameof(Slide1Tooltip));
    }

    // ── Official GameLoop Downloads ──
    [RelayCommand]
    public void DownloadGlobal32()
    {
        OpenDownloadUrl("https://dlied6.myapp.com/myapp/kspc/installer/GameLoop_id_900206197_100.exe", "GameLoop Global 32-Bit Standard");
    }

    [RelayCommand]
    public void DownloadGlobal64()
    {
        OpenDownloadUrl("https://dlied6.myapp.com/myapp/kspc/installer/GLP_installer_900206197_market.exe", "GameLoop Global 64-Bit Beta");
    }

    [RelayCommand]
    public void DownloadChina32()
    {
        OpenDownloadUrl("https://dldir1.qq.com/syzs/TGB/GameDownload_com.tencent.tgb_100103_3.2.368.100.exe", "GameLoop Chinese 32-Bit (QQ)");
    }

    [RelayCommand]
    public void DownloadChina64()
    {
        OpenDownloadUrl("https://dldir1.qq.com/syzs/TGB/64/GameDownload_com.tencent.tgb_100103_64.exe", "GameLoop Chinese 64-Bit Beta (QQ)");
    }

    [RelayCommand]
    public void DownloadOfficial64()
    {
        OpenDownloadUrl("https://www.gameloop.com/", "GameLoop Official 64-Bit Latest Original");
    }

    private void OpenDownloadUrl(string url, string title)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            StatusMessage = $"Download started: {title}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open download: {ex.Message}";
        }
    }

    public RegistryOptimizationViewModel(
        IGameLoopOptimizerService gameLoopOptimizer,
        IGameLoopDetectionService gameLoopDetection,
        IRegistryService registry,
        IBackupService backup,
        ILogService log,
        IAdminService admin)
    {
        _gameLoopOptimizer = gameLoopOptimizer;
        _gameLoopDetection = gameLoopDetection;
        _registry = registry;
        _backup = backup;
        _log = log;
        _admin = admin;

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            IsGameLoopInstalled = await _gameLoopDetection.IsInstalledAsync();
            IsUninstallerFirst = IsGameLoopInstalled;
            SelectedSlideIndex = 0;
            IsGameLoopRunning = Process.GetProcessesByName("AndroidEmulator").Length > 0 ||
                                Process.GetProcessesByName("AppMarket").Length > 0;

            Is120FpsActive = await _gameLoopOptimizer.Is120FpsUnlockedAsync();
            IsCoreIsolationDisabled = !(await _gameLoopOptimizer.IsCoreIsolationHvciEnabledAsync());
            IsAppMarketAutoCloseActive = await _gameLoopOptimizer.IsAppMarketAutoCloseEnabledAsync();

            // CPU FrameBuffer state
            var cfVal = _registry.GetValue(@"HKEY_CURRENT_USER\Software\Tencent\MobileGamePC", "UseCPUFrameBuffer");
            IsCpuFrameBufferDisabled = cfVal != null && Convert.ToInt32(cfVal) == 0;

            // Render Engine
            int curEngine = await _gameLoopOptimizer.GetCurrentRenderEngineAsync();
            SelectedRenderEngine = curEngine;
            RenderEngineName = curEngine switch
            {
                1 => "DirectX+",
                2 => "OpenGL+",
                _ => "Auto Smart"
            };

            // Resolution
            var (w, h, _) = await _gameLoopOptimizer.GetCurrentResolutionAsync();
            if (w == 1440 && h == 1080) SelectedResolutionPreset = "iPad";
            else if (w == 2560 && h == 1440) SelectedResolutionPreset = "2K";
            else SelectedResolutionPreset = "1080p";

            StatusMessage = IsGameLoopInstalled ? "GameLoop detected & ready." : "GameLoop not detected in default directories.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Status check: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task Toggle120FpsAsync()
    {
        IsLoading = true;
        try
        {
            bool targetState = !Is120FpsActive;
            var res = await _gameLoopOptimizer.Unlock120FpsAsync(targetState);
            Is120FpsActive = targetState;
            StatusMessage = res.Success ? (targetState ? "120 FPS unlocked successfully!" : "120 FPS reverted to default.") : res.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"120 FPS error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ApplyProfileAsync(string profile)
    {
        IsLoading = true;
        try
        {
            SelectedProfile = profile;
            if (profile == "ROG5")
            {
                var res = await _gameLoopOptimizer.ApplyDeviceProfileAsync("ASUS_I005DA", "asus", "ROG Phone 5", 120);
                StatusMessage = res.Success ? "ASUS ROG Phone 5 profile applied (120 FPS unlocked)." : res.Message;
            }
            else if (profile == "iPad")
            {
                SelectedResolutionPreset = "iPad";
                var res = await _gameLoopOptimizer.ApplyResolutionPresetAsync(1440, 1080, 320, "iPad View (4:3)");
                StatusMessage = res.Success ? "iPad Pro View (1440x1080 4:3) applied." : res.Message;
            }
            else if (profile == "MaxHardware")
            {
                var res = await _gameLoopOptimizer.ApplyHardwareAllocationAsync(8, 8192);
                StatusMessage = res.Success ? "Allocated 8 CPU cores & 8192 MB RAM." : res.Message;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Profile error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ApplyResolutionAsync(string preset)
    {
        IsLoading = true;
        try
        {
            SelectedResolutionPreset = preset;
            OptimizationResult res;
            if (preset == "1080p")
            {
                res = await _gameLoopOptimizer.ApplyResolutionPresetAsync(1920, 1080, 320, "1080p (16:9)");
            }
            else if (preset == "2K")
            {
                res = await _gameLoopOptimizer.ApplyResolutionPresetAsync(2560, 1440, 480, "2K Ultra HD");
            }
            else
            {
                SelectedProfile = "iPad";
                res = await _gameLoopOptimizer.ApplyResolutionPresetAsync(1440, 1080, 320, "iPad View (4:3)");
            }

            StatusMessage = res.Success ? $"Resolution preset '{preset}' applied successfully." : res.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Resolution error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CycleRenderEngineAsync()
    {
        IsLoading = true;
        try
        {
            int next = (SelectedRenderEngine + 1) % 3;
            SelectedRenderEngine = next;
            RenderEngineName = next switch
            {
                1 => "DirectX+",
                2 => "OpenGL+",
                _ => "Auto Smart"
            };

            var res = await _gameLoopOptimizer.ApplyRenderEngineAsync(next, RenderEngineName);
            StatusMessage = res.Success ? $"Render engine set to {RenderEngineName}." : res.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Engine switch error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ToggleCoreIsolationAsync()
    {
        IsLoading = true;
        try
        {
            bool targetDisable = !IsCoreIsolationDisabled;
            var res = await _gameLoopOptimizer.SetCoreIsolationHvciAsync(!targetDisable);
            IsCoreIsolationDisabled = targetDisable;
            StatusMessage = res.Success ? (targetDisable ? "Core Isolation (HVCI) disabled for max virtualization." : "Core Isolation enabled.") : res.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"HVCI error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ToggleAppMarketAutoCloseAsync()
    {
        IsLoading = true;
        try
        {
            bool target = !IsAppMarketAutoCloseActive;
            var res = await _gameLoopOptimizer.SetAppMarketAutoCloseAsync(target);
            IsAppMarketAutoCloseActive = target;
            StatusMessage = res.Success ? (target ? "AppMarket auto-killer active (saves ~850MB RAM)." : "AppMarket auto-killer deactivated.") : res.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"AppMarket killer error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ToggleCpuFrameBufferAsync()
    {
        IsLoading = true;
        try
        {
            bool target = !IsCpuFrameBufferDisabled;
            uint val = target ? 0u : 1u;
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Tencent\MobileGamePC", "UseCPUFrameBuffer", val, RegistryValueKind.DWord);
            try { _registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Tencent\MobileGamePC", "UseCPUFrameBuffer", val, RegistryValueKind.DWord); } catch { }
            IsCpuFrameBufferDisabled = target;
            StatusMessage = target ? "GameLoop CPU FrameBuffer disabled (micro-stutters fixed)." : "CPU FrameBuffer set to default.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"FrameBuffer error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task PurgeShaderCacheAsync()
    {
        IsLoading = true;
        try
        {
            var res = await _gameLoopOptimizer.CleanShaderAndTempCacheAsync();
            StatusMessage = res.Success ? "GameLoop DX/GL shader and temp caches purged." : res.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cache clean error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task LaunchGameLoopAsync()
    {
        IsLoading = true;
        try
        {
            StatusMessage = "Launching GameLoop with 120 FPS parameters...";
            var res = await _gameLoopOptimizer.LaunchGameLoop120FpsAsync();
            StatusMessage = res.Success ? "GameLoop launched successfully!" : res.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Launch error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SafeCloseGameLoopAsync()
    {
        IsLoading = true;
        try
        {
            var res = await _gameLoopOptimizer.SafeCloseGameLoopAsync();
            IsGameLoopRunning = false;
            StatusMessage = res.Success ? "All GameLoop processes safely terminated." : res.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Close error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task UninstallGameLoopAsync()
    {
        IsLoading = true;
        try
        {
            UninstallStatusText = "Searching for official GameLoop uninstaller (TUninstall.exe)...";
            var res = await _gameLoopOptimizer.LaunchOfficialUninstallAsync();
            UninstallStatusText = res.Message;
            StatusMessage = res.Message;
        }
        catch (Exception ex)
        {
            UninstallStatusText = $"Uninstaller error: {ex.Message}";
            StatusMessage = UninstallStatusText;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task NuclearCleanAsync()
    {
        IsCleaning = true;
        IsLoading = true;
        try
        {
            CleanerStatusText = "Executing 7-step nuclear wipe across all drives (terminating processes, stopping services, wiping registry & cache)...";
            var res = await _gameLoopOptimizer.FullNuclearCleanGameLoopAsync();
            CleanerStatusText = res.Message;
            StatusMessage = res.Message;
            IsGameLoopRunning = false;
        }
        catch (Exception ex)
        {
            CleanerStatusText = $"Nuclear clean error: {ex.Message}";
            StatusMessage = CleanerStatusText;
        }
        finally
        {
            IsCleaning = false;
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task KillTelemetryAsync()
    {
        IsLoading = true;
        try
        {
            CleanerStatusText = "Terminating background telemetry daemons...";
            var cleaner = new GameLoopCleanerService();
            int killed = await Task.Run(() => cleaner.KillTelemetryAndDaemons());
            CleanerStatusText = killed > 0
                ? $"Terminated {killed} GameLoop telemetry processes."
                : "No active telemetry daemons found.";
            StatusMessage = CleanerStatusText;
        }
        catch (Exception ex)
        {
            CleanerStatusText = $"Telemetry kill error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
