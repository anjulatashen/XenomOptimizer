using Microsoft.Win32;
using XenomOptimizer.Models;
using XenomOptimizer.Services;

namespace XenomOptimizer.Optimizations;

/// <summary>
/// Factory for all Windows-specific optimizations.
/// Each optimization is a self-contained unit that detects, applies, and restores a single setting.
/// </summary>
public static class WindowsOptimizations
{
    public static List<IOptimization> GetAll(IRegistryService registry, IBackupService backup, ILogService log)
    {
        return new List<IOptimization>
        {
            // ═══ Slide 1: Disable Unused Features & Services ═══
            new GameBarOptimization(registry, backup, log),
            new GameDvrOptimization(registry, backup, log),
            new BackgroundAppsOptimization(registry, backup, log),
            new DeliveryOptimizationTweak(registry, backup, log),
            new TelemetryOptimizationTweak(registry, backup, log),
            new WindowsErrorReportingTweak(registry, backup, log),
            new WindowsTipsOptimizationTweak(registry, backup, log),
            new SysMainOptimizationTweak(registry, backup, log),

            // ═══ Slide 2: Advance Windows Optimization ═══
            new PowerPlanOptimization(log, backup),
            new HardwareAcceleratedGpuSchedulingOptimization(registry, backup, log),
            new VisualEffectsOptimization(registry, backup, log),
            new GameModeOptimization(registry, backup, log),
            new MouseAccelerationOptimization(registry, backup, log),
            new GpuPriorityOptimization(registry, backup, log),
            new SystemResponsivenessOptimization(registry, backup, log),
            new NetworkThrottlingOptimization(registry, backup, log),
            new MmcssPriorityOptimization(registry, backup, log),

            // ═══ Slide 2 Bottom: Low-End PC Optimizations ═══
            new DesktopTransparencyOptimization(registry, backup, log),
            new CpuCoreUnparkingOptimization(registry, backup, log),
            new WindowsSearchIndexingOptimization(registry, backup, log),
        };
    }
}

/// <summary>
/// Base class for registry-based optimizations with backup/restore support.
/// </summary>
public abstract class RegistryOptimizationBase : IOptimization
{
    protected readonly IRegistryService Registry;
    protected readonly IBackupService Backup;
    protected readonly ILogService Log;

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Category { get; }
    public abstract string Description { get; }
    public abstract RiskLevel RiskLevel { get; }
    public abstract bool RequiresAdmin { get; }

    /// <summary>Full registry key path (e.g., HKEY_CURRENT_USER\Software\...).</summary>
    protected abstract string RegistryKeyPath { get; }

    /// <summary>Registry value name.</summary>
    protected abstract string ValueName { get; }

    /// <summary>The optimal value to set.</summary>
    protected abstract object RecommendedValueData { get; }

    /// <summary>The registry value kind.</summary>
    protected abstract RegistryValueKind ValueKind { get; }

    /// <summary>Human-readable description of the recommended value.</summary>
    protected abstract string RecommendedValueDisplay { get; }

    protected RegistryOptimizationBase(IRegistryService registry, IBackupService backup, ILogService log)
    {
        Registry = registry;
        Backup = backup;
        Log = log;
    }

    public virtual Task<bool> IsSupportedAsync()
    {
        return Task.FromResult(true);
    }

    public Task<OptimizationState> GetCurrentStateAsync()
    {
        var current = Registry.GetValue(RegistryKeyPath, ValueName);
        var currentStr = current?.ToString() ?? "Not Set";
        var isOptimal = current != null && current.ToString() == RecommendedValueData.ToString();

        return Task.FromResult(new OptimizationState
        {
            CurrentValue = currentStr,
            RecommendedValue = RecommendedValueDisplay,
            IsOptimal = isOptimal,
            IsSupported = true
        });
    }

