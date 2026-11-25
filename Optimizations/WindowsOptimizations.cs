using System.Diagnostics;
using System.Linq;
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
            new BackgroundAppsOptimization(registry, backup, log),
            new DeliveryOptimizationTweak(registry, backup, log),
            new TelemetryOptimizationTweak(registry, backup, log),
            new WindowsErrorReportingTweak(registry, backup, log),
            new WindowsTipsOptimizationTweak(registry, backup, log),
            new SysMainOptimizationTweak(registry, backup, log),
            new BrowserBackgroundOptimization(registry, backup, log),
            new PhoneServiceOptimization(registry, backup, log),
            new PrinterServiceOptimization(registry, backup, log),
            new LocationTrackingOptimization(registry, backup, log),
            new ActivityHistoryOptimization(registry, backup, log),
            new CopilotAiOptimization(registry, backup, log),

            // ═══ Slide 2: Advance Windows Optimization ═══
            new PowerPlanOptimization(log, backup),
            new HardwareAcceleratedGpuSchedulingOptimization(registry, backup, log),
            new GameBarOptimization(registry, backup, log),
            new GameDvrOptimization(registry, backup, log),
            new GameModeOptimization(registry, backup, log),
            new MouseAccelerationOptimization(registry, backup, log),
            new GpuPriorityOptimization(registry, backup, log),
            new SystemResponsivenessOptimization(registry, backup, log),
            new NetworkThrottlingOptimization(registry, backup, log),
            new MmcssPriorityOptimization(registry, backup, log),
            new CpuCoreUnparkingOptimization(registry, backup, log),
            new WindowsSearchIndexingOptimization(registry, backup, log),
            new PowerThrottlingOptimization(registry, backup, log),
            new GpuDpcLatencyOptimization(registry, backup, log),
            new MouseQueueBufferOptimization(registry, backup, log),
            new ConsumerBloatwareOptimization(registry, backup, log),
            new TcpNoDelayOptimization(registry, backup, log),
            new Win32PrioritySeparationOptimization(registry, backup, log),
            new DynamicTickOptimization(backup, log),
            new QosDriverPriorityOptimization(registry, backup, log),
            new TrueFullscreenFseOptimization(registry, backup, log),
            new GameLoopCpuFrameBufferOptimization(registry, backup, log),

            // ═══ Slide 2: Low-End PC Optimizations ═══
            new VisualEffectsOptimization(registry, backup, log),
            new DesktopTransparencyOptimization(registry, backup, log),
            new UltimatePerformanceOptimization(log, backup),
            new OfflineStartSearchOptimization(registry, backup, log),
            new MemoryCompressionOptimization(backup, log),
            new GameBarPresenceWriterOptimization(registry, backup, log),
            new VirtualMemory8GBOptimization(registry, backup, log),
            new WidgetsFeedsOptimization(registry, backup, log),
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

    /// <summary>The default (stock) value to restore when no backup exists.</summary>
    protected virtual object? DefaultValueData => null;

    /// <summary>Whether to delete the registry value when restoring with no backup and DefaultValueData is null.</summary>
    protected virtual bool DeleteOnRestoreIfNoBackup => true;

    protected RegistryOptimizationBase(IRegistryService registry, IBackupService backup, ILogService log)
    {
        Registry = registry;
        Backup = backup;
        Log = log;
    }

    /// <summary>
    /// Executes a Windows Service Control (sc.exe) command and waits briefly for SCM state commit.
    /// </summary>
    protected static void RunServiceCommand(string arguments)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("sc.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            var p = System.Diagnostics.Process.Start(psi);
            p?.WaitForExit(3000);
        }
        catch { }
    }

    public virtual Task<bool> IsSupportedAsync()
    {
        return Task.FromResult(true);
    }

    public bool IsValueOptimal(object? current)
    {
        if (current == null) return false;
        if (string.Equals(current.ToString(), RecommendedValueData?.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (ValueKind == RegistryValueKind.DWord && RecommendedValueData != null)
        {
            try
            {
                uint currentDword = unchecked((uint)Convert.ToInt64(current));
                uint recDword = unchecked((uint)Convert.ToInt64(RecommendedValueData));
                if (currentDword == recDword)
                {
                    return true;
                }
            }
            catch { }
        }
        else if (ValueKind == RegistryValueKind.QWord && RecommendedValueData != null)
        {
            try
            {
                ulong currentQword = unchecked((ulong)Convert.ToInt64(current));
                ulong recQword = unchecked((ulong)Convert.ToInt64(RecommendedValueData));
                if (currentQword == recQword)
                {
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    public Task<OptimizationState> GetCurrentStateAsync()
    {
        var current = Registry.GetValue(RegistryKeyPath, ValueName);
        var currentStr = current?.ToString() ?? "Not Set";
        bool isOptimal = IsValueOptimal(current);

        if (ValueKind == RegistryValueKind.DWord && current != null)
        {
            try
            {
                uint currentDword = unchecked((uint)Convert.ToInt64(current));
                if (currentDword == 0xFFFFFFFF && RecommendedValueData != null && unchecked((uint)Convert.ToInt64(RecommendedValueData)) == 0xFFFFFFFF)
                {
                    currentStr = RecommendedValueDisplay;
                }
            }
            catch { }
        }

        return Task.FromResult(new OptimizationState
        {
            CurrentValue = currentStr,
            RecommendedValue = RecommendedValueDisplay,
            IsOptimal = isOptimal,
            IsSupported = true
        });
    }

    public virtual async Task<OptimizationResult> ApplyAsync()
    {
        try
        {
            // Backup current value first (only if not already in optimal state)
            var currentValue = Registry.GetValue(RegistryKeyPath, ValueName);
            if (!IsValueOptimal(currentValue))
            {
                _ = Task.Run(() => Backup.ExportRegistryKeyAsync(RegistryKeyPath));
                await Backup.CreateBackupAsync(Id, Name, Category, "Registry",
                    currentValue?.ToString() ?? "Not Set",
                    $"{RegistryKeyPath}\\{ValueName}");
            }

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

    public virtual async Task<OptimizationResult> RestoreAsync()
    {
        try
        {
            var backups = Backup.GetBackupsByOptimization(Id);
            // Find latest backup that is not restored and whose original value was not already optimal
            var validBackup = backups.FirstOrDefault(b => !b.IsRestored && !IsValueOptimal(b.OriginalValue));

            // Mark all existing unrestored backups for this optimization as restored so stale backups do not loop
            foreach (var b in backups.Where(b => !b.IsRestored))
            {
                await Backup.RestoreBackupAsync(b.Id);
            }

            if (validBackup != null && !IsValueOptimal(validBackup.OriginalValue))
            {
                var originalValue = validBackup.OriginalValue;

                if (originalValue == "Not Set")
                {
                    // Delete the value if it wasn't originally set
                    Registry.DeleteValue(RegistryKeyPath, ValueName);
                }
                else
                {
                    // Restore original value
                    if (ValueKind == RegistryValueKind.DWord && int.TryParse(originalValue, out var intVal))
                        Registry.SetValue(RegistryKeyPath, ValueName, intVal, ValueKind);
                    else
                        Registry.SetValue(RegistryKeyPath, ValueName, originalValue, ValueKind);
                }

                // Verify the restored value is indeed not optimal
                var check = Registry.GetValue(RegistryKeyPath, ValueName);
                if (!IsValueOptimal(check))
                {
                    Log.Log("Windows", $"Restored: {Name}", "SUCCESS", newValue: originalValue);
                    return OptimizationResult.Succeeded($"{Name} restored to original value.", newValue: originalValue);
                }
            }

            // Fallback when no valid pre-existing backup found (or previous backup was already optimal): restore to known Windows factory default
            if (DefaultValueData != null)
            {
                Registry.SetValue(RegistryKeyPath, ValueName, DefaultValueData, ValueKind);
                Log.Log("Windows", $"Restored to default: {Name}", "SUCCESS", newValue: DefaultValueData.ToString());
                return OptimizationResult.Succeeded($"{Name} restored to factory default.", newValue: DefaultValueData.ToString());
            }

            if (DeleteOnRestoreIfNoBackup)
            {
                Registry.DeleteValue(RegistryKeyPath, ValueName);
                Log.Log("Windows", $"Deleted registry value to restore default: {Name}", "SUCCESS");
                return OptimizationResult.Succeeded($"{Name} reset to default.", newValue: "Default");
            }

            return OptimizationResult.Succeeded($"{Name} already in default state.");
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

    [System.Runtime.InteropServices.DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

    public string Id => "win_power_plan";
    public string Name => "Power Plan: High Performance";
    public string Category => "Power & Performance";
    public string Description => "Keeps CPU at peak clock speeds and prevents power throttling for maximum gaming performance.";
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

            // Set High Performance instantly via Win32 API (< 0.1ms)
            if (Guid.TryParse(HighPerformanceGuid, out var hpGuid))
            {
                try { PowerSetActiveScheme(IntPtr.Zero, ref hpGuid); } catch { }
            }

            // Sync via powercfg in background
            _ = Task.Run(() =>
            {
                try
                {
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
                    process.WaitForExit(3000);
                }
                catch { }
            });

            _log.Log("Windows", "Power plan set to High Performance", "SUCCESS",
                previousValue: currentName, newValue: "High Performance");
            return OptimizationResult.Succeeded("Power plan set to High Performance.",
                currentName, "High Performance");
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
            var latest = backups.FirstOrDefault(b => !b.IsRestored) ?? backups.FirstOrDefault();

            if (latest == null)
            {
                var ultBackups = _backup.GetBackupsByOptimization("win_ultimate_performance");
                latest = ultBackups.FirstOrDefault(b => !b.IsRestored) ?? ultBackups.FirstOrDefault();
            }

            string targetGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
            string targetName = "Balanced";

            if (latest != null)
            {
                var parts = latest.OriginalValue.Split('|');
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    targetGuid = parts[0].Trim();
                    if (parts.Length > 1) targetName = parts[1].Trim();
                }
            }
            else
            {
                var initialPlan = UltimatePerformanceOptimization.GetPersistedInitialPowerPlan();
                if (initialPlan != null)
                {
                    targetGuid = initialPlan.Value.Guid;
                    targetName = initialPlan.Value.Name;
                }
            }

            // If the user's prior plan was already High/Ultimate Performance, turning it OFF falls back to Windows default Balanced
            if (string.Equals(targetGuid, HighPerformanceGuid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetGuid, "e9a42b02-d5df-448d-aa00-03f14749eb61", StringComparison.OrdinalIgnoreCase))
            {
                targetGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
                targetName = "Balanced";
            }

            // Instant Win32 API restoration (< 0.1ms)
            if (Guid.TryParse(targetGuid, out var resGuid))
            {
                try { PowerSetActiveScheme(IntPtr.Zero, ref resGuid); } catch { }
            }

            // Sync via powercfg in background
            _ = Task.Run(() =>
            {
                try
                {
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "powercfg",
                            Arguments = $"/setactive {targetGuid}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    process.Start();
                    process.WaitForExit(3000);
                }
                catch { }
            });

            if (latest != null && !latest.IsRestored)
            {
                await _backup.RestoreBackupAsync(latest.Id);
            }

            _log.Log("Windows", $"Power plan restored to: {targetName}", "SUCCESS");
            return OptimizationResult.Succeeded($"Power plan restored to: {targetName}");
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

            if (!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(name))
            {
                UltimatePerformanceOptimization.EnsureInitialPowerPlanPersisted(guid, name);
            }

            return (guid, name);
        }
        catch
        {
            return (null, null);
        }
    }
}

/// <summary>
/// <summary>
/// Unlocks and activates Windows Ultimate Performance power scheme.
/// Eliminates micro-latencies by disabling all CPU power saving and disk spindown timers.
/// </summary>
public class UltimatePerformanceOptimization : IOptimization
{
    private readonly ILogService _log;
    private readonly IBackupService _backup;
    private const string UltimateTemplateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private static string? _cachedUltimateGuid;

    [System.Runtime.InteropServices.DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

    public string Id => "win_ultimate_performance";
    public string Name => "Power Plan: Ultimate Performance";
    public string Category => "Low-End PC";
    public string Description => "Disables all CPU throttling, core sleep states, and disk spindown timers for raw speed.";
    public RiskLevel RiskLevel => RiskLevel.Medium;
    public bool RequiresAdmin => true;

    public UltimatePerformanceOptimization(ILogService log, IBackupService backup)
    {
        _log = log;
        _backup = backup;
    }

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public async Task<OptimizationState> GetCurrentStateAsync()
    {
        var (guid, name) = await GetActivePowerPlanAsync();
        bool isUltimate = name?.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase) == true;

        return new OptimizationState
        {
            CurrentValue = name ?? "Unknown",
            RecommendedValue = "Ultimate Performance",
            IsOptimal = isUltimate,
            IsSupported = true
        };
    }

    public async Task<OptimizationResult> ApplyAsync()
    {
        try
        {
            var (currentGuid, currentName) = await GetActivePowerPlanAsync();

            // Backup original
            await _backup.CreateBackupAsync(Id, Name, Category, "Setting",
                $"{currentGuid}|{currentName}", "Power plan GUID and name");

            // Check if Ultimate Performance scheme already exists
            var existingUltimateGuid = _cachedUltimateGuid ?? await FindUltimatePerformanceGuidAsync();
            if (string.IsNullOrEmpty(existingUltimateGuid))
            {
                // Duplicate template
                existingUltimateGuid = await DuplicateUltimateSchemeAsync();
            }

            if (string.IsNullOrEmpty(existingUltimateGuid))
            {
                existingUltimateGuid = UltimateTemplateGuid;
            }
            _cachedUltimateGuid = existingUltimateGuid;

            // Instant Win32 API activation (< 0.1ms)
            if (Guid.TryParse(existingUltimateGuid, out var schemeGuid))
            {
                try { PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid); } catch { }
            }

            // Sync via powercfg in background
            _ = Task.Run(() =>
            {
                try
                {
                    var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("powercfg", $"/setactive {existingUltimateGuid}")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    p?.WaitForExit(3000);
                }
                catch { }
            });

            _log.Log("Windows", "Power plan set to Ultimate Performance", "SUCCESS",
                previousValue: currentName, newValue: "Ultimate Performance");
            return OptimizationResult.Succeeded("Power plan set to Ultimate Performance.",
                currentName, "Ultimate Performance");
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
            var latest = backups.FirstOrDefault(b => !b.IsRestored) ?? backups.FirstOrDefault();

            if (latest == null)
            {
                var powerPlanBackups = _backup.GetBackupsByOptimization("win_power_plan");
                latest = powerPlanBackups.FirstOrDefault(b => !b.IsRestored) ?? powerPlanBackups.FirstOrDefault();
            }

            string targetGuid = "381b4222-f694-41f0-9685-ff5bb260df2e"; // Default: Balanced
            string targetName = "Balanced";

            if (latest != null)
            {
                var parts = latest.OriginalValue.Split('|');
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    targetGuid = parts[0].Trim();
                    if (parts.Length > 1) targetName = parts[1].Trim();
                }
            }
            else
            {
                var initialPlan = GetPersistedInitialPowerPlan();
                if (initialPlan != null)
                {
                    targetGuid = initialPlan.Value.Guid;
                    targetName = initialPlan.Value.Name;
                }
            }

            // If prior plan was Ultimate/High Performance, turning it OFF falls back to Windows default Balanced
            if (string.Equals(targetGuid, UltimateTemplateGuid, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetGuid, "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase))
            {
                targetGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
                targetName = "Balanced";
            }

            // Instant Win32 API activation (< 0.1ms)
            if (Guid.TryParse(targetGuid, out var restoreGuid))
            {
                try { PowerSetActiveScheme(IntPtr.Zero, ref restoreGuid); } catch { }
            }

            // Sync via powercfg in background
            _ = Task.Run(() =>
            {
                try
                {
                    var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("powercfg", $"/setactive {targetGuid}")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    p?.WaitForExit(3000);
                }
                catch { }
            });

            if (latest != null && !latest.IsRestored)
            {
                await _backup.RestoreBackupAsync(latest.Id);
            }

            _log.Log("Windows", $"Power plan restored to: {targetName}", "SUCCESS");
            return OptimizationResult.Succeeded("Power plan restored to " + targetName);
        }
        catch (Exception ex)
        {
            return OptimizationResult.Failed(ex.Message);
        }
    }

    public static (string Guid, string Name)? GetPersistedInitialPowerPlan()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var filePath = System.IO.Path.Combine(appData, "XenomOptimizer", "initial_power_plan.txt");
            if (System.IO.File.Exists(filePath))
            {
                var text = System.IO.File.ReadAllText(filePath).Trim();
                var parts = text.Split('|');
                if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    return (parts[0].Trim(), parts[1].Trim());
                }
            }
        }
        catch { }
        return null;
    }

    public static void EnsureInitialPowerPlanPersisted(string? guid, string? name)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(name)) return;
            if (name.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase)) return;

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = System.IO.Path.Combine(appData, "XenomOptimizer");
            System.IO.Directory.CreateDirectory(dir);
            var filePath = System.IO.Path.Combine(dir, "initial_power_plan.txt");
            if (!System.IO.File.Exists(filePath))
            {
                System.IO.File.WriteAllText(filePath, $"{guid}|{name}");
            }
        }
        catch { }
    }

    private static async Task<string?> FindUltimatePerformanceGuidAsync()
    {
        // 1. Fast direct registry lookup (< 0.05ms)
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes");
            if (key != null)
            {
                foreach (var subName in key.GetSubKeyNames())
                {
                    if (string.Equals(subName, UltimateTemplateGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        return subName;
                    }
                    using var subKey = key.OpenSubKey(subName);
                    var friendly = subKey?.GetValue("FriendlyName")?.ToString();
                    if (friendly != null && friendly.Contains("Ultimate", StringComparison.OrdinalIgnoreCase))
                    {
                        return subName;
                    }
                }
            }
        }
        catch { }

        // 2. Fallback to powercfg if registry lookup did not find it
        return await Task.Run(() =>
        {
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = "/list",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(2000);

                using var reader = new System.IO.StringReader(output);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase))
                    {
                        var idx = line.IndexOf("Power Scheme GUID:", StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            var start = idx + "Power Scheme GUID:".Length;
                            var end = line.IndexOf('(', start);
                            if (end > start)
                            {
                                return line[start..end].Trim();
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        });
    }

    private static async Task<string?> DuplicateUltimateSchemeAsync()
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = $"-duplicatescheme {UltimateTemplateGuid}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var idx = output.IndexOf("Power Scheme GUID:", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var start = idx + "Power Scheme GUID:".Length;
                var end = output.IndexOf('(', start);
                if (end > start)
                {
                    return output[start..end].Trim();
                }
            }
        }
        catch { }
        return null;
    }

    private static async Task<(string? Guid, string? Name)> GetActivePowerPlanAsync()
    {
        // 1. Fast direct registry read (< 0.05ms)
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes");
            var activeGuidStr = key?.GetValue("ActivePowerScheme")?.ToString();
            if (!string.IsNullOrWhiteSpace(activeGuidStr))
            {
                using var schemeKey = key?.OpenSubKey(activeGuidStr);
                var friendly = schemeKey?.GetValue("FriendlyName")?.ToString();
                if (!string.IsNullOrWhiteSpace(friendly))
                {
                    var commaIdx = friendly.LastIndexOf(',');
                    string name = commaIdx >= 0 ? friendly[(commaIdx + 1)..].Trim() : friendly;
                    EnsureInitialPowerPlanPersisted(activeGuidStr, name);
                    return (activeGuidStr, name);
                }
                return (activeGuidStr, activeGuidStr);
            }
        }
        catch { }

        // 2. Fallback to powercfg if registry read fails
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

            var guidStart = output.IndexOf(':');
            var guidEnd = output.IndexOf('(');
            var nameEnd = output.LastIndexOf(')');

            string? guid = null, name = null;
            if (guidStart > 0 && guidEnd > guidStart)
                guid = output[(guidStart + 1)..guidEnd].Trim();
            if (guidEnd > 0 && nameEnd > guidEnd)
                name = output[(guidEnd + 1)..nameEnd].Trim();

            if (!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(name))
            {
                EnsureInitialPowerPlanPersisted(guid, name);
            }

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
    public override string Description => "Allows GPU to manage VRAM directly to reduce CPU overhead and input latency. Requires restart.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
    protected override string ValueName => "HwSchMode";
    protected override object RecommendedValueData => 2; // 2 = Enabled
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Enabled (2)";
    protected override object? DefaultValueData => 1; // 1 = Disabled (Windows default)

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
/// Custom Performance Options Visual Effects:
/// Disables all 15 Windows UI animations, fades, and shadows,
/// while keeping ONLY Thumbnails and Font Smoothing enabled (matching user reference).
/// </summary>
public class VisualEffectsOptimization : IOptimization
{
    private readonly IRegistryService _registry;
    private readonly IBackupService _backup;
    private readonly ILogService _log;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, UIntPtr wParam, string lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

