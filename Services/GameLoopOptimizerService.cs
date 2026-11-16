using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using XenomOptimizer.Models;

namespace XenomOptimizer.Services;

/// <summary>
/// Service implementing modular backend optimizations for GameLoop Emulator and Windows gaming performance.
/// Includes error handling, admin checks, backups, and rollback capabilities.
/// </summary>
public class GameLoopOptimizerService : IGameLoopOptimizerService
{
    private readonly IRegistryService _registry;
    private readonly IAdminService _admin;
    private readonly ILogService _log;
    private readonly IBackupService _backup;
    private readonly IGameLoopDetectionService _gameLoopDetection;

    // GameLoop processes to optimize
    private static readonly string[] EmulatorProcesses =
    {
        "AndroidProcess.exe",
        "AppMarket.exe",
        "aow_exe.exe",
        "QMEmulatorService.exe",
        "TBSGameLive.exe"
    };

    public GameLoopOptimizerService(
        IRegistryService registry,
        IAdminService admin,
        ILogService log,
        IBackupService backup,
        IGameLoopDetectionService gameLoopDetection)
    {
        _registry = registry;
        _admin = admin;
        _log = log;
        _backup = backup;
        _gameLoopDetection = gameLoopDetection;
    }

    #region 1. Emulator High Priority (CPU + GPU)

