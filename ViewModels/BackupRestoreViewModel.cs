using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XenomOptimizer.Models;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

/// <summary>
/// Backup & Restore ViewModel showing all backup sessions with restore capabilities.
/// </summary>
public partial class BackupRestoreViewModel : ObservableObject
{
    private readonly IBackupService _backup;
    private readonly ILogService _log;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "";

    public ObservableCollection<BackupSessionViewModel> Sessions { get; } = new();

    public BackupRestoreViewModel(IBackupService backup, ILogService log)
    {
        _backup = backup;
        _log = log;
        LoadSessions();
    }

    private void LoadSessions()
    {
        Sessions.Clear();
        var sessions = _backup.GetAllSessions();
        foreach (var session in sessions)
        {
            Sessions.Add(new BackupSessionViewModel
            {
                SessionId = session.SessionId,
                Timestamp = session.Timestamp,
                WindowsVersion = session.WindowsVersion,
                GameLoopVersion = session.GameLoopVersion ?? "N/A",
                TotalChanges = session.TotalChanges,
                IsFullyRestored = session.IsFullyRestored,
                Records = new ObservableCollection<BackupRecord>(session.Records),
                Backup = _backup,
                Log = _log,
                Parent = this
            });
        }
    }

    [RelayCommand]
    private async Task RestoreAllAsync()
    {
        await _backup.RestoreAllAsync();
        StatusMessage = "All backups marked as restored. Note: Registry and file changes require re-running the restore from individual optimization pages.";
        _log.Log("Backup", "Restore all requested", "SUCCESS");
        LoadSessions();
    }

    public void Refresh() => LoadSessions();
}

public partial class BackupSessionViewModel : ObservableObject
{
    public string SessionId { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string WindowsVersion { get; set; } = "";
    public string GameLoopVersion { get; set; } = "";
    public int TotalChanges { get; set; }
    [ObservableProperty] private bool _isFullyRestored;
    public ObservableCollection<BackupRecord> Records { get; set; } = new();
    public IBackupService? Backup { get; set; }
    public ILogService? Log { get; set; }
    public BackupRestoreViewModel? Parent { get; set; }

    public string FormattedDate => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

    [RelayCommand]
    private async Task RestoreSessionAsync()
    {
        if (Backup == null) return;
        await Backup.RestoreSessionAsync(SessionId);
        IsFullyRestored = true;
        Log?.Log("Backup", $"Session {SessionId} restored", "SUCCESS");
        Parent?.Refresh();
    }
}
