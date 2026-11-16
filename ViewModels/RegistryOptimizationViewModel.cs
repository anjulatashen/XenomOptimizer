using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XenomOptimizer.Models;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

/// <summary>
/// ViewModel for the Registry Optimization page.
/// Shows each registry tweak with path, current value, recommended value, and risk level.
/// </summary>
public partial class RegistryOptimizationViewModel : ObservableObject
{
    private readonly IRegistryService _registry;
    private readonly IBackupService _backup;
    private readonly ILogService _log;
    private readonly IAdminService _admin;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _statusMessage = "";

    public ObservableCollection<RegistryTweakViewModel> Tweaks { get; } = new();

    public RegistryOptimizationViewModel(IRegistryService registry, IBackupService backup, ILogService log, IAdminService admin)
    {
        _registry = registry;
        _backup = backup;
        _log = log;
        _admin = admin;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        await Task.Yield();
        try
        {
            var tweaks = GetRegistryTweaks();
            foreach (var tweak in tweaks)
            {
                var currentValue = _registry.GetValue(tweak.RegistryPath, tweak.ValueName);
                tweak.CurrentValue = currentValue?.ToString() ?? "Not Set";
                tweak.IsOptimal = tweak.CurrentValue == tweak.RecommendedValue;
                Tweaks.Add(tweak);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private List<RegistryTweakViewModel> GetRegistryTweaks()
    {
        return new List<RegistryTweakViewModel>
        {
            new()
            {
                Name = "Disable Nagle's Algorithm",
                Category = "Network / Latency",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\MSMQ\Parameters",
                ValueName = "TCPNoDelay",
                RecommendedValue = "1",
                DataType = "DWORD",
                RiskLevel = RiskLevel.Low,
                RequiresAdmin = true,
                Description = "Disables TCP buffering (Nagle's algorithm) which can add latency to small packets. Useful for real-time games where low latency is more important than bandwidth efficiency.",
                Registry = _registry,
                Backup = _backup,
                Log = _log
            },
            new()
            {
                Name = "Network Throttling Index",
                Category = "Network / Latency",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                ValueName = "NetworkThrottlingIndex",
                RecommendedValue = "4294967295",
                DataType = "DWORD",
                RiskLevel = RiskLevel.Low,
                RequiresAdmin = true,
                Description = "Windows throttles non-multimedia network traffic by default. Setting to max (0xFFFFFFFF) disables this throttling, which can improve network performance for games.",
                Registry = _registry,
                Backup = _backup,
                Log = _log
            },
            new()
            {
                Name = "System Responsiveness",
                Category = "Performance",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                ValueName = "SystemResponsiveness",
                RecommendedValue = "0",
                DataType = "DWORD",
                RiskLevel = RiskLevel.Medium,
                RequiresAdmin = true,
                Description = "Controls the percentage of CPU resources guaranteed to low-priority tasks. Setting to 0 allows games to use all available CPU time. Default is 20.",
                Registry = _registry,
                Backup = _backup,
                Log = _log
            },
            new()
            {
                Name = "Game Scheduling Priority",
                Category = "Gaming",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                ValueName = "Scheduling Category",
                RecommendedValue = "High",
                DataType = "STRING",
                RiskLevel = RiskLevel.Low,
                RequiresAdmin = true,
                Description = "Sets the MMCSS (Multimedia Class Scheduler Service) category for games to High, giving game threads higher scheduling priority.",
                Registry = _registry,
                Backup = _backup,
                Log = _log
            },
            new()
            {
                Name = "Game SFIO Priority",
                Category = "Gaming",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                ValueName = "SFIO Priority",
                RecommendedValue = "High",
                DataType = "STRING",
                RiskLevel = RiskLevel.Low,
                RequiresAdmin = true,
                Description = "Sets the Superfetch I/O priority for game processes to High, reducing disk I/O latency when loading game assets.",
                Registry = _registry,
                Backup = _backup,
                Log = _log
            },
            new()
            {
                Name = "Disable Full-Screen Optimizations",
                Category = "GPU",
                RegistryPath = @"HKEY_CURRENT_USER\System\GameConfigStore",
                ValueName = "GameDVR_DXGIHonorPlayEnhancedMode",
                RecommendedValue = "0",
                DataType = "DWORD",
                RiskLevel = RiskLevel.Low,
                RequiresAdmin = false,
                Description = "Disables Windows full-screen optimizations (DWM flip model) which can add input latency. Some applications may benefit from true exclusive fullscreen mode.",
                Registry = _registry,
                Backup = _backup,
                Log = _log
            },
        };
    }

    [RelayCommand]
    private async Task ApplyAllAsync()
    {
        await _backup.StartSessionAsync();
        int count = 0;
        foreach (var tweak in Tweaks.Where(t => !t.IsOptimal))
        {
            if (tweak.RequiresAdmin && !_admin.IsRunningAsAdmin)
                continue;
            await tweak.ApplyCommand.ExecuteAsync(null);
            count++;
        }
        await _backup.EndSessionAsync();
        StatusMessage = $"Applied {count} registry optimizations.";
    }
}

/// <summary>
/// ViewModel for a single registry tweak with full metadata and apply/restore.
/// </summary>
public partial class RegistryTweakViewModel : ObservableObject
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string RegistryPath { get; set; } = "";
    public string ValueName { get; set; } = "";
    public string RecommendedValue { get; set; } = "";
    public string DataType { get; set; } = "DWORD";
    public RiskLevel RiskLevel { get; set; }
    public bool RequiresAdmin { get; set; }
    public string Description { get; set; } = "";

    [ObservableProperty] private string _currentValue = "";
    [ObservableProperty] private bool _isOptimal;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isBusy;

    public IRegistryService? Registry { get; set; }
    public IBackupService? Backup { get; set; }
    public ILogService? Log { get; set; }

    public string RiskText => RiskLevel switch
    {
        RiskLevel.Low => "Low",
        RiskLevel.Medium => "Medium",
        RiskLevel.High => "High",
        _ => "Unknown"
    };

    public string RiskColor => RiskLevel switch
    {
        RiskLevel.Low => "#A3E635",
        RiskLevel.Medium => "#FBBF24",
        RiskLevel.High => "#EF4444",
        _ => "#94A3B8"
    };

    [RelayCommand]
    public async Task ApplyAsync()
    {
        if (Registry == null || Backup == null || IsBusy) return;
        IsBusy = true;
        try
        {
            // Backup
            await Backup.ExportRegistryKeyAsync(RegistryPath);
            await Backup.CreateBackupAsync($"reg_{ValueName}", Name, "Registry", "Registry",
                CurrentValue, $"{RegistryPath}\\{ValueName}");

            // Apply
            var valueKind = DataType.ToUpper() switch
            {
                "DWORD" => Microsoft.Win32.RegistryValueKind.DWord,
                "STRING" => Microsoft.Win32.RegistryValueKind.String,
                "QWORD" => Microsoft.Win32.RegistryValueKind.QWord,
                _ => Microsoft.Win32.RegistryValueKind.DWord
            };

            object value = DataType.ToUpper() switch
            {
                "DWORD" => uint.TryParse(RecommendedValue, out var dw) ? dw : (object)RecommendedValue,
                "QWORD" => ulong.TryParse(RecommendedValue, out var qw) ? qw : (object)RecommendedValue,
                _ => RecommendedValue
            };

            var success = Registry.SetValue(RegistryPath, ValueName, value, valueKind);
            if (success)
            {
                CurrentValue = RecommendedValue;
                IsOptimal = true;
                StatusMessage = "Applied successfully.";
                Log?.Log("Registry", $"Applied {Name}", "SUCCESS", newValue: RecommendedValue);
            }
            else
            {
                StatusMessage = "Failed. Administrator privileges may be required.";
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

    [RelayCommand]
    public async Task RestoreAsync()
    {
        if (Registry == null || Backup == null || IsBusy) return;
        IsBusy = true;
        try
        {
            var backups = Backup.GetBackupsByOptimization($"reg_{ValueName}");
            var latest = backups.FirstOrDefault(b => !b.IsRestored);
            if (latest == null)
            {
                StatusMessage = "No backup found.";
                return;
            }

            var originalValue = latest.OriginalValue;
            var valueKind = DataType.ToUpper() switch
            {
                "DWORD" => Microsoft.Win32.RegistryValueKind.DWord,
                "STRING" => Microsoft.Win32.RegistryValueKind.String,
                _ => Microsoft.Win32.RegistryValueKind.DWord
            };

            if (originalValue == "Not Set")
            {
                Registry.DeleteValue(RegistryPath, ValueName);
            }
            else
            {
                object value = DataType.ToUpper() switch
                {
                    "DWORD" => uint.TryParse(originalValue, out var dw) ? dw : (object)originalValue,
                    _ => originalValue
                };
                Registry.SetValue(RegistryPath, ValueName, value, valueKind);
            }

            CurrentValue = originalValue;
            IsOptimal = false;
            await Backup.RestoreBackupAsync(latest.Id);
            StatusMessage = "Restored to original value.";
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