    public async Task<OptimizationResult> ApplyAsync()
    {
        try
        {
            // Backup current value first
            var currentValue = Registry.GetValue(RegistryKeyPath, ValueName);
            await Backup.ExportRegistryKeyAsync(RegistryKeyPath);
            await Backup.CreateBackupAsync(Id, Name, Category, "Registry",
                currentValue?.ToString() ?? "Not Set",
                $"{RegistryKeyPath}\\{ValueName}");

            // Apply new value
            var success = Registry.SetValue(RegistryKeyPath, ValueName, RecommendedValueData, ValueKind);

            if (success)
            {
                Log.Log("Windows", $"Applied: {Name}", "SUCCESS",
                    previousValue: currentValue?.ToString(), newValue: RecommendedValueData.ToString());
                return OptimizationResult.Succeeded($"{Name} applied successfully.",
                    currentValue?.ToString(), RecommendedValueData.ToString());
            }

            return OptimizationResult.Failed("Failed to write registry value. Administrator privileges may be required.");
        }
        catch (Exception ex)
        {
            Log.Log("Windows", $"Failed to apply: {Name}", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed(ex.Message);
        }
    }

    public async Task<OptimizationResult> RestoreAsync()
    {
        try
        {
            var backups = Backup.GetBackupsByOptimization(Id);
            var latest = backups.FirstOrDefault(b => !b.IsRestored);

            if (latest == null)
                return OptimizationResult.Failed("No backup found to restore.");

            var originalValue = latest.OriginalValue;

            if (originalValue == "Not Set")
            {
                // Delete the value if it wasn't originally set
                Registry.DeleteValue(RegistryKeyPath, ValueName);
            }
            else
            {
                // Restore original value
                if (int.TryParse(originalValue, out var intVal))
                    Registry.SetValue(RegistryKeyPath, ValueName, intVal, ValueKind);
                else
                    Registry.SetValue(RegistryKeyPath, ValueName, originalValue, ValueKind);
            }

            await Backup.RestoreBackupAsync(latest.Id);
            Log.Log("Windows", $"Restored: {Name}", "SUCCESS", newValue: originalValue);
            return OptimizationResult.Succeeded($"{Name} restored to original value.", newValue: originalValue);
        }
        catch (Exception ex)
        {
            Log.Log("Windows", $"Failed to restore: {Name}", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed(ex.Message);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════
// POWER & PERFORMANCE
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Sets Windows power plan to High Performance.
/// Uses powercfg.exe rather than direct registry modification for safety.
/// </summary>
public class PowerPlanOptimization : IOptimization
{
    private readonly ILogService _log;
    private readonly IBackupService _backup;
    private string? _originalPlanGuid;

    // Known Windows power plan GUIDs
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    public string Id => "win_power_plan";
    public string Name => "Power Plan: High Performance";
    public string Category => "Power & Performance";
    public string Description => "Sets the Windows power plan to 'High Performance' which prevents CPU throttling and keeps the processor at higher clock speeds. This may increase power consumption but improves gaming performance.";
    public RiskLevel RiskLevel => RiskLevel.Low;
    public bool RequiresAdmin => false;

    public PowerPlanOptimization(ILogService log, IBackupService backup)
    {
        _log = log;
        _backup = backup;
    }

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public async Task<OptimizationState> GetCurrentStateAsync()
    {
        var (guid, name) = await GetActivePowerPlanAsync();
        var isHighPerf = guid?.Contains(HighPerformanceGuid, StringComparison.OrdinalIgnoreCase) == true;

        return new OptimizationState
        {
            CurrentValue = name ?? "Unknown",
            RecommendedValue = "High Performance",
            IsOptimal = isHighPerf,
            IsSupported = true
        };
    }

    public async Task<OptimizationResult> ApplyAsync()
    {
        try
        {
            var (currentGuid, currentName) = await GetActivePowerPlanAsync();
            _originalPlanGuid = currentGuid;

            // Backup original
            await _backup.CreateBackupAsync(Id, Name, Category, "Setting",
                $"{currentGuid}|{currentName}", "Power plan GUID and name");

            // Set High Performance
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = $"/setactive {HighPerformanceGuid}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _log.Log("Windows", "Power plan set to High Performance", "SUCCESS",
                    previousValue: currentName, newValue: "High Performance");
                return OptimizationResult.Succeeded("Power plan set to High Performance.",
                    currentName, "High Performance");
            }

            return OptimizationResult.Failed("Failed to change power plan.");
        }
        catch (Exception ex)
        {
            return OptimizationResult.Failed(ex.Message);
        }
    }

    public async Task<OptimizationResult> RestoreAsync()
    {
        try
        {
            var backups = _backup.GetBackupsByOptimization(Id);
            var latest = backups.FirstOrDefault(b => !b.IsRestored);
            if (latest == null) return OptimizationResult.Failed("No backup found.");

            var parts = latest.OriginalValue.Split('|');
            if (parts.Length < 1) return OptimizationResult.Failed("Invalid backup data.");

            var guid = parts[0].Trim();
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = $"/setactive {guid}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();

            await _backup.RestoreBackupAsync(latest.Id);
            _log.Log("Windows", $"Power plan restored to: {(parts.Length > 1 ? parts[1] : guid)}", "SUCCESS");
            return OptimizationResult.Succeeded("Power plan restored.");
        }
        catch (Exception ex)
        {
            return OptimizationResult.Failed(ex.Message);
        }
    }

    private static async Task<(string? Guid, string? Name)> GetActivePowerPlanAsync()
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/getactivescheme",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Parse: "Power Scheme GUID: 8c5e7fda-... (High performance)"
            var guidStart = output.IndexOf(':');
            var guidEnd = output.IndexOf('(');
            var nameEnd = output.LastIndexOf(')');

            string? guid = null, name = null;
            if (guidStart > 0 && guidEnd > guidStart)
                guid = output[(guidStart + 1)..guidEnd].Trim();
            if (guidEnd > 0 && nameEnd > guidEnd)
                name = output[(guidEnd + 1)..nameEnd].Trim();

            return (guid, name);
        }
        catch
        {
            return (null, null);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════
// HARDWARE ACCELERATED GPU SCHEDULING
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Enables Hardware Accelerated GPU Scheduling (HAGS).
/// Available on Windows 10 2004+ with compatible GPU drivers.
/// Reduces CPU overhead for GPU scheduling tasks.
/// </summary>
public class HardwareAcceleratedGpuSchedulingOptimization : RegistryOptimizationBase
{
    public override string Id => "win_hags";
    public override string Name => "Hardware Accelerated GPU Scheduling";
    public override string Category => "GPU Scheduling";
    public override string Description => "Allows the GPU to manage its own video memory scheduling directly, reducing CPU overhead. Requires Windows 10 2004+ and a compatible GPU driver (NVIDIA 451.48+ or AMD Adrenalin 20.5.1+). Requires restart.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
    protected override string ValueName => "HwSchMode";
    protected override object RecommendedValueData => 2; // 2 = Enabled
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Enabled (2)";

    public HardwareAcceleratedGpuSchedulingOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override Task<bool> IsSupportedAsync()
    {
        // HAGS requires Windows 10 2004 (Build 19041) or later
        var build = Environment.OSVersion.Version.Build;
        var supported = build >= 19041;
        return Task.FromResult(supported);
    }
}

// ═══════════════════════════════════════════════════════════════════
// VISUAL EFFECTS - OPTIMIZE FOR PERFORMANCE
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Reduces Windows visual effects to improve performance.
/// Sets UserPreferencesMask to "Adjust for best performance" mode
/// while keeping font smoothing enabled.
/// </summary>
public class VisualEffectsOptimization : RegistryOptimizationBase
{
    public override string Id => "win_visual_effects";
    public override string Name => "Reduce Visual Effects";
    public override string Category => "Power & Performance";
    public override string Description => "Disables unnecessary Windows visual effects (animations, transparency, shadows) to free up GPU and CPU resources. Font smoothing (ClearType) is preserved. Requires sign-out to fully apply.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects";
    protected override string ValueName => "VisualFXSetting";
    protected override object RecommendedValueData => 2; // 2 = Custom / Best Performance
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Best Performance (2)";

    public VisualEffectsOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

// ═══════════════════════════════════════════════════════════════════
// GAME MODE
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Enables Windows Game Mode.
/// Game Mode allocates more system resources to games and reduces background interruptions.
/// </summary>
public class GameModeOptimization : RegistryOptimizationBase
{
    public override string Id => "win_game_mode";
    public override string Name => "Windows Game Mode";
    public override string Category => "Windows Gaming Settings";
    public override string Description => "Enables Windows Game Mode which prioritizes system resources for games, prevents Windows Update from installing drivers during gaming, and reduces background activity.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Microsoft\GameBar";
    protected override string ValueName => "AutoGameModeEnabled";
    protected override object RecommendedValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Enabled (1)";

    public GameModeOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override Task<bool> IsSupportedAsync()
    {
        // Game Mode available since Windows 10 Creators Update (Build 15063)
        return Task.FromResult(Environment.OSVersion.Version.Build >= 15063);
    }
}

// ═══════════════════════════════════════════════════════════════════
// GAME BAR
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Disables Xbox Game Bar overlay.
/// The overlay consumes resources and can cause micro-stutters.
/// </summary>
public class GameBarOptimization : RegistryOptimizationBase
{
    public override string Id => "win_game_bar";
    public override string Name => "Disable Xbox Game Bar";
    public override string Category => "Windows Gaming Settings";
    public override string Description => "Disables the Xbox Game Bar overlay which runs in the background, consumes GPU resources for compositing, and can cause micro-stutters in some games. Screenshots and recording can still be done through other tools.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Microsoft\GameBar";
    protected override string ValueName => "AllowAutoGameMode";
    protected override object RecommendedValueData => 0; // 0 = Disabled
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";

    public GameBarOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

// ═══════════════════════════════════════════════════════════════════
// GAME DVR
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Disables Game DVR background recording.
/// Game DVR continuously records gameplay which uses GPU encoding resources.
/// </summary>
public class GameDvrOptimization : RegistryOptimizationBase
{
    public override string Id => "win_game_dvr";
    public override string Name => "Disable Game DVR";
    public override string Category => "Windows Gaming Settings";
    public override string Description => "Disables Game DVR background recording which uses hardware encoding (GPU) resources even when you're not actively recording. This frees up GPU encoder capacity and reduces frame time variance.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\System\GameConfigStore";
    protected override string ValueName => "GameDVR_Enabled";
    protected override object RecommendedValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";

    public GameDvrOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

// ═══════════════════════════════════════════════════════════════════
// MOUSE ACCELERATION
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Disables mouse pointer acceleration (Enhance pointer precision).
/// Provides 1:1 mouse input mapping which is preferred for gaming.
/// </summary>
public class MouseAccelerationOptimization : RegistryOptimizationBase
{
    public override string Id => "win_mouse_accel";
    public override string Name => "Disable Mouse Acceleration";
    public override string Category => "Input / Latency";
    public override string Description => "Disables 'Enhance pointer precision' (mouse acceleration). This provides 1:1 mouse movement mapping without Windows applying a non-linear curve, which improves aiming consistency in games.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Control Panel\Mouse";
    protected override string ValueName => "MouseSpeed";
    protected override object RecommendedValueData => "0"; // String value
    protected override RegistryValueKind ValueKind => RegistryValueKind.String;
    protected override string RecommendedValueDisplay => "Disabled (0)";

    public MouseAccelerationOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

// ═══════════════════════════════════════════════════════════════════
// BACKGROUND APPS
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Disables background apps from running when not in use.
/// Reduces CPU/RAM usage from UWP apps running in background.
/// </summary>
public class BackgroundAppsOptimization : RegistryOptimizationBase
{
    public override string Id => "win_bg_apps";
    public override string Name => "Disable Background Apps";
    public override string Category => "Background Activity";
    public override string Description => "Prevents UWP (Microsoft Store) apps from running in the background. This reduces CPU and RAM usage from apps like News, Weather, Cortana, etc. that poll for data even when not opened.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";
    protected override string ValueName => "GlobalUserDisabled";
    protected override object RecommendedValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (1)";

    public BackgroundAppsOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

// ═══════════════════════════════════════════════════════════════════
// GPU PRIORITY
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// Sets GPU scheduling priority for games.
/// Tells Windows to prioritize GPU resources for foreground applications.
/// </summary>
public class GpuPriorityOptimization : RegistryOptimizationBase
{
    public override string Id => "win_gpu_priority";
    public override string Name => "GPU Priority for Foreground Apps";
    public override string Category => "GPU Scheduling";
    public override string Description => "Sets the GPU scheduling priority for foreground applications to high (8). This tells the Windows GPU scheduler to prioritize the active game window over background GPU tasks.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
    protected override string ValueName => "GPU Priority";
    protected override object RecommendedValueData => 8;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "High (8)";

    public GpuPriorityOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

// ═══════════════════════════════════════════════════════════════════
// SLIDE 1: UNUSED SERVICES & FEATURES TWEAKS
// ═══════════════════════════════════════════════════════════════════

public class DeliveryOptimizationTweak : RegistryOptimizationBase
{
    public override string Id => "win_delivery_opt";
    public override string Name => "Disable Delivery Optimization P2P";
    public override string Category => "Unused Services";
    public override string Description => "Stops Windows Update Delivery Optimization from using network bandwidth and disk I/O to seed updates to other PCs.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
    protected override string ValueName => "DODownloadMode";
    protected override object RecommendedValueData => 0; // 0 = Bypass / HTTP only
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";

    public DeliveryOptimizationTweak(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class TelemetryOptimizationTweak : RegistryOptimizationBase
{
    public override string Id => "win_telemetry";
    public override string Name => "Disable Windows Telemetry & Diagnostics";
    public override string Category => "Unused Services";
    public override string Description => "Disables Windows background telemetry, diagnostic tracking and data collection, freeing up CPU cycles and network polling.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection";
    protected override string ValueName => "AllowTelemetry";
    protected override object RecommendedValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";

    public TelemetryOptimizationTweak(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class WindowsErrorReportingTweak : RegistryOptimizationBase
{
    public override string Id => "win_wersvc";
    public override string Name => "Disable Windows Error Reporting";
    public override string Category => "Unused Services";
    public override string Description => "Prevents Windows Error Reporting from collecting memory dumps and freezing background games when minor software crashes occur.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting";
    protected override string ValueName => "Disabled";
    protected override object RecommendedValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (1)";

    public WindowsErrorReportingTweak(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class WindowsTipsOptimizationTweak : RegistryOptimizationBase
{
    public override string Id => "win_tips";
    public override string Name => "Disable Windows Tips & Suggestions";
    public override string Category => "Unused Services";
    public override string Description => "Disables automated Windows suggestions, consumer features, and lock-screen tips that run background indexing tasks.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    protected override string ValueName => "SoftLandingEnabled";
    protected override object RecommendedValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";

    public WindowsTipsOptimizationTweak(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class SysMainOptimizationTweak : RegistryOptimizationBase
{
    public override string Id => "win_sysmain";
    public override string Name => "Disable SysMain (Superfetch for SSDs)";
    public override string Category => "Unused Services";
    public override string Description => "Disables the SysMain disk caching service, preventing background disk thrashing and stutter on modern SSDs during gaming.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SysMain";
    protected override string ValueName => "Start";
    protected override object RecommendedValueData => 4; // 4 = Disabled
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (4)";

    public SysMainOptimizationTweak(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

// ═══════════════════════════════════════════════════════════════════
// SLIDE 2: ADVANCE WINDOWS OPTIMIZATION TWEAKS
// ═══════════════════════════════════════════════════════════════════

public class SystemResponsivenessOptimization : RegistryOptimizationBase
{
    public override string Id => "win_system_responsiveness";
    public override string Name => "Full System Responsiveness (0%)";
    public override string Category => "Advance Optimization";
    public override string Description => "Removes the 20% reserved CPU limit for background tasks, dedicating 100% of CPU time slices to the active foreground game.";
    public override RiskLevel RiskLevel => RiskLevel.Medium;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    protected override string ValueName => "SystemResponsiveness";
    protected override object RecommendedValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Full 100% (0)";

    public SystemResponsivenessOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class NetworkThrottlingOptimization : RegistryOptimizationBase
{
    public override string Id => "win_net_throttle";
    public override string Name => "Disable Network Throttling Index";
    public override string Category => "Advance Optimization";
    public override string Description => "Disables Windows network packet throttling mechanism for non-multimedia traffic, reducing packet latency and jitter.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    protected override string ValueName => "NetworkThrottlingIndex";
    protected override object RecommendedValueData => 4294967295u;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0xFFFFFFFF)";

    public NetworkThrottlingOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class MmcssPriorityOptimization : RegistryOptimizationBase
{
    public override string Id => "win_mmcss_games";
    public override string Name => "MMCSS Games Priority Profile";
    public override string Category => "Advance Optimization";
    public override string Description => "Configures the Windows Multimedia Class Scheduler Service (MMCSS) to grant highest scheduling time slices and SFIO priority to games.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
    protected override string ValueName => "Scheduling Category";
    protected override object RecommendedValueData => "High";
    protected override RegistryValueKind ValueKind => RegistryValueKind.String;
    protected override string RecommendedValueDisplay => "High";

    public MmcssPriorityOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

// ═══════════════════════════════════════════════════════════════════
// SLIDE 2 BOTTOM: LOW-END PC OPTIMIZATIONS
// ═══════════════════════════════════════════════════════════════════

public class DesktopTransparencyOptimization : RegistryOptimizationBase
{
    public override string Id => "win_transparency";
    public override string Name => "Disable Desktop Transparency";
    public override string Category => "Low-End PC";
    public override string Description => "Disables DWM desktop and taskbar transparency effects, saving significant GPU rasterization fill-rate on entry-level graphics.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    protected override string ValueName => "EnableTransparency";
    protected override object RecommendedValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";

    public DesktopTransparencyOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class CpuCoreUnparkingOptimization : RegistryOptimizationBase
{
    public override string Id => "win_core_unparking";
    public override string Name => "CPU Core Unparking (Max Performance)";
    public override string Category => "Low-End PC";
    public override string Description => "Prevents Windows power manager from parking CPU cores into deep sleep, keeping all logical processor threads active.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583";
    protected override string ValueName => "ValueMax";
    protected override object RecommendedValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Unparked (0)";

    public CpuCoreUnparkingOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class WindowsSearchIndexingOptimization : RegistryOptimizationBase
{
    public override string Id => "win_search_indexing";
    public override string Name => "Disable Search Indexing During Gaming";
    public override string Category => "Low-End PC";
    public override string Description => "Prevents Windows Search indexer from thrashing disk drives while games are running, eliminating 100% disk usage spikes.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search";
    protected override string ValueName => "PreventIndexingLowDiskSpaceMB";
    protected override object RecommendedValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Optimized (1)";

    public WindowsSearchIndexingOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

