using System.Collections.ObjectModel;
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

    // 120 FPS Safe Unlock properties
    [ObservableProperty] private bool _is120FpsUnlocked;
    [ObservableProperty] private string _fps120StatusText = "90 FPS (Stock Capped)";
    [ObservableProperty] private string _fps120DetailText = "GameLoop is currently capped at 90 FPS. Click 'Unlock 120 FPS Mode' to apply safe certified 120Hz device spoofing.";
    [ObservableProperty] private string _fps120ActionMessage = "";
    [ObservableProperty] private bool _isApplying120Fps;

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
            Fps120DetailText = "Samsung SM-X910 (Galaxy Tab S9 120Hz) device profile active. In PUBG Mobile: Settings > Audio & Graphics, select 'Ultra Extreme (120 FPS)'.";
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
