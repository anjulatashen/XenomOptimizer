using XenomOptimizer.Models;

namespace XenomOptimizer.Services;

/// <summary>
/// Logs all application operations to timestamped log files.
/// Each day creates a new log file in the Logs directory.
/// </summary>
public class LogService : ILogService
{
    private readonly object _lock = new();

    public string LogDirectory { get; }

    public LogService()
    {
        LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "XenomOptimizer", "Logs");
        Directory.CreateDirectory(LogDirectory);
    }

    public void Log(string category, string message, string level = "INFO",
        string? previousValue = null, string? newValue = null, string? errorMessage = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Category = category,
            Message = message,
            Level = level,
            PreviousValue = previousValue,
            NewValue = newValue,
            ErrorMessage = errorMessage
        };

        var logLine = FormatLogEntry(entry);
        var logFile = Path.Combine(LogDirectory, $"xenom_{DateTime.Now:yyyy-MM-dd}.log");

        lock (_lock)
        {
            try
            {
                File.AppendAllText(logFile, logLine + Environment.NewLine);
            }
            catch
            {
                // Logging should never crash the app
            }
        }
    }

    public List<LogEntry> GetRecentLogs(int count = 100)
    {
        var entries = new List<LogEntry>();

        try
        {
            var logFiles = Directory.GetFiles(LogDirectory, "xenom_*.log")
                .OrderByDescending(f => f)
                .Take(7); // Last 7 days

            foreach (var file in logFiles)
            {
                var lines = File.ReadAllLines(file);
                foreach (var line in lines.Reverse())
                {
                    if (entries.Count >= count) break;
                    var entry = ParseLogLine(line);
                    if (entry != null) entries.Add(entry);
                }
                if (entries.Count >= count) break;
            }
        }
        catch
        {
            // Return what we have
        }

        return entries;
    }

    private static string FormatLogEntry(LogEntry entry)
    {
        var parts = new List<string>
        {
            $"[{entry.Timestamp:HH:mm:ss}]",
            $"[{entry.Level}]",
            $"[{entry.Category}]",
            entry.Message
        };

        if (entry.PreviousValue != null)
            parts.Add($"Old: {entry.PreviousValue}");
        if (entry.NewValue != null)
            parts.Add($"New: {entry.NewValue}");
        if (entry.ErrorMessage != null)
            parts.Add($"Error: {entry.ErrorMessage}");

        return string.Join(" | ", parts);
    }

    private static LogEntry? ParseLogLine(string line)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(line)) return null;

            var parts = line.Split(" | ");
            if (parts.Length < 4) return null;

            var timeStr = parts[0].Trim('[', ']');
            var level = parts[1].Trim('[', ']');
            var category = parts[2].Trim('[', ']');
            var message = parts[3];

            return new LogEntry
            {
                Timestamp = DateTime.TryParse(timeStr, out var t) ? t : DateTime.Now,
                Level = level,
                Category = category,
                Message = message,
                PreviousValue = parts.Length > 4 ? parts[4].Replace("Old: ", "") : null,
                NewValue = parts.Length > 5 ? parts[5].Replace("New: ", "") : null,
                ErrorMessage = parts.Length > 6 ? parts[6].Replace("Error: ", "") : null
            };
        }
        catch
        {
            return null;
        }
    }
}