    private const uint SPIF_UPDATEINIFILE = 0x01;
    private const uint SPIF_SENDCHANGE = 0x02;

    private static readonly string[] DisabledVisualEffectSubKeys = new[]
    {
        "AnimateMinMax",
        "ComboBoxAnimation",
        "ControlAnimations",
        "CursorShadow",
        "DragFullWindows",
        "DropShadow",
        "DWMAeroPeekEnabled",
        "DWMSaveThumbnailEnabled",
        "ListBoxSmoothScrolling",
        "ListviewAlphaSelect",
        "ListviewShadow",
        "MenuAnimation",
        "SelectionFade",
        "TaskbarAnimations",
        "TooltipAnimation"
    };

    public string Id => "win_visual_effects";
    public string Name => "Reduce Visual Effects";
    public string Category => "Low-End PC";
    public string Description => "Disables UI animations and shadows to free up GPU and CPU. Keeps Thumbnails and Font Smoothing active.";
    public RiskLevel RiskLevel => RiskLevel.Low;
    public bool RequiresAdmin => false;

    public VisualEffectsOptimization(IRegistryService registry, IBackupService backup, ILogService log)
    {
        _registry = registry;
        _backup = backup;
        _log = log;
    }

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public Task<OptimizationState> GetCurrentStateAsync()
    {
        try
        {
            var fxVal = _registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting");
            var taskbarAnim = _registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations");
            var dragFull = _registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "DragFullWindows");

            int fxInt = fxVal switch { int i => i, uint u => (int)u, long l => (int)l, _ => 0 };
            int tbInt = taskbarAnim switch { int i => i, uint u => (int)u, _ => 1 };
            string dragStr = dragFull?.ToString() ?? "1";

            bool isOptimal = (fxInt == 3 || fxInt == 2) && tbInt == 0 && dragStr == "0";

            return Task.FromResult(new OptimizationState
            {
                CurrentValue = isOptimal ? "Essential Only (Thumbnails & Fonts)" : "Windows Default",
                RecommendedValue = "Essential Only (Thumbnails & Fonts)",
                IsOptimal = isOptimal,
                StatusMessage = isOptimal ? "Only Thumbnails & Font Smoothing enabled" : "Windows animations enabled"
            });
        }
        catch (Exception ex)
        {
            _log.Log("VisualEffects", "Failed to get state", "ERROR", errorMessage: ex.Message);
            return Task.FromResult(new OptimizationState { IsOptimal = false, StatusMessage = "Unknown" });
        }
    }

    public async Task<OptimizationResult> ApplyAsync()
    {
        try
        {
            var state = await GetCurrentStateAsync();
            if (!state.IsOptimal)
            {
                await _backup.CreateBackupAsync(Id, Name, Category, "Registry", state.CurrentValue ?? "Windows Default", "Visual Effects Custom Settings");
            }

            // 1. Immediately apply all registry settings (< 0.5ms)
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 3, Microsoft.Win32.RegistryValueKind.DWord);

            // Mark individual VisualEffects subkeys: Only Thumbnails and FontSmoothing = 1, all others = 0
            foreach (var subKey in DisabledVisualEffectSubKeys)
            {
                _registry.SetValue($@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\{subKey}", "DefaultApplied", 0, Microsoft.Win32.RegistryValueKind.DWord);
            }
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\ThumbnailsOrIcon", "DefaultApplied", 1, Microsoft.Win32.RegistryValueKind.DWord);
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\FontSmoothing", "DefaultApplied", 1, Microsoft.Win32.RegistryValueKind.DWord);

            // Set UserPreferencesMask: Keep font smoothing bit, turn off all animation, fade, and shadow bits
            byte[] mask = new byte[] { 0x90, 0x12, 0x01, 0x80, 0x10, 0x00, 0x00, 0x00 };
            _registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "UserPreferencesMask", mask, Microsoft.Win32.RegistryValueKind.Binary);

            // Keep Font Smoothing ON (Smooth edges of screen fonts - Checked)
            _registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "FontSmoothing", "2", Microsoft.Win32.RegistryValueKind.String);
            _registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "FontSmoothingType", 2, Microsoft.Win32.RegistryValueKind.DWord);

            // Disable dragging window contents (Show window contents while dragging - Unchecked)
            _registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "DragFullWindows", "0", Microsoft.Win32.RegistryValueKind.String);

            // Disable window minimize/maximize animation (Animate windows when minimizing and maximizing - Unchecked)
            _registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", Microsoft.Win32.RegistryValueKind.String);

            // Keep Thumbnails ON (Show thumbnails instead of icons - Checked: IconsOnly = 0)
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "IconsOnly", 0, Microsoft.Win32.RegistryValueKind.DWord);

            // Disable translucent selection, drop shadows, taskbar animations
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect", 0, Microsoft.Win32.RegistryValueKind.DWord);
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow", 0, Microsoft.Win32.RegistryValueKind.DWord);
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0, Microsoft.Win32.RegistryValueKind.DWord);

            // Disable AeroPeek and Thumbnail cache
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0, Microsoft.Win32.RegistryValueKind.DWord);
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AlwaysHibernateThumbnails", 0, Microsoft.Win32.RegistryValueKind.DWord);

            // Asynchronously notify Windows shell and apps in background so UI never blocks for seconds
            _ = Task.Run(() =>
            {
                try
                {
                    uint flags = SPIF_UPDATEINIFILE;
                    SystemParametersInfo(75, 1, IntPtr.Zero, flags); // SPI_SETFONTSMOOTHING -> ON
                    SystemParametersInfo(37, 0, IntPtr.Zero, flags); // SPI_SETDRAGFULLWINDOWS -> OFF
                    SystemParametersInfo(4099, 0, IntPtr.Zero, flags); // SPI_SETMENUANIMATION -> OFF
                    SystemParametersInfo(4101, 0, IntPtr.Zero, flags); // SPI_SETCOMBOBOXANIMATION -> OFF
                    SystemParametersInfo(4103, 0, IntPtr.Zero, flags); // SPI_SETLISTBOXSMOOTHSCROLLING -> OFF
                    SystemParametersInfo(4117, 0, IntPtr.Zero, flags); // SPI_SETSELECTIONFADE -> OFF
                    SystemParametersInfo(4119, 0, IntPtr.Zero, flags); // SPI_SETTOOLTIPANIMATION -> OFF
                    SystemParametersInfo(4123, 0, IntPtr.Zero, flags); // SPI_SETCURSORSHADOW -> OFF
                    SystemParametersInfo(4133, 0, IntPtr.Zero, flags); // SPI_SETDROPSHADOW -> OFF
                    SystemParametersInfo(4163, 0, IntPtr.Zero, flags); // SPI_SETCLIENTAREAANIMATION -> OFF
                    SendMessageTimeout(new IntPtr(0xffff), 0x001A, UIntPtr.Zero, "Environment", 2, 200, out _);
                }
                catch { }
            });

            _log.Log("VisualEffects", "Applied custom visual effects: Only Thumbnails & Font Smoothing enabled", "SUCCESS");
            return OptimizationResult.Succeeded($"{Name} applied: Only Thumbnails and Font Smoothing active.", "Default", "Essential Only");
        }
        catch (Exception ex)
        {
            _log.Log("VisualEffects", "Failed to apply visual effects", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed(ex.Message);
        }
    }

    public async Task<OptimizationResult> RestoreAsync()
    {
        try
        {
            // 1. Immediately restore default Windows settings in registry (< 0.5ms)
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 0, Microsoft.Win32.RegistryValueKind.DWord);

            foreach (var subKey in DisabledVisualEffectSubKeys)
            {
                _registry.SetValue($@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\{subKey}", "DefaultApplied", 1, Microsoft.Win32.RegistryValueKind.DWord);
            }
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\ThumbnailsOrIcon", "DefaultApplied", 1, Microsoft.Win32.RegistryValueKind.DWord);
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects\FontSmoothing", "DefaultApplied", 1, Microsoft.Win32.RegistryValueKind.DWord);

            byte[] defaultMask = new byte[] { 0x9E, 0x1E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00 };
            _registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "UserPreferencesMask", defaultMask, Microsoft.Win32.RegistryValueKind.Binary);

            _registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "FontSmoothing", "2", Microsoft.Win32.RegistryValueKind.String);
            _registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "FontSmoothingType", 2, Microsoft.Win32.RegistryValueKind.DWord);
            _registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "DragFullWindows", "1", Microsoft.Win32.RegistryValueKind.String);
            _registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics", "MinAnimate", "1", Microsoft.Win32.RegistryValueKind.String);

            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "IconsOnly", 0, Microsoft.Win32.RegistryValueKind.DWord);
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect", 1, Microsoft.Win32.RegistryValueKind.DWord);
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow", 1, Microsoft.Win32.RegistryValueKind.DWord);
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 1, Microsoft.Win32.RegistryValueKind.DWord);
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "EnableAeroPeek", 1, Microsoft.Win32.RegistryValueKind.DWord);
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AlwaysHibernateThumbnails", 1, Microsoft.Win32.RegistryValueKind.DWord);

            // Asynchronously notify Windows shell in background
            _ = Task.Run(() =>
            {
                try
                {
                    uint flags = SPIF_UPDATEINIFILE;
                    SystemParametersInfo(75, 1, IntPtr.Zero, flags); // SPI_SETFONTSMOOTHING -> ON
                    SystemParametersInfo(37, 1, IntPtr.Zero, flags); // SPI_SETDRAGFULLWINDOWS -> ON
                    SystemParametersInfo(4099, 1, IntPtr.Zero, flags); // SPI_SETMENUANIMATION -> ON
                    SystemParametersInfo(4101, 1, IntPtr.Zero, flags); // SPI_SETCOMBOBOXANIMATION -> ON
                    SystemParametersInfo(4103, 1, IntPtr.Zero, flags); // SPI_SETLISTBOXSMOOTHSCROLLING -> ON
                    SystemParametersInfo(4117, 1, IntPtr.Zero, flags); // SPI_SETSELECTIONFADE -> ON
                    SystemParametersInfo(4119, 1, IntPtr.Zero, flags); // SPI_SETTOOLTIPANIMATION -> ON
                    SystemParametersInfo(4123, 1, IntPtr.Zero, flags); // SPI_SETCURSORSHADOW -> ON
                    SystemParametersInfo(4133, 1, IntPtr.Zero, flags); // SPI_SETDROPSHADOW -> ON
                    SystemParametersInfo(4163, 1, IntPtr.Zero, flags); // SPI_SETCLIENTAREAANIMATION -> ON
                    SendMessageTimeout(new IntPtr(0xffff), 0x001A, UIntPtr.Zero, "Environment", 2, 200, out _);
                }
                catch { }
            });

            _log.Log("VisualEffects", "Restored default visual effects settings", "SUCCESS");
            return OptimizationResult.Succeeded($"{Name} restored to default Windows settings.", "Essential Only", "Default");
        }
        catch (Exception ex)
        {
            _log.Log("VisualEffects", "Failed to restore visual effects", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed(ex.Message);
        }
    }
}

