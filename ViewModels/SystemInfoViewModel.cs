using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XenomOptimizer.Models;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

/// <summary>
/// System Information / Diagnostic page showing hardware/software checks.
/// Shows factual information without judging hardware as "bad".
/// </summary>
public partial class SystemInfoViewModel : ObservableObject
{
    private readonly ISystemDetectionService _systemDetection;
    private readonly IGameLoopDetectionService _gameLoop;
    private readonly ILogService _log;

    [ObservableProperty] private bool _isLoading = true;

    public ObservableCollection<SystemCheckItem> Checks { get; } = new();

    public SystemInfoViewModel(ISystemDetectionService systemDetection, IGameLoopDetectionService gameLoop, ILogService log)
    {
        _systemDetection = systemDetection;
        _gameLoop = gameLoop;
        _log = log;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            // CPU
            var cpu = await _systemDetection.GetCpuModelAsync();
            Checks.Add(new SystemCheckItem { Label = "CPU", Value = cpu, Icon = "⚡", Status = "GOOD" });

            // GPU
            var gpu = await _systemDetection.GetGpuModelAsync();
            Checks.Add(new SystemCheckItem { Label = "GPU", Value = gpu, Icon = "🎮", Status = "GOOD" });

            // RAM
            var ram = await _systemDetection.GetRamAmountAsync();
            var ramStatus = "GOOD";
            if (double.TryParse(ram.Replace(" GB", ""), out var ramGb))
            {
                if (ramGb < 4) ramStatus = "WARNING";
            }
            Checks.Add(new SystemCheckItem { Label = "RAM", Value = ram, Icon = "💾", Status = ramStatus,
                Note = ramGb < 8 ? "8 GB or more recommended for GameLoop" : "" });

            // Windows
            var win = await _systemDetection.GetWindowsVersionAsync();
            Checks.Add(new SystemCheckItem { Label = "Windows", Value = win, Icon = "🪟", Status = "GOOD" });

            // GPU Driver
            var driver = await _systemDetection.GetGpuDriverVersionAsync();
            Checks.Add(new SystemCheckItem { Label = "GPU Driver", Value = driver, Icon = "📦",
                Status = driver == "Not Available" ? "WARNING" : "GOOD",
                Note = driver == "Not Available" ? "Could not detect GPU driver version" : "" });

            // Disk space
            var systemDrive = DriveInfo.GetDrives().FirstOrDefault(d => d.RootDirectory.FullName == Path.GetPathRoot(Environment.SystemDirectory));
            if (systemDrive != null)
            {
                var freeGb = systemDrive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                Checks.Add(new SystemCheckItem
                {
                    Label = "System Disk Free Space",
                    Value = $"{freeGb:F1} GB free on {systemDrive.Name}",
                    Icon = "💿",
                    Status = freeGb < 10 ? "WARNING" : "GOOD",
                    Note = freeGb < 10 ? "Low disk space may affect performance" : ""
                });
            }

            // GameLoop
            var glInstalled = await _gameLoop.IsInstalledAsync();
            if (glInstalled)
            {
                var glVersion = await _gameLoop.GetVersionAsync() ?? "Unknown";
                Checks.Add(new SystemCheckItem { Label = "GameLoop", Value = $"Installed (v{glVersion})", Icon = "🎯", Status = "GOOD" });
            }
            else
            {
                Checks.Add(new SystemCheckItem { Label = "GameLoop", Value = "Not Installed", Icon = "🎯", Status = "NOT DETECTED" });
            }

            // Network
            var adapters = await _systemDetection.GetNetworkAdaptersAsync();
            var activeAdapter = adapters.FirstOrDefault(a => a.Status == "Up");
            Checks.Add(new SystemCheckItem
            {
                Label = "Network Adapter",
                Value = activeAdapter?.Name ?? "No active adapter",
                Icon = "🌐",
                Status = activeAdapter != null ? "GOOD" : "WARNING",
                Note = activeAdapter != null ? $"{activeAdapter.ConnectionType} - {activeAdapter.Speed}" : ""
            });

            // Virtualization
            var virtEnabled = await _systemDetection.IsVirtualizationEnabledAsync();
            Checks.Add(new SystemCheckItem
            {
                Label = "Virtualization (VT-x/AMD-V)",
                Value = virtEnabled ? "Enabled" : "Disabled",
                Icon = "🔧",
                Status = virtEnabled ? "GOOD" : "WARNING",
                Note = !virtEnabled ? "Enable in BIOS for better GameLoop performance" : ""
            });

            // Hyper-V
            var hyperv = await _systemDetection.IsHyperVEnabledAsync();
            Checks.Add(new SystemCheckItem
            {
                Label = "Hyper-V",
                Value = hyperv ? "Enabled" : "Disabled",
                Icon = "📦",
                Status = "GOOD",
                Note = hyperv ? "Hyper-V is enabled. GameLoop may use Hyper-V backend." : ""
            });

            // Power Plan
            var powerPlan = await _systemDetection.GetCurrentPowerPlanAsync();
            var isHighPerf = powerPlan.Contains("High", StringComparison.OrdinalIgnoreCase);
            Checks.Add(new SystemCheckItem
            {
                Label = "Power Plan",
                Value = powerPlan,
                Icon = "🔋",
                Status = isHighPerf ? "GOOD" : "WARNING",
                Note = !isHighPerf ? "High Performance plan recommended for gaming" : ""
            });
        }
        catch (Exception ex)
        {
            _log.Log("SystemInfo", "Diagnostic scan failed", "ERROR", errorMessage: ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public class SystemCheckItem
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string Icon { get; set; } = "ℹ";
    public string Status { get; set; } = "GOOD"; // GOOD, WARNING, NOT DETECTED
    public string Note { get; set; } = "";

    public string StatusColor => Status switch
    {
        "GOOD" => "#A3E635",
        "WARNING" => "#FBBF24",
        "NOT DETECTED" => "#94A3B8",
        _ => "#94A3B8"
    };
}
