using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XenomOptimizer.Models;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

/// <summary>
/// Settings page ViewModel with app configuration options.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILogService _log;

    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _autoBackup = true;
    [ObservableProperty] private string _theme = "Dark";
    [ObservableProperty] private string _accentColor = "Cyan";
    [ObservableProperty] private string _logLevel = "INFO";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _logDirectory = "";
    [ObservableProperty] private string _backupDirectory = "";

    public SettingsViewModel(ISettingsService settingsService, ILogService log, IBackupService backup)
    {
        _settingsService = settingsService;
        _log = log;

        // Load current settings
        var s = settingsService.Settings;
        StartWithWindows = s.StartWithWindows;
        MinimizeToTray = s.MinimizeToTray;
        AutoBackup = s.AutoBackupBeforeOptimization;
        Theme = s.Theme;
        AccentColor = s.AccentColor;
        LogLevel = s.LogLevel;
        LogDirectory = log.LogDirectory;
        BackupDirectory = backup.BackupDirectory;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        var s = _settingsService.Settings;
        s.StartWithWindows = StartWithWindows;
        s.MinimizeToTray = MinimizeToTray;
        s.AutoBackupBeforeOptimization = AutoBackup;
        s.Theme = Theme;
        s.AccentColor = AccentColor;
        s.LogLevel = LogLevel;

        _settingsService.Save();
        StatusMessage = "Settings saved.";
        _log.Log("Settings", "Settings saved", "SUCCESS");
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", LogDirectory);
        }
        catch { }
    }

    [RelayCommand]
    private void OpenBackupFolder()
    {
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", BackupDirectory);
        }
        catch { }
    }
}