/// <summary>
/// Allocates an 8192 MB (8GB) fixed Virtual Memory Pagefile on drive C: to expand RAM and eliminate stutters.
/// Safe and standard Windows memory management configuration.
/// </summary>
public class VirtualMemory8GBOptimization : IOptimization
{
    private readonly IRegistryService _registry;
    private readonly IBackupService _backup;
    private readonly ILogService _log;

    public string Id => "win_virtual_memory_8gb";
    public string Name => "Virtual Memory: 8GB RAM Boost";
    public string Category => "Low-End PC";
    public string Description => "Allocates a fixed 8192 MB (8GB) paging buffer on system drive (C:) from storage to expand available RAM and prevent low-memory crashes.";
    public RiskLevel RiskLevel => RiskLevel.Low;
    public bool RequiresAdmin => true;

    public VirtualMemory8GBOptimization(IRegistryService registry, IBackupService backup, ILogService log)
    {
        _registry = registry;
        _backup = backup;
        _log = log;
    }

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public Task<OptimizationState> GetCurrentStateAsync()
    {
        try
        {
            var val = _registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "PagingFiles");
            bool is8gb = false;
            string display = "System Managed";

            if (val is string[] arr && arr.Length > 0)
            {
                string combined = string.Join(" ", arr);
                if (combined.Contains("8192"))
                {
                    is8gb = true;
                    display = "Custom 8192 MB (8GB)";
                }
                else
                {
                    display = arr[0];
                }
            }
            else if (val is string s && s.Contains("8192"))
            {
                is8gb = true;
                display = "Custom 8192 MB (8GB)";
            }

            return Task.FromResult(new OptimizationState
            {
                CurrentValue = display,
                RecommendedValue = "Custom 8192 MB (8GB)",
                IsOptimal = is8gb,
                StatusMessage = is8gb ? "8GB Virtual Memory buffer active on C:" : "Default system-managed paging"
            });
        }
        catch (Exception ex)
        {
            _log.Log("VirtualMemory8GB", "Failed to get state", "ERROR", errorMessage: ex.Message);
            return Task.FromResult(new OptimizationState { IsOptimal = false, StatusMessage = "Unknown" });
        }
    }

    public async Task<OptimizationResult> ApplyAsync()
    {
        try
        {
            var state = await GetCurrentStateAsync();
            if (!state.IsOptimal)
            {
                await _backup.CreateBackupAsync(Id, Name, Category, "Registry", state.CurrentValue ?? "System Managed", "PagingFiles");
            }

            // Immediately set the 8GB fixed pagefile in registry (instant < 0.1ms)
            bool ok = _registry.SetValue(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
                "PagingFiles",
                new string[] { @"C:\pagefile.sys 8192 8192" },
                Microsoft.Win32.RegistryValueKind.MultiString);

            // WMI update is slow (2-3 seconds), execute asynchronously in background without delaying UI
            _ = Task.Run(() =>
            {
                try
                {
                    using var searcher = new System.Management.ManagementObjectSearcher("Select * from Win32_ComputerSystem");
                    foreach (System.Management.ManagementObject mo in searcher.Get())
                    {
                        mo["AutomaticManagedPagefile"] = false;
                        mo.Put();
                    }
                }
                catch { }
            });

            _log.Log("VirtualMemory8GB", "Applied 8GB Pagefile on C:", ok ? "SUCCESS" : "FAILED");
            return ok
                ? OptimizationResult.Succeeded($"{Name} applied: 8192 MB (8GB) fixed pagefile allocated on C:.", "System Managed", "Custom 8192 MB")
                : OptimizationResult.Failed("Failed to set 8GB Pagefile. Administrator privileges required.");
        }
        catch (Exception ex)
        {
            _log.Log("VirtualMemory8GB", "Failed to apply", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed(ex.Message);
        }
    }

    public async Task<OptimizationResult> RestoreAsync()
    {
        try
        {
            // Immediately restore system-managed pagefile in registry (instant < 0.1ms)
            bool ok = _registry.SetValue(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management",
                "PagingFiles",
                new string[] { @"?:\pagefile.sys" },
                Microsoft.Win32.RegistryValueKind.MultiString);

            // WMI update in background
            _ = Task.Run(() =>
            {
                try
                {
                    using var searcher = new System.Management.ManagementObjectSearcher("Select * from Win32_ComputerSystem");
                    foreach (System.Management.ManagementObject mo in searcher.Get())
                    {
                        mo["AutomaticManagedPagefile"] = true;
                        mo.Put();
                    }
                }
                catch { }
            });

            _log.Log("VirtualMemory8GB", "Restored system-managed pagefile", ok ? "SUCCESS" : "FAILED");
            return ok
                ? OptimizationResult.Succeeded($"{Name} restored to Windows default system-managed paging.")
                : OptimizationResult.Failed("Failed to restore default pagefile.");
        }
        catch (Exception ex)
        {
            _log.Log("VirtualMemory8GB", "Failed to restore", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed(ex.Message);
        }
    }
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
    public override string Category => "Power & Performance";
    public override string Description => "Prioritizes CPU and GPU hardware resources directly for active gaming sessions.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Microsoft\GameBar";
    protected override string ValueName => "AutoGameModeEnabled";
    protected override object RecommendedValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Enabled (1)";
    protected override object? DefaultValueData => 0; // 0 = Disabled

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
    public override string Category => "Power & Performance";
    public override string Description => "Disables Game Bar background overlay and capture services to free GPU resources.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Microsoft\GameBar";
    protected override string ValueName => "UseNexusForGameBarEnabled";
    protected override object RecommendedValueData => 0; // 0 = Disabled
    protected override object? DefaultValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";

    public GameBarOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode", 0, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode", 1, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }
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
    public override string Category => "Power & Performance";
    public override string Description => "Disables background gameplay recording to free GPU hardware encoder capacity.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\System\GameConfigStore";
    protected override string ValueName => "GameDVR_Enabled";
    protected override object RecommendedValueData => 0;
    protected override object? DefaultValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";

    public GameDvrOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_FSEBehaviorMode", 0, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }
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
    public override string Description => "Enables 1:1 raw mouse tracking without curve acceleration for consistent gaming aim.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Control Panel\Mouse";
    protected override string ValueName => "MouseSpeed";
    protected override object RecommendedValueData => "0"; // String value
    protected override RegistryValueKind ValueKind => RegistryValueKind.String;
    protected override string RecommendedValueDisplay => "Disabled (0)";
    protected override object? DefaultValueData => "1"; // "1" = Windows stock default

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
    public override string Description => "Prevents Microsoft Store apps from running background tasks and consuming RAM.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications";
    protected override string ValueName => "GlobalUserDisabled";
    protected override object RecommendedValueData => 1;
    protected override object? DefaultValueData => 0;
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
    public override string Description => "Grants highest GPU scheduling and graphics resource priority to active game windows.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
    protected override string ValueName => "GPU Priority";
    protected override object RecommendedValueData => 8;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "High (8)";
    protected override object? DefaultValueData => 8;

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
    public override string Description => "Stops Windows Update from utilizing internet bandwidth and disk to seed peers.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization";
    protected override string ValueName => "DODownloadMode";
    protected override object RecommendedValueData => 0; // 0 = Bypass / HTTP only
    protected override object? DefaultValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";

    public DeliveryOptimizationTweak(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config", "DODownloadMode", 0u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\DeliveryOptimization", "DODownloadMode", 0u, RegistryValueKind.DWord);
            RunServiceCommand("config DoSvc start= disabled");
            RunServiceCommand("stop DoSvc");
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config", "DODownloadMode", 1u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\DeliveryOptimization", "DODownloadMode", 1u, RegistryValueKind.DWord);
            RunServiceCommand("config DoSvc start= auto");
            RunServiceCommand("start DoSvc");
        }
        catch { }
        return res;
    }
}

