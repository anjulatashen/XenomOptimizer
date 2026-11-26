using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XenomOptimizer.Models;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

/// <summary>
/// ViewModel for the GameLoop Optimization page.
/// Detects GameLoop installation, displays configuration, and manages GameLoop-specific registry edits.
/// </summary>
public partial class GameLoopOptimizationViewModel : ObservableObject
{
    private readonly IGameLoopDetectionService _gameLoop;
    private readonly IGameLoopOptimizerService _optimizer;
    private readonly IAdminService _admin;
    private readonly IBackupService _backup;
    private readonly ILogService _log;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _isInstalled;
    [ObservableProperty] private string _installPath = "Detecting...";
    [ObservableProperty] private string _version = "Detecting...";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isOneTapBusy;
    [ObservableProperty] private string _oneTapStatus = "";

    // ── Tab Navigation ────────────────────────────────────────────────────────
    // 0: Graphics & 120 FPS, 1: Engine & Cores, 2: Device Spoofing, 3: PC Doctor, 4: Storage & Safe Close
    [ObservableProperty] private int _selectedSubTab = 0;

    // ── 120 FPS Safe Unlock properties ────────────────────────────────────────
    [ObservableProperty] private bool _is120FpsUnlocked;
    [ObservableProperty] private string _fps120StatusText = "90 FPS (Stock Capped)";
    [ObservableProperty] private string _fps120DetailText = "GameLoop is currently capped at 90 FPS. Click 'Unlock 120 FPS Mode' to apply safe certified 120Hz device spoofing.";
    [ObservableProperty] private string _fps120ActionMessage = "";
    [ObservableProperty] private bool _isApplying120Fps;

    // ── Graphics & Resolution Presets ─────────────────────────────────────────
    [ObservableProperty] private int _selectedResolutionIndex = 0;
    [ObservableProperty] private string _resolutionStatusMessage = "";
    [ObservableProperty] private string _currentResolutionDisplay = "1920x1080 (240 DPI)";

    // ── Engine & Hardware Allocation ──────────────────────────────────────────
    [ObservableProperty] private int _selectedEngineIndex = 0; // 0=DirectX+, 1=OpenGL+, 2=Vulkan, 3=DirectX Lite
    [ObservableProperty] private int _selectedCoresIndex = 0;  // 0=Auto, 1=2, 2=4, 3=6, 4=8 Cores
    [ObservableProperty] private int _selectedRamIndex = 0;    // 0=Auto, 1=2GB, 2=4GB, 3=8GB, 4=12GB
    [ObservableProperty] private string _engineHardwareStatus = "";

    // ── Device Spoofing ───────────────────────────────────────────────────────
    [ObservableProperty] private int _selectedDeviceIndex = 0;
    [ObservableProperty] private string _currentDeviceModelText = "Detecting...";
    [ObservableProperty] private string _deviceSpoofStatus = "";

    // ── PC Doctor & Storage ───────────────────────────────────────────────────
    [ObservableProperty] private bool _isCoreIsolationHvciDisabled;
    [ObservableProperty] private bool _isAppMarketAutoCloseActive;
    [ObservableProperty] private string _doctorStatusMessage = "";
    [ObservableProperty] private string _storageStatusMessage = "";
    [ObservableProperty] private string _cleanerStatusMessage = "";
    [ObservableProperty] private bool _isCleaning;

    public ObservableCollection<OptimizationCardViewModel> Optimizations { get; } = new();
    public ObservableCollection<GameLoopConfigItem> ConfigItems { get; } = new();
    public ObservableCollection<GameLoopRegistryTweakViewModel> RegistryEdits { get; } = new();

