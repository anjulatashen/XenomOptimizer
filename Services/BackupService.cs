using System.Text.Json;
using XenomOptimizer.Models;

namespace XenomOptimizer.Services;

/// <summary>
/// Manages backup and restore of system settings before optimization.
/// Backups are stored as JSON metadata with accompanying .reg files or file copies.
/// </summary>
public class BackupService : IBackupService
{
    private readonly ILogService _log;
    private readonly object _lock = new();
    private BackupSession? _currentSession;
    private List<BackupSession> _sessions = new();

    public string BackupDirectory { get; }
    private string SessionsFile => Path.Combine(BackupDirectory, "sessions.json");

    public BackupService(ILogService logService)
    {
        _log = logService;
        BackupDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XenomOptimizer", "Backups");
        Directory.CreateDirectory(BackupDirectory);
        LoadSessions();
    }

    public Task StartSessionAsync()
    {
        _currentSession = new BackupSession
        {
            WindowsVersion = Environment.OSVersion.VersionString
        };
        return Task.CompletedTask;
    }

    public Task EndSessionAsync()
    {
        if (_currentSession != null && _currentSession.Records.Count > 0)
        {
            _sessions.Add(_currentSession);
            SaveSessions();
            _log.Log("Backup", $"Backup session completed with {_currentSession.TotalChanges} records", "SUCCESS");
        }
        _currentSession = null;
        return Task.CompletedTask;
    }

    public Task<BackupRecord> CreateBackupAsync(string optimizationId, string optimizationName,
        string category, string backupType, string originalValue, string? description = null)
    {
        var record = new BackupRecord
        {
            OptimizationId = optimizationId,
            OptimizationName = optimizationName,
            Category = category,
            BackupType = backupType,
            OriginalValue = originalValue,
            WindowsVersion = Environment.OSVersion.VersionString,
            Description = description
        };

        // Create backup directory for this record
        var recordDir = Path.Combine(BackupDirectory, record.Id);
        Directory.CreateDirectory(recordDir);
        record.BackupPath = recordDir;

        // Save the original value to a file
        var valuePath = Path.Combine(recordDir, "original_value.txt");
        File.WriteAllText(valuePath, originalValue);

        if (_currentSession != null)
        {
            _currentSession.Records.Add(record);
        }
        else
        {
            // Auto-create a session for standalone backups
            var session = new BackupSession
            {
                WindowsVersion = Environment.OSVersion.VersionString,
                Records = { record }
            };
            _sessions.Add(session);
            SaveSessions();
        }

        _log.Log("Backup", $"Created backup for '{optimizationName}'", "SUCCESS",
            previousValue: originalValue);

        return Task.FromResult(record);
    }

    public async Task<string?> ExportRegistryKeyAsync(string registryPath)
    {
        try
        {
            var exportDir = Path.Combine(BackupDirectory, "registry");
            Directory.CreateDirectory(exportDir);

            var fileName = $"reg_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}.reg";
            var exportPath = Path.Combine(exportDir, fileName);

            // Use reg.exe to export the key
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"export \"{registryPath}\" \"{exportPath}\" /y",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(exportPath))
            {
                _log.Log("Backup", $"Exported registry key: {registryPath}", "SUCCESS");
                return exportPath;
            }

            _log.Log("Backup", $"Failed to export registry key: {registryPath}", "WARNING");
            return null;
        }
        catch (Exception ex)
        {
            _log.Log("Backup", $"Registry export error: {registryPath}", "ERROR", errorMessage: ex.Message);
            return null;
        }
    }

    public Task<string?> BackupFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return Task.FromResult<string?>(null);

            var fileBackupDir = Path.Combine(BackupDirectory, "files");
            Directory.CreateDirectory(fileBackupDir);

            var fileName = $"{Path.GetFileName(filePath)}_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
            var backupPath = Path.Combine(fileBackupDir, fileName);

            File.Copy(filePath, backupPath, true);
            _log.Log("Backup", $"Backed up file: {filePath}", "SUCCESS");
            return Task.FromResult<string?>(backupPath);
        }
        catch (Exception ex)
        {
            _log.Log("Backup", $"File backup error: {filePath}", "ERROR", errorMessage: ex.Message);
            return Task.FromResult<string?>(null);
        }
    }

    public Task<bool> RestoreBackupAsync(string backupRecordId)
    {
        foreach (var session in _sessions)
        {
            var record = session.Records.FirstOrDefault(r => r.Id == backupRecordId);
            if (record != null)
            {
                record.IsRestored = true;
                SaveSessions();
                _log.Log("Restore", $"Marked backup '{record.OptimizationName}' as restored", "SUCCESS");
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }

    public Task<bool> RestoreSessionAsync(string sessionId)
    {
        var session = _sessions.FirstOrDefault(s => s.SessionId == sessionId);
        if (session == null) return Task.FromResult(false);

        foreach (var record in session.Records)
        {
            record.IsRestored = true;
        }
        SaveSessions();
        _log.Log("Restore", $"Restored session {sessionId}", "SUCCESS");
        return Task.FromResult(true);
    }

    public Task<bool> RestoreCategoryAsync(string category)
    {
        var found = false;
        foreach (var session in _sessions)
        {
            foreach (var record in session.Records.Where(r => r.Category == category && !r.IsRestored))
            {
                record.IsRestored = true;
                found = true;
            }
        }
        if (found) SaveSessions();
        return Task.FromResult(found);
    }

    public Task<bool> RestoreAllAsync()
    {
        foreach (var session in _sessions)
        {
            foreach (var record in session.Records)
            {
                record.IsRestored = true;
            }
        }
        SaveSessions();
        _log.Log("Restore", "Restored all backups", "SUCCESS");
        return Task.FromResult(true);
    }

    public List<BackupSession> GetAllSessions()
    {
        return _sessions.OrderByDescending(s => s.Timestamp).ToList();
    }

    public List<BackupRecord> GetBackupsByOptimization(string optimizationId)
    {
        return _sessions.SelectMany(s => s.Records)
            .Where(r => r.OptimizationId == optimizationId)
            .OrderByDescending(r => r.Timestamp)
            .ToList();
    }

    private void SaveSessions()
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_sessions, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SessionsFile, json);
            }
            catch (Exception ex)
            {
                _log.Log("Backup", "Failed to save sessions file", "ERROR", errorMessage: ex.Message);
            }
        }
    }

    private void LoadSessions()
    {
        try
        {
            if (File.Exists(SessionsFile))
            {
                var json = File.ReadAllText(SessionsFile);
                _sessions = JsonSerializer.Deserialize<List<BackupSession>>(json) ?? new();
            }
        }
        catch (Exception ex)
        {
            _sessions = new();
            _log.Log("Backup", "Failed to load sessions file, starting fresh", "WARNING", errorMessage: ex.Message);
        }
    }
}