public class TelemetryOptimizationTweak : RegistryOptimizationBase
{
    public override string Id => "win_telemetry";
    public override string Name => "Disable Windows Telemetry & Diagnostics";
    public override string Category => "Unused Services";
    public override string Description => "Turns off background telemetry data collection and automated diagnostic logging.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection";
    protected override string ValueName => "AllowTelemetry";
    protected override object RecommendedValueData => 0;
    protected override object? DefaultValueData => 3;
    protected override bool DeleteOnRestoreIfNoBackup => true;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";

    public TelemetryOptimizationTweak(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack", "AllowTelemetry", 0u, RegistryValueKind.DWord);
            RunServiceCommand("config DiagTrack start= disabled");
            RunServiceCommand("stop DiagTrack");
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry");
            Registry.DeleteValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack", "AllowTelemetry");
            RunServiceCommand("config DiagTrack start= auto");
            RunServiceCommand("start DiagTrack");
        }
        catch { }
        return res;
    }
}

public class WindowsErrorReportingTweak : RegistryOptimizationBase
{
    public override string Id => "win_wersvc";
    public override string Name => "Disable Windows Error Reporting";
    public override string Category => "Unused Services";
    public override string Description => "Prevents background memory dump logging and freezes during software crashes.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting";
    protected override string ValueName => "Disabled";
    protected override object RecommendedValueData => 1;
    protected override object? DefaultValueData => 0;
    protected override bool DeleteOnRestoreIfNoBackup => true;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (1)";

