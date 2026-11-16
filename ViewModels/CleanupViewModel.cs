using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XenomOptimizer.Models;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

/// <summary>
/// Safe cleanup system that scans for temporary files and shows sizes before deletion.
/// Never deletes personal files or unknown files automatically.
/// </summary>
public partial class CleanupViewModel : ObservableObject
{
    private readonly IGameLoopDetectionService _gameLoop;
    private readonly ILogService _log;

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isCleaning;
    [ObservableProperty] private bool _hasScanned;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _totalSize = "0 B";

    public ObservableCollection<CleanupItem> Items { get; } = new();

    public CleanupViewModel(IGameLoopDetectionService gameLoop, ILogService log)
    {
        _gameLoop = gameLoop;
        _log = log;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsScanning = true;
        Items.Clear();
        StatusMessage = "Scanning...";

        try
        {
            await Task.Run(async () =>
            {
                // Windows Temp
                ScanDirectory(
                    Path.GetTempPath(),
                    "Windows Temporary Files",
                    "Temporary files created by Windows and applications. Safe to delete.");

                // User Temp
                var userTemp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                ScanDirectory(
                    Path.Combine(userTemp, "Temp"),
                    "User Temporary Files",
                    "Temporary files in user profile. Safe to delete.");

                // Windows Prefetch (if accessible)
                var prefetch = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
                if (Directory.Exists(prefetch))
                {
                    ScanDirectory(prefetch, "Windows Prefetch Cache",
                        "Prefetch data used to speed up application launches. Windows will rebuild this automatically.");
                }

                // DirectX Shader Cache
                var shaderCache = Path.Combine(userTemp, "D3DSCache");
                if (Directory.Exists(shaderCache))
                {
                    ScanDirectory(shaderCache, "DirectX Shader Cache",
                        "Cached compiled shaders. Games will recompile shaders as needed.");
                }

                // GameLoop temp/cache
                var glPath = await _gameLoop.GetInstallPathAsync();
                if (glPath != null)
                {
                    var glTemp = Path.Combine(glPath, "TxGameAssistant", "tmp");
                    if (Directory.Exists(glTemp))
                    {
                        ScanDirectory(glTemp, "GameLoop Temporary Files",
                            "Temporary files created by GameLoop emulator. Safe to delete when GameLoop is not running.");
                    }

                    var glCache = Path.Combine(glPath, "TxGameAssistant", "cache");
                    if (Directory.Exists(glCache))
                    {
                        ScanDirectory(glCache, "GameLoop Cache",
                            "Cached data from GameLoop. Safe to delete when GameLoop is not running.");
                    }
                }
            });

            var totalBytes = Items.Sum(i => i.SizeBytes);
            TotalSize = FormatSize(totalBytes);
            HasScanned = true;
            StatusMessage = $"Scan complete. Found {TotalSize} of cleanable files.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan error: {ex.Message}";
            _log.Log("Cleanup", "Scan failed", "ERROR", errorMessage: ex.Message);
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task CleanSelectedAsync()
    {
        IsCleaning = true;
        var selectedItems = Items.Where(i => i.IsSelected && i.SizeBytes > 0).ToList();
        long totalCleaned = 0;
        int errors = 0;

        StatusMessage = "Cleaning...";

        await Task.Run(() =>
        {
            foreach (var item in selectedItems)
            {
                try
                {
                    var cleaned = CleanDirectory(item.Path);
                    totalCleaned += cleaned;
                    _log.Log("Cleanup", $"Cleaned {item.Name}: {FormatSize(cleaned)}", "SUCCESS");
                }
                catch (Exception ex)
                {
                    errors++;
                    _log.Log("Cleanup", $"Error cleaning {item.Name}", "ERROR", errorMessage: ex.Message);
                }
            }
        });

        StatusMessage = $"Cleaned {FormatSize(totalCleaned)}.{(errors > 0 ? $" {errors} items had errors (files may be in use)." : "")}";

        // Re-scan to update sizes
        await ScanAsync();
        IsCleaning = false;
    }

    private void ScanDirectory(string path, string name, string description)
    {
        try
        {
            if (!Directory.Exists(path)) return;

            long totalSize = 0;
            int fileCount = 0;

            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var fi = new FileInfo(file);
                    totalSize += fi.Length;
                    fileCount++;
                }
                catch { }
            }

            if (totalSize > 0)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Items.Add(new CleanupItem
                    {
                        Name = name,
                        Path = path,
                        Description = description,
                        SizeBytes = totalSize,
                        FileCount = fileCount,
                        IsScanned = true,
                        IsSelected = true
                    });
                });
            }
        }
        catch { }
    }

    private static long CleanDirectory(string path)
    {
        long cleaned = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var fi = new FileInfo(file);
                    cleaned += fi.Length;
                    fi.Delete();
                }
                catch
                {
                    // File in use or protected - skip
                }
            }

            // Clean empty subdirectories
            foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).Reverse())
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir);
                }
                catch { }
            }
        }
        catch { }
        return cleaned;
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
