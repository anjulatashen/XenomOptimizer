using Microsoft.Win32;

namespace XenomOptimizer.Services;

/// <summary>
/// Detects GameLoop installation by searching multiple possible locations:
/// - Registry uninstall entries
/// - Common installation directories
/// - Running processes
/// Does NOT assume a fixed path.
/// </summary>
public class GameLoopDetectionService : IGameLoopDetectionService
{
    private readonly ILogService _log;
    private readonly IRegistryService _registry;
    private string? _cachedInstallPath;

    public GameLoopDetectionService(ILogService logService, IRegistryService registryService)
    {
        _log = logService;
        _registry = registryService;
    }

    public async Task<bool> IsInstalledAsync()
    {
        var path = await GetInstallPathAsync();
        return path != null;
    }

    public Task<string?> GetInstallPathAsync()
    {
        return Task.Run(() =>
        {
            if (_cachedInstallPath != null && Directory.Exists(_cachedInstallPath))
                return _cachedInstallPath;

            // Strategy 1: Search registry uninstall entries
            var registryPath = FindInRegistry();
            if (registryPath != null)
            {
                _cachedInstallPath = registryPath;
                _log.Log("GameLoop", $"Found GameLoop via registry: {registryPath}", "INFO");
                return registryPath;
            }

            // Strategy 2: Search common installation directories
            var commonPaths = GetCommonInstallPaths();
            foreach (var path in commonPaths)
            {
                if (IsValidGameLoopInstall(path))
                {
                    _cachedInstallPath = path;
                    _log.Log("GameLoop", $"Found GameLoop in common path: {path}", "INFO");
                    return path;
                }
            }

            // Strategy 3: Check running processes for GameLoop
            var processPath = FindFromRunningProcesses();
            if (processPath != null)
            {
                _cachedInstallPath = processPath;
                _log.Log("GameLoop", $"Found GameLoop from running process: {processPath}", "INFO");
                return processPath;
            }

            _log.Log("GameLoop", "GameLoop not found on this system", "INFO");
            return (string?)null;
        });
    }

    public Task<string?> GetVersionAsync()
    {
        return Task.Run(async () =>
        {
            var installPath = await GetInstallPathAsync();
            if (installPath == null) return null;

            // Try to read version from the executable
            try
            {
                var exePaths = new[]
                {
                    Path.Combine(installPath, "AppMarket", "AppMarket.exe"),
                    Path.Combine(installPath, "TxGameAssistant", "AppMarket", "AppMarket.exe"),
                    Path.Combine(installPath, "GameLoop.exe"),
                    Path.Combine(installPath, "AndroidEmulator", "TBSGameLive.exe")
                };

                foreach (var exePath in exePaths)
                {
                    if (File.Exists(exePath))
                    {
                        var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                        if (!string.IsNullOrEmpty(versionInfo.FileVersion))
                            return versionInfo.FileVersion;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Log("GameLoop", "Failed to detect GameLoop version", "WARNING", errorMessage: ex.Message);
            }

            // Try reading version from registry
            try
            {
                string[] uninstallPaths = {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var basePath in uninstallPaths)
                {
                    using var key = Registry.LocalMachine.OpenSubKey(basePath);
                    if (key == null) continue;

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var displayName = subKey?.GetValue("DisplayName")?.ToString() ?? "";
                        if (displayName.Contains("GameLoop", StringComparison.OrdinalIgnoreCase) ||
                            displayName.Contains("TxGameAssistant", StringComparison.OrdinalIgnoreCase))
                        {
                            var version = subKey?.GetValue("DisplayVersion")?.ToString();
                            if (!string.IsNullOrEmpty(version))
                                return version;
                        }
                    }
                }
            }
            catch { }

            return (string?)null;
        });
    }

    public Task<bool> IsPubgInstalledAsync()
    {
        return Task.Run(async () =>
        {
            var installPath = await GetInstallPathAsync();
            if (installPath == null) return false;

            // Look for PUBG configuration or data directories
            var possiblePaths = new[]
            {
                Path.Combine(installPath, "TxGameAssistant", "UI", "com.tencent.ig"),
                Path.Combine(installPath, "TxGameAssistant", "AppMarket", "com.tencent.ig"),
                Path.Combine(installPath, "gameloop", "com.tencent.ig"),
            };

            // Also search for PUBG in Android data directories within the emulator
            try
            {
                var androidDataPaths = Directory.GetDirectories(installPath, "com.tencent.ig",
                    SearchOption.AllDirectories).Take(1);
                if (androidDataPaths.Any()) return true;
            }
            catch { }

            return possiblePaths.Any(Directory.Exists);
        });
    }

    public Task<string?> GetPubgConfigPathAsync()
    {
        return Task.Run(async () =>
        {
            var installPath = await GetInstallPathAsync();
            if (installPath == null) return null;

            // Search for PUBG configuration files
            try
            {
                var configDirs = Directory.GetDirectories(installPath, "com.tencent.ig",
                    SearchOption.AllDirectories);
                foreach (var dir in configDirs)
                {
                    var configFiles = Directory.GetFiles(dir, "*.ini", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(dir, "*.cfg", SearchOption.AllDirectories))
                        .Concat(Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories));

                    foreach (var file in configFiles)
                    {
                        return Path.GetDirectoryName(file);
                    }
                }
            }
            catch { }

            return (string?)null;
        });
    }