    public WindowsErrorReportingTweak(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1u, RegistryValueKind.DWord);
            RunServiceCommand("config WerSvc start= disabled");
            RunServiceCommand("stop WerSvc");
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 0u, RegistryValueKind.DWord);
            Registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled");
            RunServiceCommand("config WerSvc start= demand");
            RunServiceCommand("start WerSvc");
        }
        catch { }
        return res;
    }
}

public class WindowsTipsOptimizationTweak : RegistryOptimizationBase
{
    public override string Id => "win_tips";
    public override string Name => "Disable Windows Tips & Suggestions";
    public override string Category => "Unused Services";
    public override string Description => "Turns off automated consumer tips, lock-screen promotions, and system alerts.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    protected override string ValueName => "SoftLandingEnabled";
    protected override object RecommendedValueData => 0;
    protected override object? DefaultValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";

    public WindowsTipsOptimizationTweak(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled", 0u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353696Enabled", 0u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableConsumerFeatures", 1u, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 1u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled", 1u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353696Enabled", 1u, RegistryValueKind.DWord);
            Registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableConsumerFeatures");
        }
        catch { }
        return res;
    }
}

public class SysMainOptimizationTweak : RegistryOptimizationBase
{
    public override string Id => "win_sysmain";
    public override string Name => "Disable SysMain (Superfetch for SSDs)";
    public override string Category => "Unused Services";
    public override string Description => "Stops unnecessary background disk caching and hitching on modern high-speed SSDs.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SysMain";
    protected override string ValueName => "Start";
    protected override object RecommendedValueData => 4; // 4 = Disabled
    protected override object? DefaultValueData => 2; // 2 = Automatic
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (4)";

    public SysMainOptimizationTweak(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            RunServiceCommand("config SysMain start= disabled");
            RunServiceCommand("stop SysMain");
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            RunServiceCommand("config SysMain start= auto");
            RunServiceCommand("start SysMain");
        }
        catch { }
        return res;
    }
}

public class BrowserBackgroundOptimization : RegistryOptimizationBase
{
    public override string Id => "win_browser_background";
    public override string Name => "Disable Browser Background Activity";
    public override string Category => "Unused Services";
    public override string Description => "Stops Edge, Chrome, and browsers from running background apps when closed.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;

    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Edge";
    protected override string ValueName => "BackgroundModeEnabled";
    protected override object RecommendedValueData => 0;
    protected override object? DefaultValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";

    public BrowserBackgroundOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Edge", "StartupBoostEnabled", 0u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\Google\Chrome", "BackgroundModeEnabled", 0u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\BraveSoftware\Brave", "BackgroundModeEnabled", 0u, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Edge", "StartupBoostEnabled", 1u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\Google\Chrome", "BackgroundModeEnabled", 1u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\BraveSoftware\Brave", "BackgroundModeEnabled", 1u, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }
}

public class PhoneServiceOptimization : RegistryOptimizationBase
{
    public override string Id => "win_phone_service";
    public override string Name => "Disable Phone Service";
    public override string Category => "Unused Services";
    public override string Description => "Stops background Phone Link sync services and telephony background polling.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;

    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PhoneSvc";
    protected override string ValueName => "Start";
    protected override object RecommendedValueData => 4; // 4 = Disabled
    protected override object? DefaultValueData => 3; // 3 = Manual
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (4)";

    public PhoneServiceOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            RunServiceCommand("config PhoneSvc start= disabled");
            RunServiceCommand("stop PhoneSvc");
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            RunServiceCommand("config PhoneSvc start= demand");
            RunServiceCommand("start PhoneSvc");
        }
        catch { }
        return res;
    }
}

public class PrinterServiceOptimization : RegistryOptimizationBase
{
    public override string Id => "win_printer_service";
    public override string Name => "Disable Printer & Spooler Services";
    public override string Category => "Unused Services";
    public override string Description => "Stops background print spooler, notification queues, and printer polling.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;

    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Spooler";
    protected override string ValueName => "Start";
    protected override object RecommendedValueData => 4; // 4 = Disabled
    protected override object? DefaultValueData => 2; // 2 = Automatic
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (4)";

    public PrinterServiceOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PrintNotify", "Start", 4u, RegistryValueKind.DWord);
            RunServiceCommand("config Spooler start= disabled");
            RunServiceCommand("stop Spooler");
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PrintNotify", "Start", 3u, RegistryValueKind.DWord);
            RunServiceCommand("config Spooler start= auto");
            RunServiceCommand("start Spooler");
        }
        catch { }
        return res;
    }
}

public class LocationTrackingOptimization : RegistryOptimizationBase
{
    public override string Id => "win_location_tracking";
    public override string Name => "Disable Windows Location Tracking";
    public override string Category => "Unused Services";
    public override string Description => "Stops background GPS and location polling that causes network ping spikes.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;

    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors";
    protected override string ValueName => "DisableLocation";
    protected override object RecommendedValueData => 1;
    protected override object? DefaultValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (1)";

    public LocationTrackingOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\lfsvc", "Start", 4u, RegistryValueKind.DWord);
            RunServiceCommand("config lfsvc start= disabled");
            RunServiceCommand("stop lfsvc");
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\lfsvc", "Start", 3u, RegistryValueKind.DWord);
            RunServiceCommand("config lfsvc start= demand");
            RunServiceCommand("start lfsvc");
        }
        catch { }
        return res;
    }
}

public class ActivityHistoryOptimization : RegistryOptimizationBase
{
    public override string Id => "win_activity_history";
    public override string Name => "Disable Activity History & Tracking";
    public override string Category => "Unused Services";
    public override string Description => "Prevents Windows from collecting app usage history and syncing timeline to cloud.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;

    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System";
    protected override string ValueName => "EnableActivityFeed";
    protected override object RecommendedValueData => 0;
    protected override object? DefaultValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";

    public ActivityHistoryOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0u, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 1u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 1u, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }
}

public class CopilotAiOptimization : RegistryOptimizationBase
{
    public override string Id => "win_copilot_ai";
    public override string Name => "Disable Windows Copilot & AI Services";
    public override string Category => "Unused Services";
    public override string Description => "Disables Windows Copilot AI background processes and automated screen telemetry.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;

    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\WindowsCopilot";
    protected override string ValueName => "TurnOffWindowsCopilot";
    protected override object RecommendedValueData => 1;
    protected override object? DefaultValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (1)";

    public CopilotAiOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1u, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot");
            Registry.DeleteValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot");
        }
        catch { }
        return res;
    }
}

// ═══════════════════════════════════════════════════════════════════
// SLIDE 2: ADVANCE WINDOWS OPTIMIZATION TWEAKS
// ═══════════════════════════════════════════════════════════════════