    public async Task<OptimizationResult> SetEmulatorHighPriorityAsync(bool enable)
    {
        if (!_admin.IsRunningAsAdmin)
        {
            _log.Log("GameLoop", "SetEmulatorHighPriority failed: Admin required", "ERROR");
            return OptimizationResult.Failed("Administrator privileges required to modify process execution priority.");
        }

        try
        {
            await _backup.StartSessionAsync();
            int modifiedCount = 0;

            foreach (var exe in EmulatorProcesses)
            {
                var keyPath = $@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{exe}\PerfOptions";

                await _backup.ExportRegistryKeyAsync(keyPath);

                if (enable)
                {
                    // CpuPriorityClass: 3 = High, GpuPriority: 8 = High, IoPriority: 3 = High
                    _registry.SetValue(keyPath, "CpuPriorityClass", 3u, RegistryValueKind.DWord);
                    _registry.SetValue(keyPath, "GpuPriority", 8u, RegistryValueKind.DWord);
                    _registry.SetValue(keyPath, "IoPriority", 3u, RegistryValueKind.DWord);
                }
                else
                {
                    // Revert to Normal: CpuPriorityClass: 2 = Normal, GpuPriority: 0, IoPriority: 2
                    _registry.SetValue(keyPath, "CpuPriorityClass", 2u, RegistryValueKind.DWord);
                    _registry.SetValue(keyPath, "GpuPriority", 0u, RegistryValueKind.DWord);
                    _registry.SetValue(keyPath, "IoPriority", 2u, RegistryValueKind.DWord);
                }

                modifiedCount++;
            }

            // Also adjust priority dynamically if any emulator processes are currently running
            var procNames = EmulatorProcesses.Select(Path.GetFileNameWithoutExtension).ToArray();
            foreach (var name in procNames)
            {
                try
                {
                    var procs = Process.GetProcessesByName(name);
                    foreach (var proc in procs)
                    {
                        try
                        {
                            proc.PriorityClass = enable ? ProcessPriorityClass.High : ProcessPriorityClass.Normal;
                        }
                        catch { }
                        finally
                        {
                            proc.Dispose();
                        }
                    }
                }
                catch { }
            }

            await _backup.EndSessionAsync();
            var action = enable ? "applied (High Priority)" : "reverted (Normal Priority)";
            _log.Log("GameLoop", $"Emulator High Priority {action} for {modifiedCount} processes", "SUCCESS");
            return OptimizationResult.Succeeded($"Emulator CPU & GPU High Priority {action} successfully.");
        }
        catch (Exception ex)
        {
            _log.Log("GameLoop", "SetEmulatorHighPriority exception", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed($"Failed to configure Emulator Priority: {ex.Message}");
        }
    }

    public Task<bool> IsEmulatorHighPriorityEnabledAsync()
    {
        return Task.Run(() =>
        {
            var keyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\AndroidProcess.exe\PerfOptions";
            var cpuVal = _registry.GetValue(keyPath, "CpuPriorityClass");
            var gpuVal = _registry.GetValue(keyPath, "GpuPriority");

            return cpuVal != null && cpuVal.ToString() == "3" &&
                   gpuVal != null && gpuVal.ToString() == "8";
        });
    }

    #endregion

    #region 2. Force Dedicated GPU + VRR/Swap Upgrade

    public async Task<OptimizationResult> ForceDedicatedGpuAndVrrAsync(bool enable)
    {
        try
        {
            var installPath = await _gameLoopDetection.GetInstallPathAsync();
            var targetExePaths = await LocateEmulatorExecutablesAsync(installPath);

            const string userGpuPrefsKey = @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences";
            await _backup.ExportRegistryKeyAsync(userGpuPrefsKey);

            int configuredCount = 0;
            // GpuPreference=2: Dedicated High-Performance GPU, AutoSwapchainEnabled=1: Upgrade swap-chain & VRR
            var prefValue = enable ? "GpuPreference=2;AutoSwapchainEnabled=1;" : "GpuPreference=0;";

            foreach (var exePath in targetExePaths)
            {
                if (File.Exists(exePath))
                {
                    _registry.SetValue(userGpuPrefsKey, exePath, prefValue, RegistryValueKind.String);
                    configuredCount++;
                }
            }

            // Also configure DXGI Swap & Full-Screen VRR optimizations
            const string gameConfigStore = @"HKEY_CURRENT_USER\System\GameConfigStore";
            _registry.SetValue(gameConfigStore, "GameDVR_DXGIHonorPlayEnhancedMode", enable ? 0u : 1u, RegistryValueKind.DWord);

            var action = enable ? "pinned to Dedicated GPU & Swapchain/VRR enabled" : "reset to system default GPU";
            _log.Log("GameLoop", $"Force Dedicated GPU {action} for {configuredCount} executables", "SUCCESS");
            return OptimizationResult.Succeeded($"Dedicated GPU and VRR swap-chain optimizations {action}.");
        }
        catch (Exception ex)
        {
            _log.Log("GameLoop", "ForceDedicatedGpuAndVrr exception", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed($"Failed to configure Dedicated GPU & VRR: {ex.Message}");
        }
    }

    public async Task<bool> IsDedicatedGpuForcedAsync()
    {
        var installPath = await _gameLoopDetection.GetInstallPathAsync();
        var exes = await LocateEmulatorExecutablesAsync(installPath);
        if (exes.Count == 0) return false;

        const string userGpuPrefsKey = @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences";
        var firstExe = exes.FirstOrDefault(File.Exists);
        if (firstExe == null) return false;

        var val = _registry.GetValue(userGpuPrefsKey, firstExe)?.ToString();
        return val != null && val.Contains("GpuPreference=2", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region 3. Prefer VT-x over Hyper-V & Stop AppMarket Auto-Start

    public async Task<OptimizationResult> PreferVtxOverHyperVAsync(bool enable)
    {
        try
        {
            // 1. Stop AppMarket and TxGameAssistant auto-start entries in Run keys
            string[] runKeys =
            {
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run",
                @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Run"
            };

            string[] autoStartValueNames = { "AppMarket", "TxGameAssistant", "GameLoop", "TencentGameAssistant" };

            if (enable)
            {
                foreach (var runKey in runKeys)
                {
                    foreach (var valName in autoStartValueNames)
                    {
                        if (_registry.ValueExists(runKey, valName))
                        {
                            _registry.DeleteValue(runKey, valName);
                        }
                    }
                }
            }

            // 2. Set GameLoop engine virtualization flags to prefer VT-x
            string[] tencentKeys =
            {
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Tencent\MobileGamePC",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Tencent\MobileGamePC",
                @"HKEY_CURRENT_USER\Software\Tencent\MobileGamePC"
            };

            foreach (var key in tencentKeys)
            {
                if (enable)
                {
                    _registry.SetValue(key, "PreferVTx", 1u, RegistryValueKind.DWord);
                    _registry.SetValue(key, "DisableHyperVFallback", 1u, RegistryValueKind.DWord);
                }
                else
                {
                    _registry.DeleteValue(key, "PreferVTx");
                    _registry.DeleteValue(key, "DisableHyperVFallback");
                }
            }

            // 3. Update vm_configuration.ini if accessible
            var installPath = await _gameLoopDetection.GetInstallPathAsync();
            if (installPath != null)
            {
                var vmIni = Path.Combine(installPath, "TxGameAssistant", "UI", "vm_configuration.ini");
                if (File.Exists(vmIni))
                {
                    await _backup.BackupFileAsync(vmIni);
                    UpdateIniKey(vmIni, "VM", "hyperv", enable ? "0" : "1");
                    UpdateIniKey(vmIni, "VM", "vtx_preferred", enable ? "1" : "0");
                }
            }

            var status = enable ? "Prefer VT-x enabled and AppMarket auto-start disabled" : "Virtualization settings restored";
            _log.Log("GameLoop", status, "SUCCESS");
            return OptimizationResult.Succeeded(status);
        }
        catch (Exception ex)
        {
            _log.Log("GameLoop", "PreferVtxOverHyperV exception", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed($"Failed to configure VT-x & startup: {ex.Message}");
        }
    }

    public Task<bool> IsVtxPreferredAsync()
    {
        return Task.Run(() =>
        {
            const string key = @"HKEY_CURRENT_USER\Software\Tencent\MobileGamePC";
            var val = _registry.GetValue(key, "PreferVTx");
            return val != null && val.ToString() == "1";
        });
    }

    #endregion

    #region 4. Engine Render Boost

    public async Task<OptimizationResult> ApplyEngineRenderBoostAsync(bool enable)
    {
        try
        {
            // 1. Configure GameLoop Registry render tweaks
            string[] tencentKeys =
            {
                @"HKEY_CURRENT_USER\Software\Tencent\MobileGamePC",
                @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Tencent\MobileGamePC"
            };

            foreach (var key in tencentKeys)
            {
                if (enable)
                {
                    _registry.SetValue(key, "RenderType", 1u, RegistryValueKind.DWord);       // 1 = DirectX+
                    _registry.SetValue(key, "Vsync", 0u, RegistryValueKind.DWord);            // Disable VSync
                    _registry.SetValue(key, "ShaderCache", 1u, RegistryValueKind.DWord);      // Enable shader cache
                    _registry.SetValue(key, "ThreadOptimization", 1u, RegistryValueKind.DWord); // Thread Boost
                    _registry.SetValue(key, "NoLagMode", 1u, RegistryValueKind.DWord);
                }
                else
                {
                    _registry.SetValue(key, "Vsync", 1u, RegistryValueKind.DWord);
                    _registry.DeleteValue(key, "ThreadOptimization");
                    _registry.DeleteValue(key, "NoLagMode");
                }
            }

            // 2. Locate and tune config.ini and vm_configuration.ini
            var installPath = await _gameLoopDetection.GetInstallPathAsync();
            if (installPath != null)
            {
                string[] iniPaths =
                {
                    Path.Combine(installPath, "TxGameAssistant", "UI", "config.ini"),
                    Path.Combine(installPath, "TxGameAssistant", "UI", "vm_configuration.ini")
                };

                foreach (var ini in iniPaths)
                {
                    if (File.Exists(ini))
                    {
                        await _backup.BackupFileAsync(ini);
                        if (enable)
                        {
                            UpdateIniKey(ini, "Engine", "render_type", "directx");
                            UpdateIniKey(ini, "Engine", "vsync", "0");
                            UpdateIniKey(ini, "Engine", "shader_cache", "1");
                            UpdateIniKey(ini, "Engine", "multithread", "1");
                        }
                    }
                }
            }

            var status = enable ? "Engine Render Boost applied (DirectX+, No VSync, Thread Boost, Shader Cache)" : "Engine render settings restored";
            _log.Log("GameLoop", status, "SUCCESS");
            return OptimizationResult.Succeeded(status);
        }
        catch (Exception ex)
        {
            _log.Log("GameLoop", "ApplyEngineRenderBoost exception", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed($"Failed to apply Engine Render Boost: {ex.Message}");
        }
    }

    public Task<bool> IsEngineRenderBoostEnabledAsync()
    {
        return Task.Run(() =>
        {
            const string key = @"HKEY_CURRENT_USER\Software\Tencent\MobileGamePC";
            var renderType = _registry.GetValue(key, "RenderType");
            var vsync = _registry.GetValue(key, "Vsync");
            return renderType?.ToString() == "1" && vsync?.ToString() == "0";
        });
    }

    #endregion

    #region 5. MMCSS Games Priority Profile

    public async Task<OptimizationResult> ApplyMmcssGamesPriorityAsync(bool enable)
    {
        if (!_admin.IsRunningAsAdmin)
        {
            _log.Log("System", "ApplyMmcssGamesPriority failed: Admin required", "ERROR");
            return OptimizationResult.Failed("Administrator privileges required to modify Windows Multimedia SystemProfile.");
        }

        try
        {
            const string gamesPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
            await _backup.ExportRegistryKeyAsync(gamesPath);

            if (enable)
            {
                _registry.SetValue(gamesPath, "Affinity", 0u, RegistryValueKind.DWord);
                _registry.SetValue(gamesPath, "Background Only", "False", RegistryValueKind.String);
                _registry.SetValue(gamesPath, "Clock Rate", 10000u, RegistryValueKind.DWord); // 10ms interval
                _registry.SetValue(gamesPath, "GPU Priority", 8u, RegistryValueKind.DWord);  // High GPU time-slice
                _registry.SetValue(gamesPath, "Priority", 6u, RegistryValueKind.DWord);      // High thread priority
                _registry.SetValue(gamesPath, "Scheduling Category", "High", RegistryValueKind.String);
                _registry.SetValue(gamesPath, "SFIO Priority", "High", RegistryValueKind.String);
            }
            else
            {
                _registry.SetValue(gamesPath, "Affinity", 0u, RegistryValueKind.DWord);
                _registry.SetValue(gamesPath, "Background Only", "False", RegistryValueKind.String);
                _registry.SetValue(gamesPath, "Clock Rate", 10000u, RegistryValueKind.DWord);
                _registry.SetValue(gamesPath, "GPU Priority", 8u, RegistryValueKind.DWord);
                _registry.SetValue(gamesPath, "Priority", 2u, RegistryValueKind.DWord);
                _registry.SetValue(gamesPath, "Scheduling Category", "Medium", RegistryValueKind.String);
                _registry.SetValue(gamesPath, "SFIO Priority", "Normal", RegistryValueKind.String);
            }

            var status = enable ? "MMCSS Games Priority profile configured to High" : "MMCSS Games Priority restored to defaults";
            _log.Log("System", status, "SUCCESS");
            return OptimizationResult.Succeeded(status);
        }
        catch (Exception ex)
        {
            _log.Log("System", "ApplyMmcssGamesPriority exception", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed($"Failed to configure MMCSS profile: {ex.Message}");
        }
    }

    public Task<bool> IsMmcssGamesPriorityEnabledAsync()
    {
        return Task.Run(() =>
        {
            const string gamesPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
            var sched = _registry.GetValue(gamesPath, "Scheduling Category")?.ToString();
            var sfio = _registry.GetValue(gamesPath, "SFIO Priority")?.ToString();
            var priority = _registry.GetValue(gamesPath, "Priority")?.ToString();

            return sched == "High" && sfio == "High" && priority == "6";
        });
    }

    #endregion

    #region 6. System Responsiveness + Network Throttle Off

    public async Task<OptimizationResult> SetSystemResponsivenessAndNetworkThrottleAsync(bool enable)
    {
        if (!_admin.IsRunningAsAdmin)
        {
            _log.Log("System", "SetSystemResponsivenessAndNetworkThrottle failed: Admin required", "ERROR");
            return OptimizationResult.Failed("Administrator privileges required to modify System Responsiveness.");
        }

        try
        {
            const string sysProfile = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
            await _backup.ExportRegistryKeyAsync(sysProfile);

            if (enable)
            {
                // NetworkThrottlingIndex: 0xFFFFFFFF (4294967295) disables throttling
                _registry.SetValue(sysProfile, "NetworkThrottlingIndex", 4294967295u, RegistryValueKind.DWord);
                // SystemResponsiveness: 0 dedicates 100% of CPU time to foreground games (default is 20)
                _registry.SetValue(sysProfile, "SystemResponsiveness", 0u, RegistryValueKind.DWord);
            }
            else
            {
                _registry.SetValue(sysProfile, "NetworkThrottlingIndex", 10u, RegistryValueKind.DWord);
                _registry.SetValue(sysProfile, "SystemResponsiveness", 20u, RegistryValueKind.DWord);
            }

            var status = enable ? "Network Throttling removed and System Responsiveness set to 0 (Full)" : "Network throttling and responsiveness restored to Windows defaults";
            _log.Log("System", status, "SUCCESS");
            return OptimizationResult.Succeeded(status);
        }
        catch (Exception ex)
        {
            _log.Log("System", "SetSystemResponsivenessAndNetworkThrottle exception", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed($"Failed to configure System Responsiveness & Network Throttle: {ex.Message}");
        }
    }

    public Task<bool> IsNetworkThrottlingDisabledAsync()
    {
        return Task.Run(() =>
        {
            const string sysProfile = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
            var netIndex = _registry.GetValue(sysProfile, "NetworkThrottlingIndex")?.ToString();
            var sysResp = _registry.GetValue(sysProfile, "SystemResponsiveness")?.ToString();

            return (netIndex == "4294967295" || netIndex == "-1") && sysResp == "0";
        });
    }

    #endregion

    #region 7. Disable Game DVR / Xbox Game Bar

    public async Task<OptimizationResult> DisableGameDvrAndXboxGameBarAsync(bool enable)
    {
        await Task.Yield();
        try
        {
            // 1. User GameDVR capture settings
            const string gameDvrKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR";
            _registry.SetValue(gameDvrKey, "AppCaptureEnabled", enable ? 0u : 1u, RegistryValueKind.DWord);

            // 2. GameConfigStore tweaks
            const string gameConfigStore = @"HKEY_CURRENT_USER\System\GameConfigStore";
            _registry.SetValue(gameConfigStore, "GameDVR_Enabled", enable ? 0u : 1u, RegistryValueKind.DWord);
            _registry.SetValue(gameConfigStore, "GameDVR_FSEBehaviorMode", enable ? 2u : 0u, RegistryValueKind.DWord);
            _registry.SetValue(gameConfigStore, "GameDVR_HonorUserFSEBehaviorMode", enable ? 1u : 0u, RegistryValueKind.DWord);
            _registry.SetValue(gameConfigStore, "GameDVR_DXGIHonorPlayEnhancedMode", enable ? 0u : 1u, RegistryValueKind.DWord);
            _registry.SetValue(gameConfigStore, "GameDVR_EFSEFeatureFlags", enable ? 0u : 1u, RegistryValueKind.DWord);

            // 3. GameBar settings
            const string gameBarKey = @"HKEY_CURRENT_USER\Software\Microsoft\GameBar";
            _registry.SetValue(gameBarKey, "AllowAutoGameMode", enable ? 0u : 1u, RegistryValueKind.DWord);
            _registry.SetValue(gameBarKey, "AutoGameModeEnabled", enable ? 0u : 1u, RegistryValueKind.DWord);
            _registry.SetValue(gameBarKey, "UseNexusForGameBarEnabled", enable ? 0u : 1u, RegistryValueKind.DWord);

            // 4. Policy level (if Admin available)
            if (_admin.IsRunningAsAdmin)
            {
                const string policyGameDvr = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\GameDVR";
                _registry.SetValue(policyGameDvr, "AllowGameDVR", enable ? 0u : 1u, RegistryValueKind.DWord);
            }

            var status = enable ? "Game DVR and Xbox Game Bar disabled (overlay and background recording OFF)" : "Game DVR and Xbox Game Bar restored";
            _log.Log("System", status, "SUCCESS");
            return OptimizationResult.Succeeded(status);
        }
        catch (Exception ex)
        {
            _log.Log("System", "DisableGameDvrAndXboxGameBar exception", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed($"Failed to configure Game DVR: {ex.Message}");
        }
    }

    public Task<bool> IsGameDvrDisabledAsync()
    {
        return Task.Run(() =>
        {
            const string gameDvrKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR";
            const string gameConfigStore = @"HKEY_CURRENT_USER\System\GameConfigStore";

            var capture = _registry.GetValue(gameDvrKey, "AppCaptureEnabled")?.ToString();
            var dvr = _registry.GetValue(gameConfigStore, "GameDVR_Enabled")?.ToString();

            return capture == "0" && dvr == "0";
        });
    }

    #endregion

    #region 8. Disable Update Delivery Optimization (P2P)

    public async Task<OptimizationResult> DisableDeliveryOptimizationP2PAsync(bool enable)
    {
        if (!_admin.IsRunningAsAdmin)
        {
            _log.Log("System", "DisableDeliveryOptimizationP2P failed: Admin required", "ERROR");
            return OptimizationResult.Failed("Administrator privileges required to configure Delivery Optimization.");
        }

        try
        {
            // DODownloadMode: 0 = Off / Bypass (No P2P seeding, HTTP only)
            const string policyKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
            const string configKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config";

            await _backup.ExportRegistryKeyAsync(policyKey);
            await _backup.ExportRegistryKeyAsync(configKey);

            if (enable)
            {
                _registry.SetValue(policyKey, "DODownloadMode", 0u, RegistryValueKind.DWord);
                _registry.SetValue(configKey, "DODownloadMode", 0u, RegistryValueKind.DWord);
            }
            else
            {
                _registry.SetValue(policyKey, "DODownloadMode", 1u, RegistryValueKind.DWord); // 1 = LAN only
                _registry.SetValue(configKey, "DODownloadMode", 1u, RegistryValueKind.DWord);
            }

            var status = enable ? "Windows Update Delivery Optimization (P2P seeding) disabled" : "Delivery Optimization restored to default";
            _log.Log("System", status, "SUCCESS");
            return OptimizationResult.Succeeded(status);
        }
        catch (Exception ex)
        {
            _log.Log("System", "DisableDeliveryOptimizationP2P exception", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed($"Failed to configure Delivery Optimization: {ex.Message}");
        }
    }

    public Task<bool> IsDeliveryOptimizationDisabledAsync()
    {
        return Task.Run(() =>
        {
            const string policyKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
            const string configKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config";

            var polVal = _registry.GetValue(policyKey, "DODownloadMode")?.ToString();
            var cfgVal = _registry.GetValue(configKey, "DODownloadMode")?.ToString();

            return polVal == "0" || cfgVal == "0";
        });
    }

    #endregion

    #region 120 FPS Safe Unlock & Frame Rate Tuning

    public async Task<OptimizationResult> Unlock120FpsAsync(bool enable)
    {
        try
        {
            await _backup.StartSessionAsync();

            const string tencentKey = @"HKEY_CURRENT_USER\Software\Tencent\MobileGamePC";
            await _backup.ExportRegistryKeyAsync(tencentKey);

            if (enable)
            {
                // 1. Force PUBG Mobile 120 FPS Level in GameLoop Registry
                _registry.SetValue(tencentKey, "com.tencent.ig_FPSLevel", 120u, RegistryValueKind.DWord);
                _registry.SetValue(tencentKey, "com.vng.pubgmobile_FPSLevel", 120u, RegistryValueKind.DWord);
                _registry.SetValue(tencentKey, "com.pubg.krmobile_FPSLevel", 120u, RegistryValueKind.DWord);
                _registry.SetValue(tencentKey, "com.pubg.imobile_FPSLevel", 120u, RegistryValueKind.DWord);
                _registry.SetValue(tencentKey, "fps_max", 120u, RegistryValueKind.DWord);
                _registry.SetValue(tencentKey, "VSyncEnabled", 0u, RegistryValueKind.DWord);
                _registry.SetValue(tencentKey, "FPSLock", 0u, RegistryValueKind.DWord);
                _registry.SetValue(tencentKey, "RenderOptimizeEnabled", 1u, RegistryValueKind.DWord);
                _registry.SetValue(tencentKey, "LocalShaderCacheEnabled", 1u, RegistryValueKind.DWord);

                // 2. Set Device Model to Official 120Hz Supported Device (Samsung Galaxy Tab S9 - SM-X910)
                _registry.SetValue(tencentKey, "VMDeviceModel", "SM-X910", RegistryValueKind.String);
                _registry.SetValue(tencentKey, "VMDeviceManufacturer", "SAMSUNG", RegistryValueKind.String);
                _registry.SetValue(tencentKey, "VMPhoneDevice", "Samsung SM-X910", RegistryValueKind.String);
            }
            else
            {
                _registry.SetValue(tencentKey, "com.tencent.ig_FPSLevel", 90u, RegistryValueKind.DWord);
                _registry.SetValue(tencentKey, "fps_max", 90u, RegistryValueKind.DWord);
                _registry.SetValue(tencentKey, "VMDeviceModel", "SM-X920", RegistryValueKind.String);
                _registry.SetValue(tencentKey, "VMPhoneDevice", "Asus ROG 2", RegistryValueKind.String);
            }

            // 3. Update GameLoop Config.ini if found
            var installPath = await _gameLoopDetection.GetInstallPathAsync();
            if (installPath != null)
            {
                var candidatePaths = new[]
                {
                    Path.Combine(installPath, "Config.ini"),
                    Path.Combine(installPath, "UI", "Config.ini"),
                    Path.Combine(installPath, "TxGameAssistant", "UI", "Config.ini")
                };

                foreach (var iniPath in candidatePaths)
                {
                    if (File.Exists(iniPath))
                    {
                        await _backup.BackupFileAsync(iniPath);
                        UpdateIniKey(iniPath, "Engine", "fps_max", enable ? "120" : "90");
                        UpdateIniKey(iniPath, "Engine", "vsync", "0");
                        UpdateIniKey(iniPath, "Engine", "render_swap_interval", "0");
                        UpdateRootIniKey(iniPath, "fps_max", enable ? "120" : "90");
                    }
                }
            }

            await _backup.EndSessionAsync();
            var status = enable
                ? "120 FPS Ultra Mode unlocked successfully! Device model set to Samsung SM-X910 (Galaxy Tab S9 120Hz)."
                : "Restored stock 90 FPS configuration.";
            _log.Log("GameLoop", status, "SUCCESS");
            return OptimizationResult.Succeeded(status);
        }
        catch (Exception ex)
        {
            _log.Log("GameLoop", "Unlock120Fps exception", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed($"Failed to configure 120 FPS: {ex.Message}");
        }
    }

    public Task<bool> Is120FpsUnlockedAsync()
    {
        return Task.Run(() =>
        {
            const string tencentKey = @"HKEY_CURRENT_USER\Software\Tencent\MobileGamePC";
            var fpsVal = _registry.GetValue(tencentKey, "com.tencent.ig_FPSLevel")?.ToString();
            var modelVal = _registry.GetValue(tencentKey, "VMDeviceModel")?.ToString();

            return (fpsVal == "120" || modelVal == "SM-X910");
        });
    }

    public async Task<OptimizationResult> Create120FpsShortcutAsync()
    {
        return await Task.Run(async () =>
        {
            try
            {
                var installPath = await _gameLoopDetection.GetInstallPathAsync();
                if (string.IsNullOrEmpty(installPath))
                {
                    return OptimizationResult.Failed("GameLoop installation path not detected.");
                }

                var candidateExes = new[]
                {
                    Path.Combine(installPath, "AndroidEmulatorEn.exe"),
                    Path.Combine(installPath, "UI", "AndroidEmulatorEn.exe"),
                    Path.Combine(installPath, "TxGameAssistant", "UI", "AndroidEmulatorEn.exe"),
                    Path.Combine(installPath, "AndroidEmulator.exe"),
                    Path.Combine(installPath, "UI", "AndroidEmulator.exe")
                };

                var targetExe = candidateExes.FirstOrDefault(File.Exists);
                if (targetExe == null)
                {
                    return OptimizationResult.Failed("Could not locate AndroidEmulatorEn.exe in GameLoop directory.");
                }

                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var shortcutPath = Path.Combine(desktop, "GameLoop (120 FPS Ultra).lnk");
                var workingDir = Path.GetDirectoryName(targetExe);

                // Use PowerShell to create Desktop .lnk cleanly
                var script = $"$WshShell = New-Object -ComObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('{shortcutPath}'); $Shortcut.TargetPath = '{targetExe}'; $Shortcut.Arguments = '-vm 100'; $Shortcut.Description = 'GameLoop 120 FPS Ultra Performance'; $Shortcut.WorkingDirectory = '{workingDir}'; $Shortcut.Save()";

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                    if (proc.ExitCode == 0)
                    {
                        return OptimizationResult.Succeeded("Created 'GameLoop (120 FPS Ultra)' shortcut on your Desktop!");
                    }
                }

                return OptimizationResult.Failed("Failed to create Desktop shortcut.");
            }
            catch (Exception ex)
            {
                return OptimizationResult.Failed($"Error creating shortcut: {ex.Message}");
            }
        });
    }

    public async Task<OptimizationResult> LaunchGameLoop120FpsAsync()
    {
        return await Task.Run(async () =>
        {
            try
            {
                var installPath = await _gameLoopDetection.GetInstallPathAsync();
                if (string.IsNullOrEmpty(installPath))
                {
                    return OptimizationResult.Failed("GameLoop installation path not detected.");
                }

                var candidateExes = new[]
                {
                    Path.Combine(installPath, "AndroidEmulatorEn.exe"),
                    Path.Combine(installPath, "UI", "AndroidEmulatorEn.exe"),
                    Path.Combine(installPath, "TxGameAssistant", "UI", "AndroidEmulatorEn.exe"),
                    Path.Combine(installPath, "AndroidEmulator.exe")
                };

                var targetExe = candidateExes.FirstOrDefault(File.Exists);
                if (targetExe == null)
                {
                    return OptimizationResult.Failed("Could not locate AndroidEmulatorEn.exe.");
                }

                // Ensure 120 FPS registry & config are applied before launching
                await Unlock120FpsAsync(true);

                var psi = new ProcessStartInfo
                {
                    FileName = targetExe,
                    Arguments = "-vm 100",
                    WorkingDirectory = Path.GetDirectoryName(targetExe),
                    UseShellExecute = true
                };

                Process.Start(psi);
                return OptimizationResult.Succeeded("GameLoop launched in 120 FPS Ultra mode (-vm 100)!");
            }
            catch (Exception ex)
            {
                return OptimizationResult.Failed($"Error launching GameLoop: {ex.Message}");
            }
        });
    }

    #endregion

    #region One-Tap Optimization Batch

    public async Task<OptimizationResult> ApplyOneTapOptimizationAsync()
    {
        await _backup.StartSessionAsync();
        int successCount = 0;
        var errors = new List<string>();

        // 1. Emulator Priority
        var r1 = await SetEmulatorHighPriorityAsync(true);
        if (r1.Success) successCount++; else errors.Add($"Priority: {r1.Message}");

        // 2. Force Dedicated GPU + VRR
        var r2 = await ForceDedicatedGpuAndVrrAsync(true);
        if (r2.Success) successCount++; else errors.Add($"GPU: {r2.Message}");

        // 3. Prefer VT-x over Hyper-V
        var r3 = await PreferVtxOverHyperVAsync(true);
        if (r3.Success) successCount++; else errors.Add($"VT-x: {r3.Message}");

        // 4. Engine Render Boost
        var r4 = await ApplyEngineRenderBoostAsync(true);
        if (r4.Success) successCount++; else errors.Add($"Render: {r4.Message}");

        // 5. MMCSS Profile
        var r5 = await ApplyMmcssGamesPriorityAsync(true);
        if (r5.Success) successCount++; else errors.Add($"MMCSS: {r5.Message}");

        // 6. System Responsiveness & Network Throttle
        var r6 = await SetSystemResponsivenessAndNetworkThrottleAsync(true);
        if (r6.Success) successCount++; else errors.Add($"Network/Resp: {r6.Message}");

        // 7. Disable Game DVR / Game Bar
        var r7 = await DisableGameDvrAndXboxGameBarAsync(true);
        if (r7.Success) successCount++; else errors.Add($"GameDVR: {r7.Message}");

        // 8. Disable Delivery Optimization P2P
        var r8 = await DisableDeliveryOptimizationP2PAsync(true);
        if (r8.Success) successCount++; else errors.Add($"DeliveryOpt: {r8.Message}");

        await _backup.EndSessionAsync();

        if (successCount >= 6)
        {
            return OptimizationResult.Succeeded($"One-Tap Optimization complete! Applied {successCount}/8 modules successfully.");
        }
        else
        {
            return OptimizationResult.Failed($"Applied {successCount}/8 modules. Errors: {string.Join("; ", errors)}");
        }
    }

    #endregion

    #region Helper Methods

    private Task<List<string>> LocateEmulatorExecutablesAsync(string? installPath)
    {
        return Task.Run(() =>
        {
            var paths = new List<string>();

            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                var candidateDirs = new[]
                {
                    installPath,
                    Path.Combine(installPath, "AppMarket"),
                    Path.Combine(installPath, "TxGameAssistant"),
                    Path.Combine(installPath, "TxGameAssistant", "AppMarket"),
                    Path.Combine(installPath, "AndroidEmulator"),
                    Path.Combine(installPath, "ui"),
                    Path.Combine(installPath, "TxGameAssistant", "ui")
                };

                foreach (var dir in candidateDirs)
                {
                    if (!Directory.Exists(dir)) continue;

                    foreach (var exe in EmulatorProcesses)
                    {
                        var filePath = Path.Combine(dir, exe);
                        if (File.Exists(filePath) && !paths.Contains(filePath))
                        {
                            paths.Add(filePath);
                        }
                    }
                }
            }

            // Fallback search in fixed drives
            if (paths.Count == 0)
            {
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
                {
                    var root = drive.RootDirectory.FullName;
                    var testDirs = new[]
                    {
                        Path.Combine(root, "Program Files", "TxGameAssistant", "AppMarket"),
                        Path.Combine(root, "Program Files", "TxGameAssistant", "ui"),
                        Path.Combine(root, "TxGameAssistant", "AppMarket"),
                        Path.Combine(root, "TxGameAssistant", "ui")
                    };

                    foreach (var dir in testDirs)
                    {
                        if (Directory.Exists(dir))
                        {
                            foreach (var exe in EmulatorProcesses)
                            {
                                var filePath = Path.Combine(dir, exe);
                                if (File.Exists(filePath) && !paths.Contains(filePath))
                                    paths.Add(filePath);
                            }
                        }
                    }
                }
            }

            return paths;
        });
    }

    private static void UpdateIniKey(string filePath, string sectionName, string keyName, string newValue)
    {
        try
        {
            if (!File.Exists(filePath)) return;

            var lines = File.ReadAllLines(filePath).ToList();
            bool inSection = false;
            bool sectionFound = false;
            bool keyUpdated = false;

            for (int i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    var currSec = trimmed[1..^1];
                    if (string.Equals(currSec, sectionName, StringComparison.OrdinalIgnoreCase))
                    {
                        inSection = true;
                        sectionFound = true;
                    }
                    else if (inSection)
                    {
                        // Exited our section, insert before this line if not updated
                        lines.Insert(i, $"{keyName}={newValue}");
                        keyUpdated = true;
                        break;
                    }
                }
                else if (inSection && trimmed.StartsWith($"{keyName}=", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"{keyName}={newValue}";
                    keyUpdated = true;
                    break;
                }
            }

            if (!keyUpdated)
            {
                if (!sectionFound)
                {
                    lines.Add($"[{sectionName}]");
                }
                lines.Add($"{keyName}={newValue}");
            }

            File.WriteAllLines(filePath, lines);
        }
        catch { }
    }

    private static void UpdateRootIniKey(string filePath, string keyName, string newValue)
    {
        try
        {
            if (!File.Exists(filePath)) return;
            var lines = File.ReadAllLines(filePath).ToList();
            bool updated = false;

            for (int i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith($"{keyName}=", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"{keyName}={newValue}";
                    updated = true;
                    break;
                }
            }

            if (!updated)
            {
                lines.Add($"{keyName}={newValue}");
            }

            File.WriteAllLines(filePath, lines);
        }
        catch { }
    }

    #endregion
}