    public Task<Dictionary<string, string>> GetGameLoopConfigAsync()
    {
        return Task.Run(async () =>
        {
            var config = new Dictionary<string, string>();
            var installPath = await GetInstallPathAsync();
            if (installPath == null) return config;

            // Search for GameLoop configuration files
            var configFiles = new[]
            {
                Path.Combine(installPath, "TxGameAssistant", "UI", "config.ini"),
                Path.Combine(installPath, "TxGameAssistant", "UI", "vm_configuration.ini"),
                Path.Combine(installPath, "gameloop", "config.ini"),
            };

            // Also search dynamically
            try
            {
                var iniFiles = Directory.GetFiles(installPath, "*.ini", SearchOption.AllDirectories)
                    .Where(f => Path.GetFileName(f).Contains("config", StringComparison.OrdinalIgnoreCase) ||
                                Path.GetFileName(f).Contains("vm_configuration", StringComparison.OrdinalIgnoreCase))
                    .Take(10);

                foreach (var iniFile in iniFiles)
                {
                    config[$"ConfigFile:{Path.GetFileName(iniFile)}"] = iniFile;
                    ParseIniFile(iniFile, config);
                }
            }
            catch { }

            foreach (var configFile in configFiles)
            {
                if (File.Exists(configFile) && !config.ContainsValue(configFile))
                {
                    config[$"ConfigFile:{Path.GetFileName(configFile)}"] = configFile;
                    ParseIniFile(configFile, config);
                }
            }

            return config;
        });
    }

    private string? FindInRegistry()
    {
        string[] uninstallPaths = {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        try
        {
            foreach (var basePath in uninstallPaths)
            {
                using var key = Registry.LocalMachine.OpenSubKey(basePath);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var displayName = subKey?.GetValue("DisplayName")?.ToString() ?? "";
                        if (displayName.Contains("GameLoop", StringComparison.OrdinalIgnoreCase) ||
                            displayName.Contains("Gameloop", StringComparison.OrdinalIgnoreCase) ||
                            displayName.Contains("TxGameAssistant", StringComparison.OrdinalIgnoreCase))
                        {
                            var installLocation = subKey?.GetValue("InstallLocation")?.ToString();
                            if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                                return installLocation;

                            var uninstallString = subKey?.GetValue("UninstallString")?.ToString();
                            if (!string.IsNullOrEmpty(uninstallString))
                            {
                                var dir = Path.GetDirectoryName(uninstallString.Trim('"'));
                                if (dir != null && Directory.Exists(dir))
                                    return dir;
                            }
                        }
                    }
                    catch { continue; }
                }
            }

            // Also check HKCU
            foreach (var basePath in uninstallPaths)
            {
                var hkcuPath = basePath;
                using var key = Registry.CurrentUser.OpenSubKey(hkcuPath);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var displayName = subKey?.GetValue("DisplayName")?.ToString() ?? "";
                        if (displayName.Contains("GameLoop", StringComparison.OrdinalIgnoreCase))
                        {
                            var installLocation = subKey?.GetValue("InstallLocation")?.ToString();
                            if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                                return installLocation;
                        }
                    }
                    catch { continue; }
                }
            }
        }
        catch { }

        return null;
    }

    private List<string> GetCommonInstallPaths()
    {
        var paths = new List<string>();

        // Check all fixed drives
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            var root = drive.RootDirectory.FullName;
            paths.Add(Path.Combine(root, "Program Files", "TxGameAssistant"));
            paths.Add(Path.Combine(root, "Program Files (x86)", "TxGameAssistant"));
            paths.Add(Path.Combine(root, "GameLoop"));
            paths.Add(Path.Combine(root, "TxGameAssistant"));
            paths.Add(Path.Combine(root, "Gameloop"));
            paths.Add(Path.Combine(root, "Program Files", "Gameloop"));
            paths.Add(Path.Combine(root, "Program Files (x86)", "Gameloop"));
        }

        // Check user directories
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLoop"));
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GameLoop"));
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TxGameAssistant"));
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "TxGameAssistant"));

        return paths.Distinct().ToList();
    }

    private static bool IsValidGameLoopInstall(string path)
    {
        if (!Directory.Exists(path)) return false;

        // Look for GameLoop-specific files/directories
        var indicators = new[]
        {
            "AppMarket",
            "TxGameAssistant",
            "AndroidEmulator",
            "GameLoop.exe",
            "gameloop.exe",
        };

        return indicators.Any(indicator =>
            File.Exists(Path.Combine(path, indicator)) ||
            Directory.Exists(Path.Combine(path, indicator)));
    }

    private string? FindFromRunningProcesses()
    {
        var processNames = new[] { "AppMarket", "GameLoader", "aow_exe", "TBSGameLive", "AndroidProcess" };

        try
        {
            foreach (var procName in processNames)
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(procName);
                foreach (var proc in processes)
                {
                    try
                    {
                        var filePath = proc.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            // Navigate up to the GameLoop root
                            var dir = Path.GetDirectoryName(filePath);
                            while (dir != null && !IsValidGameLoopInstall(dir))
                            {
                                dir = Path.GetDirectoryName(dir);
                            }
                            if (dir != null) return dir;
                        }
                    }
                    catch { }
                    finally { proc.Dispose(); }
                }
            }
        }
        catch { }

        return null;
    }

    private static void ParseIniFile(string filePath, Dictionary<string, string> config)
    {
        try
        {
            if (!File.Exists(filePath)) return;

            var section = "";
            foreach (var line in File.ReadAllLines(filePath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                    continue;

                if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                {
                    section = trimmed[1..^1];
                    continue;
                }

                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex > 0)
                {
                    var key = trimmed[..eqIndex].Trim();
                    var value = trimmed[(eqIndex + 1)..].Trim();
                    var fullKey = string.IsNullOrEmpty(section) ? key : $"{section}.{key}";
                    config[fullKey] = value;
                }
            }
        }
        catch { }
    }
}
