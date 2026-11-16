using XenomOptimizer.Models;

namespace XenomOptimizer.Services;

/// <summary>Service for logging all operations performed by the application.</summary>
public interface ILogService
{
    void Log(string category, string message, string level = "INFO", string? previousValue = null, string? newValue = null, string? errorMessage = null);
    List<LogEntry> GetRecentLogs(int count = 100);
    string LogDirectory { get; }
}

/// <summary>Service for creating and restoring backups of system settings.</summary>
public interface IBackupService
{
    Task<BackupRecord> CreateBackupAsync(string optimizationId, string optimizationName, string category, string backupType, string originalValue, string? description = null);
    Task<bool> RestoreBackupAsync(string backupRecordId);
    Task<bool> RestoreSessionAsync(string sessionId);
    Task<bool> RestoreCategoryAsync(string category);
    Task<bool> RestoreAllAsync();
    List<BackupSession> GetAllSessions();
    List<BackupRecord> GetBackupsByOptimization(string optimizationId);
    string BackupDirectory { get; }
    Task StartSessionAsync();
    Task EndSessionAsync();

    /// <summary>Exports a registry key to a .reg file before modification.</summary>
    Task<string?> ExportRegistryKeyAsync(string registryPath);

    /// <summary>Backs up a file before modification.</summary>
    Task<string?> BackupFileAsync(string filePath);
}

/// <summary>Service for detecting system hardware and software information.</summary>
public interface ISystemDetectionService
{
    Task<string> GetCpuModelAsync();
    Task<string> GetGpuModelAsync();
    Task<string> GetRamAmountAsync();
    Task<string> GetRamDetailedSpecAsync();
    Task<string> GetWindowsVersionAsync();
    Task<string> GetGpuDriverVersionAsync();
    Task<string> GetCurrentPowerPlanAsync();
    Task<double> GetRamUsagePercentAsync();
    Task<double> GetCpuUsagePercentAsync();
    Task<string> GetGpuUsageAsync();
    Task<double> GetGpuUsagePercentAsync();
    Task<double> GetDiskUsagePercentAsync();
    Task<bool> IsVirtualizationEnabledAsync();
    Task<bool> IsHyperVEnabledAsync();
    Task<List<NetworkAdapterInfo>> GetNetworkAdaptersAsync();
    Task<List<DiskDriveInfo>> GetStorageDrivesAsync();
    Task<string> GetMotherboardModelAsync();
    Task<(string wifi, string bluetooth)> GetWirelessHardwareAsync();
    void InvalidateHardwareCache();
}

/// <summary>Service for detecting GameLoop installation and configuration.</summary>
public interface IGameLoopDetectionService
{
    Task<bool> IsInstalledAsync();
    Task<string?> GetInstallPathAsync();
    Task<string?> GetVersionAsync();
    Task<bool> IsPubgInstalledAsync();
    Task<string?> GetPubgConfigPathAsync();
    Task<Dictionary<string, string>> GetGameLoopConfigAsync();
}

/// <summary>Service for safe registry operations with backup support.</summary>
public interface IRegistryService
{
    /// <summary>Read a registry value. Returns null if the key/value doesn't exist.</summary>
    object? GetValue(string keyPath, string valueName);

    /// <summary>Set a registry value. The key will be created if it doesn't exist.</summary>
    bool SetValue(string keyPath, string valueName, object value, Microsoft.Win32.RegistryValueKind valueKind);

    /// <summary>Delete a registry value.</summary>
    bool DeleteValue(string keyPath, string valueName);

    /// <summary>Check if a registry key exists.</summary>
    bool KeyExists(string keyPath);

    /// <summary>Check if a specific value exists in a registry key.</summary>
    bool ValueExists(string keyPath, string valueName);

    /// <summary>Export a registry key to a .reg file.</summary>
    Task<bool> ExportKeyAsync(string keyPath, string outputPath);
}

/// <summary>Service for checking and requesting administrator privileges.</summary>
public interface IAdminService
{
    /// <summary>Whether the application is currently running as administrator.</summary>
    bool IsRunningAsAdmin { get; }

    /// <summary>Requests elevation (restarts the app as admin).</summary>
    void RequestElevation();

    /// <summary>Runs a command as administrator.</summary>
    Task<(bool Success, string Output)> RunElevatedCommandAsync(string command, string arguments);
}

/// <summary>Service for persisting and loading application settings.</summary>
public interface ISettingsService
{
    AppSettings Settings { get; }
    void Save();
    void Load();
}

/// <summary>Service for GameLoop emulator and system optimizations.</summary>
public interface IGameLoopOptimizerService
{
    // Category 1: Windows & Emulator Tweaks
    Task<OptimizationResult> SetEmulatorHighPriorityAsync(bool enable);
    Task<OptimizationResult> ForceDedicatedGpuAndVrrAsync(bool enable);
    Task<OptimizationResult> PreferVtxOverHyperVAsync(bool enable);
    Task<OptimizationResult> ApplyEngineRenderBoostAsync(bool enable);
    Task<OptimizationResult> ApplyMmcssGamesPriorityAsync(bool enable);
    Task<OptimizationResult> SetSystemResponsivenessAndNetworkThrottleAsync(bool enable);
    Task<OptimizationResult> DisableGameDvrAndXboxGameBarAsync(bool enable);
    Task<OptimizationResult> DisableDeliveryOptimizationP2PAsync(bool enable);

    // 120 FPS Safe Unlock & Frame Rate Tuning
    Task<OptimizationResult> Unlock120FpsAsync(bool enable);
    Task<bool> Is120FpsUnlockedAsync();
    Task<OptimizationResult> Create120FpsShortcutAsync();
    Task<OptimizationResult> LaunchGameLoop120FpsAsync();

    // One-Tap Batch
    Task<OptimizationResult> ApplyOneTapOptimizationAsync();

    // State queries
    Task<bool> IsEmulatorHighPriorityEnabledAsync();
    Task<bool> IsDedicatedGpuForcedAsync();
    Task<bool> IsVtxPreferredAsync();
    Task<bool> IsEngineRenderBoostEnabledAsync();
    Task<bool> IsMmcssGamesPriorityEnabledAsync();
    Task<bool> IsNetworkThrottlingDisabledAsync();
    Task<bool> IsGameDvrDisabledAsync();
    Task<bool> IsDeliveryOptimizationDisabledAsync();
}
