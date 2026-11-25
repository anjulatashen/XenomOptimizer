using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XenomOptimizer.Models;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

/// <summary>
/// Safe cleanup system that scans for temporary files and shows sizes before deletion.
/// Never deletes personal files (Downloads folder is strictly protected).
/// Silently skips files that are currently in use by Windows or other processes.
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
        StatusMessage = "Scanning for cleanable files...";

        try
        {
            await Task.Run(async () =>
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

                // 1. Temporary Files (Windows & User Temp)
                var tempPaths = new List<string>
                {
                    Path.Combine(windowsDir, "Temp"),
                    Path.Combine(localAppData, "Temp")
                };
                var userTempPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!tempPaths.Any(p => p.Equals(userTempPath, StringComparison.OrdinalIgnoreCase)))
                {
                    tempPaths.Add(userTempPath);
                }

                ScanMultipleDirectories(
                    tempPaths,
                    "Temporary Files (Windows & User Temp)",
                    "Temporary files created by Windows and running applications. Locked files are automatically skipped.");

                // 2. Thumbnails Cache
                var explorerDir = Path.Combine(localAppData, "Microsoft", "Windows", "Explorer");
                ScanThumbnails(explorerDir);

                // 3. Recycle Bin
                ScanRecycleBin();

                // 4. Delivery Optimization Files
                ScanDeliveryOptimization();

                // 5. Windows Error Reports & Crash Dumps
                var werPaths = new List<string>
                {
                    Path.Combine(programData, "Microsoft", "Windows", "WER"),
                    Path.Combine(localAppData, "Microsoft", "Windows", "WER"),
                    Path.Combine(localAppData, "CrashDumps")
                };
                ScanMultipleDirectories(
                    werPaths,
                    "Windows Error Reports & Crash Dumps",
                    "Diagnostics files and memory crash dumps generated from application errors.");

                // 6. Tencent & GameLoop AppData Residue (Roaming & Local)
                var tencentPaths = new List<string>
                {
                    Path.Combine(appData, "Tencent"),
                    Path.Combine(localAppData, "Tencent"),
                    Path.Combine(programData, "Tencent")
                };
                ScanMultipleDirectories(
                    tencentPaths,
                    "Tencent & GameLoop AppData (Roaming & Local)",
                    "Residual Tencent & GameLoop configuration files, caches, and logs across user AppData.");

                // 7. Windows Prefetch Cache
                var prefetch = Path.Combine(windowsDir, "Prefetch");
                if (Directory.Exists(prefetch))
                {
                    ScanDirectory(
                        prefetch,
                        "Windows Prefetch Cache",
                        "Prefetch data (*.pf) used to speed up application launches. Automatically rebuilt by Windows.");
                }

                // 8. DirectX Shader Cache
                var shaderCache = Path.Combine(localAppData, "D3DSCache");
                if (Directory.Exists(shaderCache))
                {
                    ScanDirectory(
                        shaderCache,
                        "DirectX Shader Cache",
                        "Cached compiled shaders. Games and apps recompile shaders automatically as needed.");
                }

                // 9. GameLoop Install Temp & Cache
                var glPath = await _gameLoop.GetInstallPathAsync();
                if (!string.IsNullOrEmpty(glPath) && Directory.Exists(glPath))
                {
                    var glPaths = new List<string>
                    {
                        Path.Combine(glPath, "TxGameAssistant", "tmp"),
                        Path.Combine(glPath, "TxGameAssistant", "cache"),
                        Path.Combine(glPath, "ShaderCache"),
                        Path.Combine(glPath, "DxShaderCache")
                    }.Where(Directory.Exists).ToList();

                    if (glPaths.Any())
                    {
                        ScanMultipleDirectories(
                            glPaths,
                            "GameLoop Temporary Files & Cache",
                            "Temporary and cache files created by GameLoop emulator runtime.");
                    }
                }
            });

            var totalBytes = Items.Sum(i => i.SizeBytes);
            TotalSize = FormatSize(totalBytes);
            HasScanned = true;
            StatusMessage = $"Scan complete. Found {TotalSize} of cleanable files (Downloads folder is protected).";
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

        StatusMessage = "Cleaning selected items (skipping locked files)...";

        await Task.Run(() =>
        {
            foreach (var item in selectedItems)
            {
                try
                {
                    long cleaned = 0;
                    if (item.CustomCleanAction != null)
                    {
                        cleaned = item.CustomCleanAction();
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(item.Path))
                        {
                            cleaned += CleanDirectory(item.Path);
                        }
                        if (item.AdditionalPaths != null)
                        {
                            foreach (var p in item.AdditionalPaths)
                            {
                                cleaned += CleanDirectory(p);
                            }
                        }
                    }

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

        StatusMessage = $"Cleaned {FormatSize(totalCleaned)}.{(errors > 0 ? $" {errors} items had in-use files that were safely skipped." : "")}";

        // Re-scan to update sizes
        await ScanAsync();
        IsCleaning = false;
    }

    private void ScanDirectory(string path, string name, string description)
    {
        if (GameLoopCleanerService.IsProtectedPath(path) || !Directory.Exists(path)) return;

        try
        {
            long totalSize = 0;
            int fileCount = 0;

            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (GameLoopCleanerService.IsProtectedPath(file)) continue;
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

    private void ScanMultipleDirectories(List<string> paths, string name, string description)
    {
        long totalSize = 0;
        int fileCount = 0;
        var validPaths = new List<string>();

        foreach (var p in paths)
        {
            if (GameLoopCleanerService.IsProtectedPath(p) || !Directory.Exists(p)) continue;
            validPaths.Add(p);

            try
            {
                foreach (var file in Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories))
                {
                    if (GameLoopCleanerService.IsProtectedPath(file)) continue;
                    try
                    {
                        var fi = new FileInfo(file);
                        totalSize += fi.Length;
                        fileCount++;
                    }
                    catch { }
                }
            }
            catch { }
        }

        if (totalSize > 0 && validPaths.Any())
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Items.Add(new CleanupItem
                {
                    Name = name,
                    Path = validPaths[0],
                    AdditionalPaths = validPaths.Skip(1).ToList(),
                    Description = description,
                    SizeBytes = totalSize,
                    FileCount = fileCount,
                    IsScanned = true,
                    IsSelected = true
                });
            });
        }
    }

    private void ScanThumbnails(string explorerDir)
    {
        if (GameLoopCleanerService.IsProtectedPath(explorerDir) || !Directory.Exists(explorerDir)) return;

        try
        {
            long totalSize = 0;
            int fileCount = 0;

            var thumbFiles = Directory.GetFiles(explorerDir, "thumbcache_*.db")
                .Concat(Directory.GetFiles(explorerDir, "iconcache_*.db"));

            foreach (var f in thumbFiles)
            {
                try
                {
                    var fi = new FileInfo(f);
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
                        Name = "Thumbnails Cache",
                        Path = explorerDir,
                        Description = "Windows thumbnail and icon database. Unlocked cache files will be safely cleared.",
                        SizeBytes = totalSize,
                        FileCount = fileCount,
                        IsScanned = true,
                        IsSelected = true,
                        CustomCleanAction = () =>
                        {
                            long cleaned = 0;
                            try
                            {
                                var files = Directory.GetFiles(explorerDir, "thumbcache_*.db")
                                    .Concat(Directory.GetFiles(explorerDir, "iconcache_*.db"));
                                foreach (var tf in files)
                                {
                                    try
                                    {
                                        var fi = new FileInfo(tf);
                                        long len = fi.Length;
                                        fi.Attributes = FileAttributes.Normal;
                                        fi.Delete();
                                        cleaned += len;
                                    }
                                    catch { } // Skip locked thumbcache files
                                }
                            }
                            catch { }
                            return cleaned;
                        }
                    });
                });
            }
        }
        catch { }
    }

    private void ScanRecycleBin()
    {
        try
        {
            var rbInfo = new GameLoopCleanerService.SHQUERYRBINFO();
            rbInfo.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(GameLoopCleanerService.SHQUERYRBINFO));
            int res = GameLoopCleanerService.SHQueryRecycleBin(null, ref rbInfo);

            if (res == 0 && (rbInfo.i64Size > 0 || rbInfo.i64NumItems > 0))
            {
                long size = rbInfo.i64Size;
                int items = (int)rbInfo.i64NumItems;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Items.Add(new CleanupItem
                    {
                        Name = "Recycle Bin",
                        Path = "RecycleBin",
                        Description = "Files deleted from your computer waiting in the Recycle Bin across all drives.",
                        SizeBytes = size,
                        FileCount = items,
                        IsScanned = true,
                        IsSelected = true,
                        CustomCleanAction = () =>
                        {
                            try
                            {
                                GameLoopCleanerService.SHEmptyRecycleBin(IntPtr.Zero, null, 7);
                            }
                            catch { }
                            return size;
                        }
                    });
                });
            }
        }
        catch { }
    }

    private void ScanDeliveryOptimization()
    {
        long totalSize = 0;
        int fileCount = 0;

        var doCandidatePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "DeliveryOptimization"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Network", "Downloader")
        };

        foreach (var p in doCandidatePaths)
        {
            if (Directory.Exists(p) && !GameLoopCleanerService.IsProtectedPath(p))
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            totalSize += fi.Length;
                            fileCount++;
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        // If DO cache has files, add it
        if (totalSize > 0)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Items.Add(new CleanupItem
                {
                    Name = "Delivery Optimization Files",
                    Path = "DeliveryOptimization",
                    Description = "Microsoft update delivery optimization cache files used for network distribution.",
                    SizeBytes = totalSize,
                    FileCount = fileCount,
                    IsScanned = true,
                    IsSelected = true,
                    CustomCleanAction = () =>
                    {
                        try
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = "powershell.exe",
                                Arguments = "-NoProfile -Command \"Delete-DeliveryOptimizationCache -Force -ErrorAction SilentlyContinue\"",
                                CreateNoWindow = true,
                                UseShellExecute = false,
                                WindowStyle = ProcessWindowStyle.Hidden
                            };
                            using var proc = Process.Start(psi);
                            proc?.WaitForExit(3000);
                        }
                        catch { }
                        return totalSize;
                    }
                });
            });
        }
    }

    private static long CleanDirectory(string path)
    {
        if (GameLoopCleanerService.IsProtectedPath(path) || !Directory.Exists(path))
            return 0;

        long cleaned = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                if (GameLoopCleanerService.IsProtectedPath(file)) continue;

                try
                {
                    var fi = new FileInfo(file);
                    cleaned += fi.Length;
                    fi.Attributes = FileAttributes.Normal;
                    fi.Delete();
                }
                catch
                {
                    // File in use or protected by Windows - silently skip!
                }
            }

            // Clean empty subdirectories
            foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).Reverse())
            {
                if (GameLoopCleanerService.IsProtectedPath(dir)) continue;

                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir);
                }
                catch
                {
                    // In-use directory - skip
                }
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
