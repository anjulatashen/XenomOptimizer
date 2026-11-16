using Microsoft.Win32;

namespace XenomOptimizer.Services;

/// <summary>
/// Provides safe registry read/write operations with proper error handling.
/// Supports both HKLM and HKCU paths using string-based key paths.
/// </summary>
public class RegistryService : IRegistryService
{
    private readonly ILogService _log;

    public RegistryService(ILogService logService)
    {
        _log = logService;
    }

    public object? GetValue(string keyPath, string valueName)
    {
        try
        {
            var (hive, subKey) = ParseKeyPath(keyPath);
            using var key = hive.OpenSubKey(subKey, false);
            return key?.GetValue(valueName);
        }
        catch (Exception ex)
        {
            _log.Log("Registry", $"Failed to read {keyPath}\\{valueName}", "ERROR", errorMessage: ex.Message);
            return null;
        }
    }

    public bool SetValue(string keyPath, string valueName, object value, RegistryValueKind valueKind)
    {
        try
        {
            var (hive, subKey) = ParseKeyPath(keyPath);
            using var key = hive.OpenSubKey(subKey, true) ?? hive.CreateSubKey(subKey, true);
            if (key == null)
            {
                _log.Log("Registry", $"Cannot open/create key: {keyPath}", "ERROR");
                return false;
            }

            key.SetValue(valueName, value, valueKind);
            _log.Log("Registry", $"Set {keyPath}\\{valueName}", "SUCCESS", newValue: value.ToString());
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _log.Log("Registry", $"Access denied: {keyPath}\\{valueName} (admin required)", "ERROR");
            return false;
        }
        catch (Exception ex)
        {
            _log.Log("Registry", $"Failed to set {keyPath}\\{valueName}", "ERROR", errorMessage: ex.Message);
            return false;
        }
    }

    public bool DeleteValue(string keyPath, string valueName)
    {
        try
        {
            var (hive, subKey) = ParseKeyPath(keyPath);
            using var key = hive.OpenSubKey(subKey, true);
            if (key == null) return false;

            key.DeleteValue(valueName, false);
            _log.Log("Registry", $"Deleted {keyPath}\\{valueName}", "SUCCESS");
            return true;
        }
        catch (Exception ex)
        {
            _log.Log("Registry", $"Failed to delete {keyPath}\\{valueName}", "ERROR", errorMessage: ex.Message);
            return false;
        }
    }

    public bool KeyExists(string keyPath)
    {
        try
        {
            var (hive, subKey) = ParseKeyPath(keyPath);
            using var key = hive.OpenSubKey(subKey, false);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    public bool ValueExists(string keyPath, string valueName)
    {
        try
        {
            var (hive, subKey) = ParseKeyPath(keyPath);
            using var key = hive.OpenSubKey(subKey, false);
            return key?.GetValue(valueName) != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ExportKeyAsync(string keyPath, string outputPath)
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"export \"{keyPath}\" \"{outputPath}\" /y",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            var success = process.ExitCode == 0;
            _log.Log("Registry", $"Export {keyPath} -> {outputPath}: {(success ? "OK" : "FAILED")}",
                success ? "SUCCESS" : "ERROR");
            return success;
        }
        catch (Exception ex)
        {
            _log.Log("Registry", $"Export failed for {keyPath}", "ERROR", errorMessage: ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Parses a registry path string into its hive and subkey components.
    /// E.g., "HKEY_CURRENT_USER\Software\Test" -> (Registry.CurrentUser, "Software\Test")
    /// </summary>
    private static (RegistryKey Hive, string SubKey) ParseKeyPath(string keyPath)
    {
        var separatorIndex = keyPath.IndexOf('\\');
        if (separatorIndex < 0)
            throw new ArgumentException($"Invalid registry path: {keyPath}");

        var hiveName = keyPath[..separatorIndex].ToUpperInvariant();
        var subKey = keyPath[(separatorIndex + 1)..];

        var hive = hiveName switch
        {
            "HKEY_CURRENT_USER" or "HKCU" => Registry.CurrentUser,
            "HKEY_LOCAL_MACHINE" or "HKLM" => Registry.LocalMachine,
            "HKEY_CLASSES_ROOT" or "HKCR" => Registry.ClassesRoot,
            "HKEY_USERS" or "HKU" => Registry.Users,
            "HKEY_CURRENT_CONFIG" or "HKCC" => Registry.CurrentConfig,
            _ => throw new ArgumentException($"Unknown registry hive: {hiveName}")
        };

        return (hive, subKey);
    }
}