public class SystemResponsivenessOptimization : RegistryOptimizationBase
{
    public override string Id => "win_system_responsiveness";
    public override string Name => "Full System Responsiveness (0%)";
    public override string Category => "Advance Optimization";
    public override string Description => "Removes the 20% reserved CPU limit for background tasks, dedicating 100% to games.";
    public override RiskLevel RiskLevel => RiskLevel.Medium;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    protected override string ValueName => "SystemResponsiveness";
    protected override object RecommendedValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Full 100% (0)";
    protected override object? DefaultValueData => 20;

    public SystemResponsivenessOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class NetworkThrottlingOptimization : RegistryOptimizationBase
{
    public override string Id => "win_net_throttle";
    public override string Name => "Disable Network Throttling Index";
    public override string Category => "Advance Optimization";
    public override string Description => "Disables Windows network packet throttling mechanism to reduce ping latency and jitter.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    protected override string ValueName => "NetworkThrottlingIndex";
    protected override object RecommendedValueData => unchecked((int)0xFFFFFFFF);
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0xFFFFFFFF)";
    protected override object? DefaultValueData => 10;

    public NetworkThrottlingOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class MmcssPriorityOptimization : RegistryOptimizationBase
{
    public override string Id => "win_mmcss_games";
    public override string Name => "MMCSS Games Priority Profile";
    public override string Category => "Advance Optimization";
    public override string Description => "Configures Windows MMCSS scheduler to grant highest CPU time slices to games.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
    protected override string ValueName => "Scheduling Category";
    protected override object RecommendedValueData => "High";
    protected override RegistryValueKind ValueKind => RegistryValueKind.String;
    protected override string RecommendedValueDisplay => "High";
    protected override object? DefaultValueData => "Medium";

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
    public override string Description => "Disables DWM desktop transparency to save GPU rasterization on entry-level graphics.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    protected override string ValueName => "EnableTransparency";
    protected override object RecommendedValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";
    protected override object? DefaultValueData => 1;

    public DesktopTransparencyOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class CpuCoreUnparkingOptimization : RegistryOptimizationBase
{
    public override string Id => "win_core_unparking";
    public override string Name => "CPU Core Unparking (Max Performance)";
    public override string Category => "Power & Performance";
    public override string Description => "Keeps all logical CPU processor cores active and prevents cores from entering sleep.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583";
    protected override string ValueName => "ValueMax";
    protected override object RecommendedValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Unparked (0)";
    protected override object? DefaultValueData => 100;

    public CpuCoreUnparkingOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class WindowsSearchIndexingOptimization : RegistryOptimizationBase
{
    public override string Id => "win_search_indexing";
    public override string Name => "Disable Search Indexing During Gaming";
    public override string Category => "Power & Performance";
    public override string Description => "Prevents Windows Search indexer from thrashing disk drives during active gaming.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search";
    protected override string ValueName => "PreventIndexingLowDiskSpaceMB";
    protected override object RecommendedValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Optimized (1)";
    protected override object? DefaultValueData => 0;

    public WindowsSearchIndexingOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class PowerThrottlingOptimization : RegistryOptimizationBase
{
    public override string Id => "win_power_throttling";
    public override string Name => "Disable Power Throttling & Fast Startup";
    public override string Category => "Advance Optimization";
    public override string Description => "Disables CPU power throttling and Fast Startup to prevent clock drops and stutters.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling";
    protected override string ValueName => "PowerThrottlingOff";
    protected override object RecommendedValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (1)";
    protected override object? DefaultValueData => 0;

    public PowerThrottlingOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabledDefault", 0u, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 1u, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabledDefault", 1u, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }
}

public class GpuDpcLatencyOptimization : RegistryOptimizationBase
{
    public override string Id => "win_gpu_dpc_latency";
    public override string Name => "GPU Core DPC Scheduling";
    public override string Category => "Advance Optimization";
    public override string Description => "Normalizes GPU driver DPC processing to prevent micro-stutters during high polling mouse movement.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
    protected override string ValueName => "RmGpsPsEnablePerCpuCoreDpc";
    protected override object RecommendedValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (Safe 0)";
    protected override bool DeleteOnRestoreIfNoBackup => true;

    public GpuDpcLatencyOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Power", "RmGpsPsEnablePerCpuCoreDpc");
            Registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "RmGpsPsEnablePerCpuCoreDpc");
            Registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm\NVAPI", "RmGpsPsEnablePerCpuCoreDpc");
            Registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm\Global\NVTweak", "RmGpsPsEnablePerCpuCoreDpc");
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Power", "RmGpsPsEnablePerCpuCoreDpc");
            Registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm", "RmGpsPsEnablePerCpuCoreDpc");
            Registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm\NVAPI", "RmGpsPsEnablePerCpuCoreDpc");
            Registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm\Global\NVTweak", "RmGpsPsEnablePerCpuCoreDpc");
        }
        catch { }
        return res;
    }
}

public class MouseQueueBufferOptimization : RegistryOptimizationBase
{
    public override string Id => "win_mouse_queue_buffer";
    public override string Name => "Mouse Queue Buffer (Low Latency)";
    public override string Category => "Advance Optimization";
    public override string Description => "Maintains safe 100-packet mouse buffer to eliminate FPS drops when rotating camera with high polling rate gaming mice.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\mouclass\Parameters";
    protected override string ValueName => "MouseDataQueueSize";
    protected override object RecommendedValueData => 100;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "100 Packets (Default Safe)";
    protected override object? DefaultValueData => 100;

    public MouseQueueBufferOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class GameLoopCpuFrameBufferOptimization : RegistryOptimizationBase
{
    public override string Id => "win_gameloop_cpu_framebuffer";
    public override string Name => "Disable GameLoop CPU FrameBuffer";
    public override string Category => "Advance Optimization";
    public override string Description => "Disables CPU frame buffering in GameLoop emulator to eliminate stuttering and lag.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Tencent\MobileGamePC";
    protected override string ValueName => "UseCPUFrameBuffer";
    protected override object RecommendedValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";
    protected override object? DefaultValueData => 1;

    public GameLoopCpuFrameBufferOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Tencent\MobileGamePC", "UseCPUFrameBuffer", 0u, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Tencent\MobileGamePC", "UseCPUFrameBuffer", 1u, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }
}

public class WidgetsFeedsOptimization : IOptimization
{
    private readonly IRegistryService _registry;
    private readonly IBackupService _backup;
    private readonly ILogService _log;

    public string Id => "win_disable_widgets_feeds";
    public string Name => "Disable Windows Widgets & Web Feeds";
    public string Category => "Low-End PC";
    public string Description => "Disables taskbar widgets and background web feeds to eliminate WebView2 RAM and CPU usage on low-spec PCs.";
    public RiskLevel RiskLevel => RiskLevel.Low;
    public bool RequiresAdmin => false;

    public WidgetsFeedsOptimization(IRegistryService registry, IBackupService backup, ILogService log)
    {
        _registry = registry;
        _backup = backup;
        _log = log;
    }

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public Task<OptimizationState> GetCurrentStateAsync()
    {
        try
        {
            var val = _registry.GetValue(@"HKEY_CURRENT_USER\Software\XenomOptimizer\Settings", "WidgetsFeedsDisabled");
            bool isOptimal = false;
            if (val is int i) isOptimal = i == 1;
            else if (val != null && int.TryParse(val.ToString(), out var parsed)) isOptimal = parsed == 1;

            return Task.FromResult(new OptimizationState
            {
                CurrentValue = isOptimal ? "Disabled (0)" : "Enabled (1)",
                RecommendedValue = "Disabled (0)",
                IsOptimal = isOptimal,
                IsSupported = true,
                StatusMessage = isOptimal ? "Widgets & Feeds disabled" : "Widgets & Feeds active"
            });
        }
        catch
        {
            return Task.FromResult(new OptimizationState { IsOptimal = false, CurrentValue = "Enabled (1)", RecommendedValue = "Disabled (0)", IsSupported = true });
        }
    }

    public Task<OptimizationResult> ApplyAsync()
    {
        try
        {
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\XenomOptimizer\Settings", "WidgetsFeedsDisabled", 1, Microsoft.Win32.RegistryValueKind.DWord);

            // Attempt to disable taskbar widgets and web feeds in background
            _ = Task.Run(() =>
            {
                try
                {
                    _registry.DeleteValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa");
                }
                catch { }
                try
                {
                    _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Feeds", "ShellFeedsTaskbarViewMode", 2, Microsoft.Win32.RegistryValueKind.DWord);
                }
                catch { }
                try
                {
                    _registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0, Microsoft.Win32.RegistryValueKind.DWord);
                }
                catch { }
                try
                {
                    _registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds", 0, Microsoft.Win32.RegistryValueKind.DWord);
                }
                catch { }
                try
                {
                    foreach (var p in System.Diagnostics.Process.GetProcessesByName("Widgets"))
                    {
                        try { p.Kill(); } catch { }
                    }
                }
                catch { }
            });

            _log.Log("Windows", $"Applied: {Name}", "SUCCESS", previousValue: "Enabled (1)", newValue: "Disabled (0)");
            return Task.FromResult(OptimizationResult.Succeeded($"{Name} applied successfully.", "Enabled (1)", "Disabled (0)"));
        }
        catch (Exception ex)
        {
            _log.Log("Windows", $"Failed to apply: {Name}", "ERROR", errorMessage: ex.Message);
            return Task.FromResult(OptimizationResult.Failed(ex.Message));
        }
    }

    public Task<OptimizationResult> RestoreAsync()
    {
        try
        {
            _registry.SetValue(@"HKEY_CURRENT_USER\Software\XenomOptimizer\Settings", "WidgetsFeedsDisabled", 0, Microsoft.Win32.RegistryValueKind.DWord);

            _ = Task.Run(() =>
            {
                try
                {
                    _registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Feeds", "ShellFeedsTaskbarViewMode", 0, Microsoft.Win32.RegistryValueKind.DWord);
                }
                catch { }
                try
                {
                    _registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests");
                }
                catch { }
                try
                {
                    _registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds");
                }
                catch { }
            });

            _log.Log("Windows", $"Restored: {Name}", "SUCCESS", newValue: "Enabled (1)");
            return Task.FromResult(OptimizationResult.Succeeded($"{Name} restored to default.", newValue: "Enabled (1)"));
        }
        catch (Exception ex)
        {
            _log.Log("Windows", $"Failed to restore: {Name}", "ERROR", errorMessage: ex.Message);
            return Task.FromResult(OptimizationResult.Failed(ex.Message));
        }
    }
}

