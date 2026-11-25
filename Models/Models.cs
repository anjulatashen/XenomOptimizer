using CommunityToolkit.Mvvm.ComponentModel;

namespace XenomOptimizer.Models;

/// <summary>
/// Represents a storage drive with media type (NVMe SSD, SATA SSD, HDD), capacity, and usage.
/// </summary>
public partial class DiskDriveInfo : ObservableObject
{
    public string DriveLetter { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string MediaType { get; set; } = "SSD";
    public string TotalSizeFormatted { get; set; } = string.Empty;
    public string FreeSizeFormatted { get; set; } = string.Empty;
    public string UsedSizeFormatted { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsagePercentFormatted))]
    private double _usagePercent;

    public double TargetUsagePercent { get; set; }
    public string UsagePercentFormatted => $"{UsagePercent:F0}%";
    public bool IsNvme => MediaType.Contains("NVMe", StringComparison.OrdinalIgnoreCase);
    public bool IsHdd => MediaType.Contains("HDD", StringComparison.OrdinalIgnoreCase);

    // Drive Health Status
    public string HealthText { get; set; } = "Health | Good";
    public string HealthColorHex { get; set; } = "#A3E635";
    public bool IsHealthGood => HealthText.Contains("Good", StringComparison.OrdinalIgnoreCase);
    public bool IsHealthNormal => HealthText.Contains("Normal", StringComparison.OrdinalIgnoreCase);
    public bool IsHealthBad => HealthText.Contains("Bad", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Represents a system information item displayed on the dashboard or system info page.
/// </summary>
public class SystemInfoItem
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = "Detecting...";
    public string Icon { get; set; } = "\uE946"; // Default info icon
    public SystemInfoStatus Status { get; set; } = SystemInfoStatus.Detecting;
}

public enum SystemInfoStatus
{
    Good,
    Warning,
    NotDetected,
    Detecting,
    Error
}

/// <summary>
/// Represents a backup record with metadata about what was backed up and when.
/// </summary>
public class BackupRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string OptimizationId { get; set; } = string.Empty;
    public string OptimizationName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string BackupType { get; set; } = string.Empty; // "Registry", "File", "Setting"
    public string BackupPath { get; set; } = string.Empty;
    public string OriginalValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string WindowsVersion { get; set; } = string.Empty;
    public string? GameLoopVersion { get; set; }
    public bool IsRestored { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Represents a group of backup records created during a single optimization session.
/// </summary>
public class BackupSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string WindowsVersion { get; set; } = string.Empty;
    public string? GameLoopVersion { get; set; }
    public List<BackupRecord> Records { get; set; } = new();
    public int TotalChanges => Records.Count;
    public bool IsFullyRestored => Records.All(r => r.IsRestored);
}

/// <summary>
/// Represents a log entry written by the logging system.
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "INFO"; // INFO, WARNING, ERROR, SUCCESS
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents a cleanup item that can be scanned and cleaned.
/// </summary>
public class CleanupItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public List<string> AdditionalPaths { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool IsSelected { get; set; } = true;
    public bool IsScanned { get; set; }
    public int FileCount { get; set; }
    public Func<long>? CustomCleanAction { get; set; }

    public string FormattedSize => FormatSize(SizeBytes);

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}

/// <summary>
/// Represents a network adapter detected on the system.
/// </summary>
public class NetworkAdapterInfo
{
    public string Name { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public string IPv4Address { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string DnsServers { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Application settings persisted between sessions.
/// </summary>
public class AppSettings
{
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; }
    public bool AutoBackupBeforeOptimization { get; set; } = true;
    public string Theme { get; set; } = "Dark";
    public string AccentColor { get; set; } = "Cyan";
    public string LogLevel { get; set; } = "INFO";
    public bool RestoreLastConfiguration { get; set; }
    public bool HasRunInitialScan { get; set; } = true;
    public bool HasSeenWindowsGuide { get; set; } = false;
    public bool HasSeenOnboardingTaskV2 { get; set; } = false;
    public bool IsDriversInstalledMode { get; set; } = false;
    public bool HasSkippedDriverScanner { get; set; } = false;

    // 6-Step Pipeline Guide Tracking
    public bool GuideStep1DriversCompleted { get; set; } = false;
    public bool GuideStep2ServicesCompleted { get; set; } = false;
    public bool GuideStep3AdvanceCompleted { get; set; } = false;
    public bool GuideStep4RegistryCompleted { get; set; } = false;
    public bool GuideStep5GameLoopCompleted { get; set; } = false;
    public bool GuideStep6CleanupCompleted { get; set; } = false;

    // Navigation and Slide State Persistence
    public string LastActivePage { get; set; } = "Dashboard";
    public int LastSelectedSlideIndex { get; set; } = 0;
    public int ServicesPresetIndex { get; set; } = 0;

    // Optimizer & Frame Gen Persistence
    public int TargetFps { get; set; } = 90;
    public int SelectedQualityIndex { get; set; } = 0;
    public bool IsFrameGenActive { get; set; } = false;
}