    public GameLoopOptimizationViewModel(
        IGameLoopDetectionService gameLoop,
        IGameLoopOptimizerService optimizer,
        IAdminService admin,
        IBackupService backup,
        ILogService log)
    {
        _gameLoop = gameLoop;
        _optimizer = optimizer;
        _admin = admin;
        _backup = backup;
        _log = log;

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            IsInstalled = await _gameLoop.IsInstalledAsync();
            if (IsInstalled)
            {
                InstallPath = await _gameLoop.GetInstallPathAsync() ?? "Unknown";
                Version = await _gameLoop.GetVersionAsync() ?? "Unknown";

                // Load current configuration
                var config = await _gameLoop.GetGameLoopConfigAsync();
                foreach (var kvp in config)
                {
                    ConfigItems.Add(new GameLoopConfigItem
                    {
                        Key = kvp.Key,
                        Value = kvp.Value
                    });
                }
            }
            else
            {
                InstallPath = "Not Installed";
                Version = "N/A";
                StatusMessage = "GameLoop is not installed on this system. GameLoop-specific optimizations are unavailable.";
            }

            // Load GameLoop Registry Edits
            await LoadRegistryEditsAsync();

            // Load 120 FPS Unlock status
            Is120FpsUnlocked = await _optimizer.Is120FpsUnlockedAsync();
            Update120FpsTexts();

            // Load GameLoop Pro Suite settings
            var res = await _optimizer.GetCurrentResolutionAsync();
            CurrentResolutionDisplay = $"{res.Width}x{res.Height} ({res.Dpi} DPI)";
            if (res.Width == 1920 && res.Height == 1080) SelectedResolutionIndex = 0;
            else if (res.Width == 2560 && res.Height == 1440) SelectedResolutionIndex = 1;
            else if (res.Width == 3840 && res.Height == 2160) SelectedResolutionIndex = 2;
            else if (res.Width == 1440 && res.Height == 1080) SelectedResolutionIndex = 3;
            else if (res.Width == 2300 && res.Height == 1920) SelectedResolutionIndex = 4;
            else if (res.Width == 1280 && res.Height == 720) SelectedResolutionIndex = 5;

            int currentEngine = await _optimizer.GetCurrentRenderEngineAsync();
            SelectedEngineIndex = currentEngine switch
            {
                2 => 1, // OpenGL+
                1 => 0, // DirectX+
                4 => 2, // Vulkan
                _ => 1  // Default to OpenGL+
            };

            var hw = await _optimizer.GetCurrentHardwareAllocationAsync();
            SelectedCoresIndex = hw.Cores switch { 2 => 1, 4 => 2, 6 => 3, 8 => 4, _ => 0 };
            SelectedRamIndex = hw.RamMb switch { 2048 => 1, 4096 => 2, 8192 => 3, 12288 => 4, _ => 0 };

            CurrentDeviceModelText = await _optimizer.GetCurrentDeviceModelAsync();
            IsCoreIsolationHvciDisabled = !(await _optimizer.IsCoreIsolationHvciEnabledAsync());
            IsAppMarketAutoCloseActive = await _optimizer.IsAppMarketAutoCloseEnabledAsync();

        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading GameLoop details: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadRegistryEditsAsync()
    {
        RegistryEdits.Clear();

        var tweak1 = new GameLoopRegistryTweakViewModel
        {
            Id = "gl_high_priority",
            Name = "Emulator High Priority (CPU + GPU)",
            Description = "Pins GameLoop engine processes (AndroidProcess.exe, aow_exe.exe, AppMarket.exe) to High CPU & GPU Priority via Windows IFEO PerfOptions.",
            RegistryTarget = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\<Process>\PerfOptions",
            RequiresAdmin = true,
            IsAdmin = _admin.IsRunningAsAdmin,
            ApplyAction = enable => _optimizer.SetEmulatorHighPriorityAsync(enable)
        };
        tweak1.IsEnabled = await _optimizer.IsEmulatorHighPriorityEnabledAsync();
        RegistryEdits.Add(tweak1);

        var tweak2 = new GameLoopRegistryTweakViewModel
        {
            Id = "gl_force_gpu_vrr",
            Name = "Force Dedicated GPU + VRR/Swap Upgrade",
            Description = "Registers GameLoop executables in DirectX UserGpuPreferences to force the Dedicated GPU (High-Performance) and enables variable refresh rate (VRR) / DXGI swap-chain optimizations.",
            RegistryTarget = @"HKCU\Software\Microsoft\DirectX\UserGpuPreferences",
            RequiresAdmin = false,
            IsAdmin = _admin.IsRunningAsAdmin,
            ApplyAction = enable => _optimizer.ForceDedicatedGpuAndVrrAsync(enable)
        };
        tweak2.IsEnabled = await _optimizer.IsDedicatedGpuForcedAsync();
        RegistryEdits.Add(tweak2);

        var tweak3 = new GameLoopRegistryTweakViewModel
        {
            Id = "gl_vtx_hyperv",
            Name = "Prefer VT-x over Hyper-V & Stop AppMarket Auto-Start",
            Description = "Stops AppMarket / TxGameAssistant background auto-start on Windows boot and tunes the emulator engine to prefer native VT-x hardware virtualization.",
            RegistryTarget = @"HKCU & HKLM Run Keys + HKLM\SOFTWARE\WOW6432Node\Tencent\MobileGamePC",
            RequiresAdmin = true,
            IsAdmin = _admin.IsRunningAsAdmin,
            ApplyAction = enable => _optimizer.PreferVtxOverHyperVAsync(enable)
        };
        tweak3.IsEnabled = await _optimizer.IsVtxPreferredAsync();
        RegistryEdits.Add(tweak3);

        var tweak4 = new GameLoopRegistryTweakViewModel
        {
            Id = "gl_engine_render_boost",
            Name = "Engine Render Boost (DirectX+ & No VSync)",
            Description = "Enables DirectX+ render path, unlocks frame rates by disabling VSync, enables persistent shader cache, and turns on render thread boost.",
            RegistryTarget = @"HKCU\Software\Tencent\MobileGamePC & config.ini / vm_configuration.ini",
            RequiresAdmin = false,
            IsAdmin = _admin.IsRunningAsAdmin,
            ApplyAction = enable => _optimizer.ApplyEngineRenderBoostAsync(enable)
        };
        tweak4.IsEnabled = await _optimizer.IsEngineRenderBoostEnabledAsync();
        RegistryEdits.Add(tweak4);
    }

    [RelayCommand]
    private async Task ApplyAllRegistryEditsAsync()
    {
        if (IsOneTapBusy) return;
        IsOneTapBusy = true;
        OneTapStatus = "Applying GameLoop Registry Edits...";

        try
        {
            int appliedCount = 0;
            foreach (var tweak in RegistryEdits)
            {
                if (tweak.RequiresAdmin && !_admin.IsRunningAsAdmin)
                    continue;

                await tweak.ToggleCommand.ExecuteAsync(true);
                appliedCount++;
            }

            OneTapStatus = $"Successfully applied {appliedCount} GameLoop registry edits!";
        }
        catch (Exception ex)
        {
            OneTapStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsOneTapBusy = false;
        }
    }

    private void Update120FpsTexts()
    {
        if (Is120FpsUnlocked)
        {
            Fps120StatusText = "120 FPS Ultra (Active ✓)";
            Fps120DetailText = "Samsung SM-X920 (Galaxy Tab S10+ 120Hz) device profile active. In PUBG Mobile: Settings > Audio & Graphics, select 'Ultra Extreme (120 FPS)' or 'HDR 90 FPS'.";
        }
        else
        {
            Fps120StatusText = "90 FPS (Stock Capped)";
            Fps120DetailText = "GameLoop is currently capped at 90 FPS. Click 'Unlock 120 FPS Mode' to apply safe certified 120Hz device spoofing.";
        }
    }

    [RelayCommand]
    private async Task Toggle120FpsAsync()
    {
        if (IsApplying120Fps) return;
        IsApplying120Fps = true;
        Fps120ActionMessage = "Configuring 120 FPS safe unlock...";
        try
        {
            bool targetState = !Is120FpsUnlocked;
            var res = await _optimizer.Unlock120FpsAsync(targetState);
            if (res.Success)
            {
                Is120FpsUnlocked = targetState;
                Update120FpsTexts();
                Fps120ActionMessage = res.Message;
            }
            else
            {
                Fps120ActionMessage = $"Failed: {res.Message}";
            }
        }
        catch (Exception ex)
        {
            Fps120ActionMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsApplying120Fps = false;
        }
    }

    [RelayCommand]
    private async Task Create120FpsShortcutAsync()
    {
        Fps120ActionMessage = "Creating desktop shortcut...";
        try
        {
            var res = await _optimizer.Create120FpsShortcutAsync();
            Fps120ActionMessage = res.Message;
        }
        catch (Exception ex)
        {
            Fps120ActionMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LaunchGameLoop120FpsAsync()
    {
        Fps120ActionMessage = "Launching GameLoop with 120 FPS engine boost...";
        try
        {
            var res = await _optimizer.LaunchGameLoop120FpsAsync();
            Fps120ActionMessage = res.Message;
        }
        catch (Exception ex)
        {
            Fps120ActionMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SwitchTab(object? parameter)
    {
        if (parameter is int i) SelectedSubTab = i;
        else if (parameter != null && int.TryParse(parameter.ToString(), out int parsed)) SelectedSubTab = parsed;
    }

    [RelayCommand]
    private async Task ApplyResolutionPresetAsync(object? parameter)
    {
        int index = 0;
        if (parameter is int i) index = i;
        else if (parameter != null && int.TryParse(parameter.ToString(), out int parsed)) index = parsed;

        SelectedResolutionIndex = index;
        var (w, h, dpi, name) = index switch
        {
            0 => (1920, 1080, 240, "1080p FHD"),
            1 => (2560, 1440, 320, "2K QHD"),
            2 => (3840, 2160, 480, "4K UHD"),
            3 => (1440, 1080, 240, "iPad View 4:3"),
            4 => (2300, 1920, 320, "iPad Pro 4:3"),
            5 => (1280, 720, 160, "720p Fast"),
            _ => (1920, 1080, 240, "1080p FHD")
        };

        ResolutionStatusMessage = $"Applying {name}...";
        var res = await _optimizer.ApplyResolutionPresetAsync(w, h, dpi, name);
        ResolutionStatusMessage = res.Message;
        CurrentResolutionDisplay = $"{w}x{h} ({dpi} DPI)";
    }

    [RelayCommand]
    private async Task ApplyEngineSettingsAsync()
    {
        int engineCode = SelectedEngineIndex switch
        {
            1 => 2, // OpenGL+
            0 => 1, // DirectX+
            2 => 4, // Vulkan
            _ => 2  // Default OpenGL+
        };

        string engineName = SelectedEngineIndex switch
        {
            1 => "OpenGL+ (Recommended • Zero Black Screen)",
            0 => "DirectX+",
            2 => "Vulkan",
            _ => "OpenGL+"
        };

        int cores = SelectedCoresIndex switch
        {
            1 => 2,
            2 => 4,
            3 => 6,
            4 => 8,
            _ => 0 // Auto
        };

        int ram = SelectedRamIndex switch
        {
            1 => 2048,
            2 => 4096,
            3 => 8192,
            4 => 12288,
            _ => 0 // Auto
        };

        EngineHardwareStatus = "Applying engine & hardware parameters...";
        var engineRes = await _optimizer.ApplyRenderEngineAsync(engineCode, engineName);
        var hwRes = await _optimizer.ApplyHardwareAllocationAsync(cores, ram);
        EngineHardwareStatus = $"{engineRes.Message} | {hwRes.Message}";
    }

    [RelayCommand]
    private async Task ApplyDeviceProfileAsync(object? parameter)
    {
        int index = 0;
        if (parameter is int i) index = i;
        else if (parameter != null && int.TryParse(parameter.ToString(), out int parsed)) index = parsed;

        SelectedDeviceIndex = index;
        var (model, manufacturer, phoneName, fps) = index switch
        {
            0 => ("ASUS_AI2401_A", "asus", "ASUS ROG Phone 8 Pro", 120),
            1 => ("SM-X920", "samsung", "Samsung Galaxy Tab S10+ (SM-X920)", 120),
            2 => ("ASUS_I005D", "asus", "ASUS ROG Phone 5", 90),
            3 => ("2210132C", "xiaomi", "Xiaomi 13 Pro", 120),
            _ => ("SM-X920", "samsung", "Samsung Galaxy Tab S10+ (SM-X920)", 120)
        };

        DeviceSpoofStatus = $"Applying {phoneName}...";
        var res = await _optimizer.ApplyDeviceProfileAsync(model, manufacturer, phoneName, fps);
        DeviceSpoofStatus = res.Message;
        CurrentDeviceModelText = phoneName;
        Is120FpsUnlocked = (fps == 120);
        Update120FpsTexts();
    }

    [RelayCommand]
    private async Task ToggleCoreIsolationHvciAsync()
    {
        if (!_admin.IsRunningAsAdmin)
        {
            DoctorStatusMessage = "Administrator privileges required to change Core Isolation (HVCI).";
            return;
        }

        bool targetState = !IsCoreIsolationHvciDisabled;
        DoctorStatusMessage = "Configuring Core Isolation (HVCI)...";
        // If targetState is true, we want it disabled for gaming
        var res = await _optimizer.SetCoreIsolationHvciAsync(!targetState);
        DoctorStatusMessage = res.Message;
        if (res.Success)
        {
            IsCoreIsolationHvciDisabled = targetState;
        }
    }

    [RelayCommand]
    private async Task ToggleAppMarketAutoCloseAsync()
    {
        bool targetState = !IsAppMarketAutoCloseActive;
        var res = await _optimizer.SetAppMarketAutoCloseAsync(targetState);
        DoctorStatusMessage = res.Message;
        if (res.Success)
        {
            IsAppMarketAutoCloseActive = targetState;
        }
    }

    [RelayCommand]
    private async Task SafeCloseGameLoopActionAsync()
    {
        StorageStatusMessage = "Closing GameLoop safely and flushing write caches...";
        var res = await _optimizer.SafeCloseGameLoopAsync();
        StorageStatusMessage = res.Message;
    }

    [RelayCommand]
    private async Task CleanShaderCacheActionAsync()
    {
        StorageStatusMessage = "Purging shader cache and temp logs...";
        var res = await _optimizer.CleanShaderAndTempCacheAsync();
        StorageStatusMessage = res.Message;
    }

    [RelayCommand]
    private async Task UninstallGameLoopAsync()
    {
        CleanerStatusMessage = "Detecting and launching GameLoop uninstaller...";
        var res = await _optimizer.LaunchOfficialUninstallAsync();
        CleanerStatusMessage = res.Message;
        StorageStatusMessage = res.Message;
        StatusMessage = res.Message;
    }


    [RelayCommand]
    private async Task NuclearCleanGameLoopAsync()
    {
        IsCleaning = true;
        CleanerStatusMessage = "Executing 7-step nuclear wipe across all drives (terminating processes, stopping services, wiping registry & cache)...";
        var res = await _optimizer.FullNuclearCleanGameLoopAsync();
        CleanerStatusMessage = res.Message;
        StorageStatusMessage = res.Message;
        StatusMessage = res.Message;
        IsCleaning = false;
    }
}

public class GameLoopConfigItem
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>
/// ViewModel for an individual GameLoop Registry Edit item.
/// </summary>
public partial class GameLoopRegistryTweakViewModel : ObservableObject
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string RegistryTarget { get; set; } = "";
    public bool RequiresAdmin { get; set; }
    public bool IsAdmin { get; set; }

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "";

    public Func<bool, Task<OptimizationResult>>? ApplyAction { get; set; }

    [RelayCommand]
    public async Task ToggleAsync(object? parameter = null)
    {
        if (ApplyAction == null || IsBusy) return;

        bool targetState = parameter is bool b ? b : !IsEnabled;

        if (RequiresAdmin && !IsAdmin)
        {
            StatusMessage = "Administrator privileges required.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Applying...";
        try
        {
            var result = await ApplyAction(targetState);
            if (result.Success)
            {
                IsEnabled = targetState;
                StatusMessage = targetState ? "Active ✓" : "Disabled";
            }
            else
            {
                StatusMessage = $"Failed: {result.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