public class ConsumerBloatwareOptimization : RegistryOptimizationBase
{
    public override string Id => "win_disable_consumer_bloatware";
    public override string Name => "Disable Windows Consumer Bloatware";
    public override string Category => "Advance Optimization";
    public override string Description => "Blocks automatic downloading of sponsored apps and promotional background packages.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CloudContent";
    protected override string ValueName => "DisableWindowsConsumerFeatures";
    protected override object RecommendedValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (1)";
    protected override object? DefaultValueData => 0;

    public ConsumerBloatwareOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            const string cdmKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
            Registry.SetValue(cdmKey, "SilentInstalledAppsEnabled", 0, RegistryValueKind.DWord);
            Registry.SetValue(cdmKey, "ContentDeliveryAllowed", 0, RegistryValueKind.DWord);
            Registry.SetValue(cdmKey, "OemPreInstalledAppsEnabled", 0, RegistryValueKind.DWord);
            Registry.SetValue(cdmKey, "PreInstalledAppsEnabled", 0, RegistryValueKind.DWord);
            Registry.SetValue(cdmKey, "PreInstalledAppsEverEnabled", 0, RegistryValueKind.DWord);
            Registry.SetValue(cdmKey, "SubscribedContent-338388Enabled", 0, RegistryValueKind.DWord);
            Registry.SetValue(cdmKey, "SubscribedContent-338389Enabled", 0, RegistryValueKind.DWord);
            Registry.SetValue(cdmKey, "SystemPaneSuggestionsEnabled", 0, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            const string cdmKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
            Registry.SetValue(cdmKey, "SilentInstalledAppsEnabled", 1, RegistryValueKind.DWord);
            Registry.SetValue(cdmKey, "ContentDeliveryAllowed", 1, RegistryValueKind.DWord);
            Registry.SetValue(cdmKey, "SystemPaneSuggestionsEnabled", 1, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }
}

public class TcpNoDelayOptimization : IOptimization
{
    private readonly IRegistryService _registry;
    private readonly IBackupService _backup;
    private readonly ILogService _log;

    public string Id => "win_tcp_nodelay";
    public string Name => "Disable Nagle's Algorithm (TCP NoDelay)";
    public string Category => "Advance Optimization";
    public string Description => "Disables TCP packet delay on network adapters to send data instantly, lowering ping.";
    public RiskLevel RiskLevel => RiskLevel.Low;
    public bool RequiresAdmin => true;

    private const string InterfacesKeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    public TcpNoDelayOptimization(IRegistryService registry, IBackupService backup, ILogService log)
    {
        _registry = registry;
        _backup = backup;
        _log = log;
    }

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public Task<OptimizationState> GetCurrentStateAsync()
    {
        try
        {
            using var baseKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces");
            if (baseKey != null)
            {
                var subKeyNames = baseKey.GetSubKeyNames();
                int optimizedCount = 0;
                int totalChecked = 0;

                foreach (var name in subKeyNames)
                {
                    using var subKey = baseKey.OpenSubKey(name);
                    if (subKey != null)
                    {
                        var ip = subKey.GetValue("IPAddress") ?? subKey.GetValue("DhcpIPAddress");
                        if (ip != null)
                        {
                            totalChecked++;
                            var tcpNoDelay = subKey.GetValue("TCPNoDelay");
                            var ackFreq = subKey.GetValue("TcpAckFrequency");
                            if (tcpNoDelay != null && Convert.ToInt32(tcpNoDelay) == 1 &&
                                ackFreq != null && Convert.ToInt32(ackFreq) == 1)
                            {
                                optimizedCount++;
                            }
                        }
                    }
                }

                bool isOptimal = totalChecked > 0 && optimizedCount == totalChecked;
                return Task.FromResult(new OptimizationState
                {
                    CurrentValue = isOptimal ? "Enabled (1)" : (optimizedCount > 0 ? $"{optimizedCount}/{totalChecked} Interfaces" : "Default"),
                    RecommendedValue = "Enabled (1)",
                    IsOptimal = isOptimal,
                    IsSupported = true
                });
            }
        }
        catch { }

        return Task.FromResult(new OptimizationState
        {
            CurrentValue = "Default",
            RecommendedValue = "Enabled (1)",
            IsOptimal = false,
            IsSupported = true
        });
    }

    public async Task<OptimizationResult> ApplyAsync()
    {
        try
        {
            await _backup.ExportRegistryKeyAsync(InterfacesKeyPath);
            await _backup.CreateBackupAsync(Id, Name, Category, "Registry", "Interfaces", InterfacesKeyPath);

            using var baseKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces");
            if (baseKey != null)
            {
                foreach (var name in baseKey.GetSubKeyNames())
                {
                    var path = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + name;
                    _registry.SetValue(path, "TcpAckFrequency", 1, RegistryValueKind.DWord);
                    _registry.SetValue(path, "TCPNoDelay", 1, RegistryValueKind.DWord);
                    _registry.SetValue(path, "TcpDelAckTicks", 0, RegistryValueKind.DWord);
                }
            }

            _log.Log("Windows", $"Applied: {Name}", "SUCCESS");
            return OptimizationResult.Succeeded($"{Name} applied successfully across all network interfaces.", "Default", "Enabled (1)");
        }
        catch (Exception ex)
        {
            _log.Log("Windows", $"Failed to apply: {Name}", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed(ex.Message);
        }
    }

    public async Task<OptimizationResult> RestoreAsync()
    {
        try
        {
            using var baseKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces");
            if (baseKey != null)
            {
                foreach (var name in baseKey.GetSubKeyNames())
                {
                    var path = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + name;
                    _registry.DeleteValue(path, "TcpAckFrequency");
                    _registry.DeleteValue(path, "TCPNoDelay");
                    _registry.DeleteValue(path, "TcpDelAckTicks");
                }
            }

            var backups = _backup.GetBackupsByOptimization(Id);
            var latest = backups.FirstOrDefault(b => !b.IsRestored);
            if (latest != null)
            {
                await _backup.RestoreBackupAsync(latest.Id);
            }

            _log.Log("Windows", $"Restored: {Name}", "SUCCESS");
            return OptimizationResult.Succeeded($"{Name} restored to default.");
        }
        catch (Exception ex)
        {
            _log.Log("Windows", $"Failed to restore: {Name}", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed(ex.Message);
        }
    }
}

public class Win32PrioritySeparationOptimization : RegistryOptimizationBase
{
    public override string Id => "win_win32_priority_separation";
    public override string Name => "Foreground CPU Priority Quantum (0x26)";
    public override string Category => "Advance Optimization";
    public override string Description => "Optimizes thread scheduling quantum (0x26) for higher framerates and lower frame variance.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl";
    protected override string ValueName => "Win32PrioritySeparation";
    protected override object RecommendedValueData => 38;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "0x26 (38)";
    protected override object? DefaultValueData => 2;

    public Win32PrioritySeparationOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class DynamicTickOptimization : IOptimization
{
    private readonly IBackupService _backup;
    private readonly ILogService _log;

    public string Id => "win_disable_dynamic_tick";
    public string Name => "Disable Dynamic Tick (Clock Stability)";
    public string Category => "Advance Optimization";
    public string Description => "Disables dynamic tick clock modulation to eliminate micro-stutters and timer latency.";
    public RiskLevel RiskLevel => RiskLevel.Low;
    public bool RequiresAdmin => true;

    public DynamicTickOptimization(IBackupService backup, ILogService log)
    {
        _backup = backup;
        _log = log;
    }

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public async Task<OptimizationState> GetCurrentStateAsync()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("bcdedit.exe", "/enum")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p != null)
            {
                var output = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();
                bool isDisabled = output.IndexOf("disabledynamictick", StringComparison.OrdinalIgnoreCase) >= 0
                    && output.IndexOf("Yes", StringComparison.OrdinalIgnoreCase) >= 0;

                return new OptimizationState
                {
                    CurrentValue = isDisabled ? "Disabled (Yes)" : "Enabled (Default)",
                    RecommendedValue = "Disabled (Yes)",
                    IsOptimal = isDisabled,
                    IsSupported = true
                };
            }
        }
        catch { }

        return new OptimizationState
        {
            CurrentValue = "Enabled (Default)",
            RecommendedValue = "Disabled (Yes)",
            IsOptimal = false,
            IsSupported = true
        };
    }

    public async Task<OptimizationResult> ApplyAsync()
    {
        try
        {
            var state = await GetCurrentStateAsync();
            await _backup.CreateBackupAsync(Id, Name, Category, "Command", state.CurrentValue, "BCD disabledynamictick");

            var psi = new System.Diagnostics.ProcessStartInfo("bcdedit.exe", "/set disabledynamictick yes")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p != null)
            {
                await p.WaitForExitAsync();
                if (p.ExitCode == 0)
                {
                    _log.Log("Windows", $"Applied: {Name}", "SUCCESS");
                    return OptimizationResult.Succeeded($"{Name} applied successfully.", state.CurrentValue, "Disabled (Yes)");
                }
            }
            return OptimizationResult.Failed("Failed to execute bcdedit. Administrator privileges required.");
        }
        catch (Exception ex)
        {
            _log.Log("Windows", $"Failed to apply: {Name}", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed(ex.Message);
        }
    }

    public async Task<OptimizationResult> RestoreAsync()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("bcdedit.exe", "/deletevalue disabledynamictick")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p != null)
            {
                await p.WaitForExitAsync();
            }

            var backups = _backup.GetBackupsByOptimization(Id);
            var latest = backups.FirstOrDefault(b => !b.IsRestored);
            if (latest != null)
            {
                await _backup.RestoreBackupAsync(latest.Id);
            }

            _log.Log("Windows", $"Restored: {Name}", "SUCCESS");
            return OptimizationResult.Succeeded($"{Name} restored to default.");
        }
        catch (Exception ex)
        {
            _log.Log("Windows", $"Failed to restore: {Name}", "ERROR", errorMessage: ex.Message);
            return OptimizationResult.Failed(ex.Message);
        }
    }
}

public class QosDriverPriorityOptimization : RegistryOptimizationBase
{
    public override string Id => "win_qos_driver_prio";
    public override string Name => "QoS Packet Scheduler High Priority";
    public override string Category => "Advance Optimization";
    public override string Description => "Sets Packet Scheduler to boot priority to prevent packet throttling in online games.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => true;
    protected override string RegistryKeyPath => @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Psched";
    protected override string ValueName => "Start";
    protected override object RecommendedValueData => 1;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "System Start (1)";
    protected override object? DefaultValueData => 2;

    public QosDriverPriorityOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }
}

public class TrueFullscreenFseOptimization : RegistryOptimizationBase
{
    public override string Id => "win_true_fullscreen_fse";
    public override string Name => "True Exclusive Fullscreen (Disable FSO)";
    public override string Category => "Advance Optimization";
    public override string Description => "Disables Fullscreen Optimizations so DirectX games run in true exclusive fullscreen.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\System\GameConfigStore";
    protected override string ValueName => "GameDVR_FSEBehaviorMode";
    protected override object RecommendedValueData => 2;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Exclusive FSE (2)";
    protected override object? DefaultValueData => 0;

    public TrueFullscreenFseOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(RegistryKeyPath, "GameDVR_HonorUserFSEBehaviorMode", 1, RegistryValueKind.DWord);
            Registry.SetValue(RegistryKeyPath, "GameDVR_DXGIHonorFSEWindowsCompatible", 1, RegistryValueKind.DWord);
            Registry.SetValue(RegistryKeyPath, "GameDVR_EFSEFeatureFlags", 0, RegistryValueKind.DWord);
            Registry.SetValue(RegistryKeyPath, "GameDVR_FSEBehavior", 2, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.SetValue(RegistryKeyPath, "GameDVR_HonorUserFSEBehaviorMode", 0, RegistryValueKind.DWord);
            Registry.SetValue(RegistryKeyPath, "GameDVR_DXGIHonorFSEWindowsCompatible", 0, RegistryValueKind.DWord);
            Registry.SetValue(RegistryKeyPath, "GameDVR_EFSEFeatureFlags", 0, RegistryValueKind.DWord);
            Registry.SetValue(RegistryKeyPath, "GameDVR_FSEBehavior", 0, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }
}

public class OfflineStartSearchOptimization : RegistryOptimizationBase
{
    public override string Id => "win_disable_bing_search";
    public override string Name => "Offline Start Menu Search (Disable Bing)";
    public override string Category => "Low-End PC";
    public override string Description => "Disables Bing web search in Start Menu to save RAM, eliminate search lag, and speed up PC.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search";
    protected override string ValueName => "BingSearchEnabled";
    protected override object RecommendedValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";
    protected override object? DefaultValueData => 1;

    public OfflineStartSearchOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent", 0, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb", 0, RegistryValueKind.DWord);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch", 1, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent", 1, RegistryValueKind.DWord);
            Registry.DeleteValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions");
            Registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb");
            Registry.DeleteValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch");
        }
        catch { }
        return res;
    }
}

public class MemoryCompressionOptimization : IOptimization
{
    private readonly IBackupService _backup;
    private readonly ILogService _log;
    private static bool? _cachedIsOptimal;

    public string Id => "win_disable_memory_compression";
    public string Name => "Disable Windows Memory Compression";
    public string Category => "Low-End PC";
    public string Description => "Disables RAM compression to prevent CPU usage spikes on budget dual and quad-core CPUs.";
    public RiskLevel RiskLevel => RiskLevel.Low;
    public bool RequiresAdmin => true;

    public MemoryCompressionOptimization(IBackupService backup, ILogService log)
    {
        _backup = backup;
        _log = log;
    }

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public Task<OptimizationState> GetCurrentStateAsync()
    {
        if (_cachedIsOptimal.HasValue)
        {
            return Task.FromResult(new OptimizationState
            {
                CurrentValue = _cachedIsOptimal.Value ? "Disabled (False)" : "Enabled (True)",
                RecommendedValue = "Disabled (False)",
                IsOptimal = _cachedIsOptimal.Value,
                IsSupported = true
            });
        }

        try
        {
            // Direct WMI method call takes < 50ms (avoids 2500ms powershell process startup)
            using var mc = new System.Management.ManagementClass(@"Root\Microsoft\Windows\PS_MMAgent", "PS_MMAgent", null);
            using var outParams = mc.InvokeMethod("Get", null, null);
            if (outParams != null && outParams["CmdletOutput"] is System.Management.ManagementBaseObject obj)
            {
                bool mcEnabled = Convert.ToBoolean(obj["MemoryCompression"]);
                bool isCompressionDisabled = !mcEnabled;
                _cachedIsOptimal = isCompressionDisabled;
                return Task.FromResult(new OptimizationState
                {
                    CurrentValue = isCompressionDisabled ? "Disabled (False)" : "Enabled (True)",
                    RecommendedValue = "Disabled (False)",
                    IsOptimal = isCompressionDisabled,
                    IsSupported = true
                });
            }
        }
        catch { }

        return Task.FromResult(new OptimizationState
        {
            CurrentValue = "Enabled (Default)",
            RecommendedValue = "Disabled (False)",
            IsOptimal = false,
            IsSupported = true
        });
    }

    public Task<OptimizationResult> ApplyAsync()
    {
        _cachedIsOptimal = true;
        _ = Task.Run(() =>
        {
            bool wmiSuccess = false;
            try
            {
                using var mc = new System.Management.ManagementClass(@"Root\Microsoft\Windows\PS_MMAgent", "PS_MMAgent", null);
                using var inParams = mc.GetMethodParameters("Disable");
                inParams["MemoryCompression"] = true;
                mc.InvokeMethod("Disable", inParams, null);
                wmiSuccess = true;
            }
            catch { }

            if (!wmiSuccess)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command Disable-MMAgent -mc")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = System.Diagnostics.Process.Start(psi);
                    p?.WaitForExit(3000);
                }
                catch { }
            }
        });

        _log.Log("Windows", $"Applied: {Name}", "SUCCESS");
        return Task.FromResult(OptimizationResult.Succeeded($"{Name} applied successfully.", "Enabled (True)", "Disabled (False)"));
    }

    public Task<OptimizationResult> RestoreAsync()
    {
        _cachedIsOptimal = false;
        _ = Task.Run(() =>
        {
            bool wmiSuccess = false;
            try
            {
                using var mc = new System.Management.ManagementClass(@"Root\Microsoft\Windows\PS_MMAgent", "PS_MMAgent", null);
                using var inParams = mc.GetMethodParameters("Enable");
                inParams["MemoryCompression"] = true;
                mc.InvokeMethod("Enable", inParams, null);
                wmiSuccess = true;
            }
            catch { }

            if (!wmiSuccess)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command Enable-MMAgent -mc")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = System.Diagnostics.Process.Start(psi);
                    p?.WaitForExit(3000);
                }
                catch { }
            }
        });

        _log.Log("Windows", $"Restored: {Name}", "SUCCESS");
        return Task.FromResult(OptimizationResult.Succeeded($"{Name} restored to default."));
    }
}

public class GameBarPresenceWriterOptimization : RegistryOptimizationBase
{
    public override string Id => "win_disable_gamebar_presence_writer";
    public override string Name => "Disable GameBar Presence Daemon";
    public override string Category => "Low-End PC";
    public override string Description => "Stops GameBar.PresenceWriter background process to conserve CPU on lower-spec systems.";
    public override RiskLevel RiskLevel => RiskLevel.Low;
    public override bool RequiresAdmin => false;
    protected override string RegistryKeyPath => @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR";
    protected override string ValueName => "AppCaptureEnabled";
    protected override object RecommendedValueData => 0;
    protected override RegistryValueKind ValueKind => RegistryValueKind.DWord;
    protected override string RecommendedValueDisplay => "Disabled (0)";
    protected override object? DefaultValueData => 1;

    public GameBarPresenceWriterOptimization(IRegistryService registry, IBackupService backup, ILogService log)
        : base(registry, backup, log) { }

    public override async Task<OptimizationResult> ApplyAsync()
    {
        var res = await base.ApplyAsync();
        try
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord);
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("GameBarPresenceWriter"))
            {
                try { p.Kill(); } catch { }
            }
        }
        catch { }
        return res;
    }

    public override async Task<OptimizationResult> RestoreAsync()
    {
        var res = await base.RestoreAsync();
        try
        {
            Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", 1, RegistryValueKind.DWord);
        }
        catch { }
        return res;
    }
}

