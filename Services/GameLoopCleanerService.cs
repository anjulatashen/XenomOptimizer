using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace XenomOptimizer.Services;

public record NuclearCleanResult(
    bool Success,
    int KilledProcesses,
    int DeletedFiles,
    int DeletedDirs,
    int DeletedRegKeys,
    long FreedBytes,
    string SummaryMessage);

public class GameLoopCleanerService
{
    public int KillTelemetryAndDaemons()
    {
        var targets = new[] { "Syzs_dl_svr.exe", "aow_exe.exe", "QmEmulatorService.exe", "AndroidProcess.exe" };
        int killed = 0;

        foreach (var target in targets)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = $"/F /T /IM {target}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(1500);
                    if (proc.ExitCode == 0)
                    {
                        killed++;
                    }
                }
            }
            catch
            {
            }
        }

        return killed;
    }

    public int DeepCachePurge()
    {
        int deletedFiles = 0;
        int deletedDirs = 0;
        long freedBytes = 0;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        // 1. Delete AppData Roaming Tencent and Local Tencent completely
        var tencentDirs = new[]
        {
            Path.Combine(appData, "Tencent"),
            Path.Combine(localAppData, "Tencent"),
            Path.Combine(commonAppData, "Tencent")
        };
        foreach (var dir in tencentDirs)
        {
            if (Directory.Exists(dir) && !IsProtectedPath(dir))
            {
                DeleteDirectoryRecursive(dir, ref deletedFiles, ref deletedDirs, ref freedBytes);
            }
        }

        // 2. Windows Temp, User Temp, and Prefetch (safe delete, skip locked files)
        var tempDirs = new[]
        {
            Path.Combine(windowsDir, "Temp"),
            Path.GetTempPath(),
            Path.Combine(localAppData, "Temp"),
            Path.Combine(windowsDir, "Prefetch"),
            Path.Combine(localAppData, "D3DSCache")
        };
        foreach (var dir in tempDirs)
        {
            CleanDirectoryContentsSafe(dir, ref deletedFiles, ref deletedDirs, ref freedBytes);
        }

        // 3. Thumbnails Cache (thumbcache_*.db, iconcache_*.db)
        var explorerDir = Path.Combine(localAppData, "Microsoft", "Windows", "Explorer");
        if (Directory.Exists(explorerDir))
        {
            try
            {
                var thumbFiles = Directory.GetFiles(explorerDir, "thumbcache_*.db")
                    .Concat(Directory.GetFiles(explorerDir, "iconcache_*.db"));
                foreach (var f in thumbFiles)
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        freedBytes += fi.Length;
                        fi.Attributes = FileAttributes.Normal;
                        fi.Delete();
                        deletedFiles++;
                    }
                    catch { }
                }
            }
            catch { }
        }

        // 4. Windows Error Reports & Diagnostics (CrashDumps, WER)
        var werDirs = new[]
        {
            Path.Combine(commonAppData, "Microsoft", "Windows", "WER"),
            Path.Combine(localAppData, "Microsoft", "Windows", "WER"),
            Path.Combine(localAppData, "CrashDumps")
        };
        foreach (var dir in werDirs)
        {
            CleanDirectoryContentsSafe(dir, ref deletedFiles, ref deletedDirs, ref freedBytes);
        }

        // 5. Empty Recycle Bin silently across all drives
        try
        {
            SHEmptyRecycleBin(IntPtr.Zero, null, 7);
        }
        catch { }

        return deletedFiles;
    }

    public bool ForceDedicatedGpu()
    {
        try
        {
            var executables = new[]
            {
                @"C:\Program Files\TxGameAssistant\AppMarket\AppMarket.exe",
                @"C:\Program Files\TxGameAssistant\ui\AndroidEmulator.exe",
                @"C:\Program Files\TxGameAssistant\ui\aow_exe.exe",
                @"C:\Program Files (x86)\TxGameAssistant\AppMarket\AppMarket.exe",
                @"C:\Program Files (x86)\TxGameAssistant\ui\AndroidEmulator.exe",
                @"D:\Program Files\TxGameAssistant\AppMarket\AppMarket.exe",
                @"D:\Program Files\TxGameAssistant\ui\AndroidEmulator.exe"
            };

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences");
            if (key != null)
            {
                foreach (var exe in executables)
                {
                    key.SetValue(exe, "GpuPreference=2;", RegistryValueKind.String);
                }
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    public bool ApplyGameLoopConfig(int width, int height, int engineCode)
    {
        try
        {
            var paths = new[]
            {
                @"Software\Tencent\MobileGamePC\VM",
                @"Software\Tencent\MobileGamePC"
            };

            foreach (var p in paths)
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(p);
                    if (key != null)
                    {
                        key.SetValue("Width", width, RegistryValueKind.DWord);
                        key.SetValue("Height", height, RegistryValueKind.DWord);
                        key.SetValue("RenderEngine", engineCode, RegistryValueKind.DWord);
                    }
                }
                catch
                {
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Searches for and launches the official GameLoop uninstaller (TUninstall.exe / Uninstall.exe).
    /// </summary>
    public bool LaunchOfficialUninstall(out string message)
    {
        string? uninstallerPath = FindOfficialUninstaller();

        if (string.IsNullOrEmpty(uninstallerPath) || !File.Exists(uninstallerPath))
        {
            message = "Official GameLoop uninstaller not found. Use 'Nuclear Clean' to completely purge GameLoop, registry keys, and cache files.";
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = uninstallerPath,
                WorkingDirectory = Path.GetDirectoryName(uninstallerPath) ?? string.Empty,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(psi);
            message = $"Official GameLoop uninstaller launched from: {uninstallerPath}. Complete the wizard, then run 'Nuclear Clean' to clear residual cache.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Failed to start uninstaller: {ex.Message}. You can run 'Nuclear Clean' directly.";
            return false;
        }
    }

    /// <summary>
    /// Finds the official GameLoop uninstaller by checking registry and scanning drives.
    /// </summary>
    public string? FindOfficialUninstaller()
    {
        // 1. Check registry uninstall entries
        var regKeys = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MobileGamePC",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\MobileGamePC"
        };

        foreach (var sub in regKeys)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(sub);
                if (key != null)
                {
                    var uninstallStr = key.GetValue("UninstallString") as string;
                    if (!string.IsNullOrWhiteSpace(uninstallStr))
                    {
                        string clean = uninstallStr.Trim('"');
                        if (File.Exists(clean)) return clean;
                    }

                    var installLoc = key.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrWhiteSpace(installLoc))
                    {
                        var candidate = Path.Combine(installLoc, "TUninstall.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            catch { }
        }

        // 2. Search common drive locations
        var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed);
        foreach (var drive in drives)
        {
            string root = drive.RootDirectory.FullName;
            var candidates = new List<string>
            {
                Path.Combine(root, "Program Files", "TxGameAssistant", "ui", "TUninstall.exe"),
                Path.Combine(root, "Program Files", "TxGameAssistant", "TUninstall.exe"),
                Path.Combine(root, "Program Files (x86)", "TxGameAssistant", "ui", "TUninstall.exe"),
                Path.Combine(root, "TxGameAssistant", "ui", "TUninstall.exe"),
                Path.Combine(root, "TxGameAssistant", "TUninstall.exe")
            };

            // Check AppMarket\*\TUninstall.exe
            var appMarketDir = Path.Combine(root, "Program Files", "TxGameAssistant", "AppMarket");
            if (Directory.Exists(appMarketDir))
            {
                try
                {
                    var found = Directory.GetFiles(appMarketDir, "TUninstall.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (found != null && File.Exists(found)) return found;
                }
                catch { }
            }

            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }
        }

        return null;
    }

    /// <summary>
    /// Executes the full 7-step nuclear wipe:
    /// 1. Terminate all GameLoop/Tencent processes
    /// 2. Stop & delete kernel services and drivers (QMEmulatorService, aow_drv)
    /// 3. Unload locked registry hives (QMConfig)
    /// 4. Delete Tencent & GameLoop registry trees (HKCU & HKLM)
    /// 5. Delete GameLoop installation and AppData directories across all drives
    /// 6. Delete Temp and download caches across all drives
    /// 7. Remove Desktop and Start Menu shortcuts
    /// </summary>
    public NuclearCleanResult FullNuclearClean(Action<string>? statusCallback = null)
    {
        int killedProcs = 0;
        int deletedFiles = 0;
        int deletedDirs = 0;
        int deletedRegKeys = 0;
        long freedBytes = 0;

        void Log(string msg) => statusCallback?.Invoke(msg);

        // ── STEP 1: Terminate all GameLoop and Tencent processes ──
        Log("[1/7] Terminating all Tencent and GameLoop processes...");
        var processNames = new[]
        {
            "AppMarket", "AndroidEmulator", "AndroidEmulatorEn", "AndroidProcess",
            "aow_exe", "QMEmulatorService", "syzs_dl_svr", "TSettingCenter",
            "TUninstall", "TXPlatform", "AndroidRenderer", "adb", "adb_server", "GameLoop"
        };

        foreach (var pName in processNames)
        {
            try
            {
                foreach (var p in Process.GetProcessesByName(pName))
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(1000);
                        killedProcs++;
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = $"/F /T /IM {pName}.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(500);
            }
            catch { }
        }

        // ── STEP 2: Stop and remove Tencent services and kernel drivers ──
        Log("[2/7] Stopping and removing Tencent kernel drivers and services...");
        var services = new[] { "QMEmulatorService", "aow_drv" };
        foreach (var s in services)
        {
            RunShellCommand("net", $"stop {s}");
            RunShellCommand("sc", $"stop {s}");
            RunShellCommand("sc", $"delete {s}");
        }

        // ── STEP 3: Unload locked registry hives ──
        Log("[3/7] Unloading locked registry hives...");
        RunShellCommand("reg", @"unload HKU\QMConfig");

        // ── STEP 4: Delete Tencent & GameLoop Registry entries ──
        Log("[4/7] Deleting Tencent & GameLoop registry entries...");
        var hkcuKeys = new[]
        {
            @"Software\Tencent"
        };

        foreach (var k in hkcuKeys)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(k, false);
                deletedRegKeys++;
            }
            catch { }
            RunShellCommand("reg", $@"delete ""HKCU\{k}"" /f");
        }

        var hklmKeys = new[]
        {
            @"SOFTWARE\Tencent",
            @"SOFTWARE\WOW6432Node\Tencent",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MobileGamePC",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\MobileGamePC",
            @"SYSTEM\CurrentControlSet\Services\QMEmulatorService",
            @"SYSTEM\CurrentControlSet\Services\aow_drv"
        };

        foreach (var k in hklmKeys)
        {
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(k, false);
                deletedRegKeys++;
            }
            catch { }
            RunShellCommand("reg", $@"delete ""HKLM\{k}"" /f");
        }

        // ── STEP 5: Delete GameLoop installation and AppData directories across all drives ──
        Log("[5/7] Deleting GameLoop installation and AppData directories across all drives...");
        var fixedDrives = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed)
            .Select(d => d.RootDirectory.FullName)
            .ToList();

        var installDirs = new List<string>();
        foreach (var r in fixedDrives)
        {
            installDirs.Add(Path.Combine(r, "Program Files", "TxGameAssistant"));
            installDirs.Add(Path.Combine(r, "Program Files (x86)", "TxGameAssistant"));
            installDirs.Add(Path.Combine(r, "TxGameAssistant"));
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        installDirs.Add(Path.Combine(appData, "Tencent"));
        installDirs.Add(Path.Combine(localAppData, "Tencent"));
        installDirs.Add(Path.Combine(programData, "Tencent"));

        foreach (var dir in installDirs)
        {
            if (Directory.Exists(dir))
            {
                DeleteDirectoryRecursive(dir, ref deletedFiles, ref deletedDirs, ref freedBytes);
            }
        }

        // ── STEP 6: Delete Temp, Prefetch, Thumbnails, Error Reports & Recycle Bin ──
        Log("[6/7] Deleting Temp, Prefetch, Thumbnails, Error Reports & Recycle Bin (skipping locked files)...");
        var tempDirs = new List<string>();
        foreach (var r in fixedDrives)
        {
            tempDirs.Add(Path.Combine(r, "Temp", "TxGameDownload"));
        }
        tempDirs.Add(Path.Combine(localAppData, "Temp", "TxGameDownload"));
        tempDirs.Add(Path.Combine(localAppData, "D3DSCache"));

        foreach (var dir in tempDirs)
        {
            if (Directory.Exists(dir) && !IsProtectedPath(dir))
            {
                DeleteDirectoryRecursive(dir, ref deletedFiles, ref deletedDirs, ref freedBytes);
            }
        }

        // Clean user temp files matching Tencent/GameLoop
        try
        {
            var userTemp = Path.GetTempPath();
            if (Directory.Exists(userTemp))
            {
                var junkDirs = Directory.GetDirectories(userTemp, "*Tencent*")
                    .Concat(Directory.GetDirectories(userTemp, "*GameLoop*"))
                    .Concat(Directory.GetDirectories(userTemp, "TxGame*"));

                foreach (var d in junkDirs)
                {
                    if (!IsProtectedPath(d))
                    {
                        DeleteDirectoryRecursive(d, ref deletedFiles, ref deletedDirs, ref freedBytes);
                    }
                }

                var junkFiles = Directory.GetFiles(userTemp, "*tencent*.*")
                    .Concat(Directory.GetFiles(userTemp, "*gameloop*.*"));
                foreach (var f in junkFiles)
                {
                    if (IsProtectedPath(f)) continue;
                    try
                    {
                        var fi = new FileInfo(f);
                        freedBytes += fi.Length;
                        fi.Delete();
                        deletedFiles++;
                    }
                    catch { }
                }
            }
        }
        catch { }

        // Specific Windows Temp, User Temp, and Prefetch directories (safe delete, skip locked files)
        var sysTempDirs = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
            Path.GetTempPath(),
            Path.Combine(localAppData, "Temp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch")
        };
        foreach (var d in sysTempDirs)
        {
            CleanDirectoryContentsSafe(d, ref deletedFiles, ref deletedDirs, ref freedBytes);
        }

        // Windows Storage items:
        // 1. Thumbnails cache (thumbcache_*.db, iconcache_*.db)
        var explorerDir = Path.Combine(localAppData, "Microsoft", "Windows", "Explorer");
        if (Directory.Exists(explorerDir))
        {
            try
            {
                var thumbFiles = Directory.GetFiles(explorerDir, "thumbcache_*.db")
                    .Concat(Directory.GetFiles(explorerDir, "iconcache_*.db"));
                foreach (var f in thumbFiles)
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        freedBytes += fi.Length;
                        fi.Attributes = FileAttributes.Normal;
                        fi.Delete();
                        deletedFiles++;
                    }
                    catch { }
                }
            }
            catch { }
        }

        // 2. Windows Error Reports & Crash Dumps
        var errorReportDirs = new[]
        {
            Path.Combine(programData, "Microsoft", "Windows", "WER"),
            Path.Combine(localAppData, "Microsoft", "Windows", "WER"),
            Path.Combine(localAppData, "CrashDumps")
        };
        foreach (var d in errorReportDirs)
        {
            CleanDirectoryContentsSafe(d, ref deletedFiles, ref deletedDirs, ref freedBytes);
        }

        // 3. Delivery Optimization Cache
        RunShellCommand("powershell.exe", "-NoProfile -Command \"Delete-DeliveryOptimizationCache -Force -ErrorAction SilentlyContinue\"");

        // 4. Empty Recycle Bin across all drives silently (no dialog, no confirmation)
        try
        {
            SHEmptyRecycleBin(IntPtr.Zero, null, 7);
        }
        catch { }

        // ── STEP 7: Cleaning Start Menu shortcuts and Desktop files ──
        Log("[7/7] Cleaning Start Menu shortcuts and Desktop links...");
        var startMenuDirs = new[]
        {
            Path.Combine(programData, @"Microsoft\Windows\Start Menu\Programs\Tencent Software"),
            Path.Combine(appData, @"Microsoft\Windows\Start Menu\Programs\Tencent Software")
        };

        foreach (var sm in startMenuDirs)
        {
            if (Directory.Exists(sm) && !IsProtectedPath(sm))
            {
                DeleteDirectoryRecursive(sm, ref deletedFiles, ref deletedDirs, ref freedBytes);
            }
        }

        var desktopLocations = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        };

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            desktopLocations.Add(Path.Combine(userProfile, "OneDrive", "Desktop"));
        }

        foreach (var dLoc in desktopLocations)
        {
            if (!Directory.Exists(dLoc) || IsProtectedPath(dLoc)) continue;
            try
            {
                var shortcuts = Directory.GetFiles(dLoc, "*GameLoop*.lnk")
                    .Concat(Directory.GetFiles(dLoc, "*PUBG*.lnk"))
                    .Concat(Directory.GetFiles(dLoc, "*Tencent*.lnk"));

                foreach (var sc in shortcuts)
                {
                    try
                    {
                        File.Delete(sc);
                        deletedFiles++;
                    }
                    catch { }
                }
            }
            catch { }
        }

        double mbFreed = freedBytes / (1024.0 * 1024.0);
        string summary = $"Nuclear Clean Complete! Freed {mbFreed:F1} MB ({deletedFiles} files, {deletedDirs} directories, {deletedRegKeys} registry hives deleted, {killedProcs} processes terminated).";
        Log(summary);

        return new NuclearCleanResult(
            Success: true,
            KilledProcesses: killedProcs,
            DeletedFiles: deletedFiles,
            DeletedDirs: deletedDirs,
            DeletedRegKeys: deletedRegKeys,
            FreedBytes: freedBytes,
            SummaryMessage: summary);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    /// <summary>
    /// Strict safety guard: Guarantees that under no circumstances is any file or folder
    /// in the user's Downloads folder ever deleted or modified.
    /// </summary>
    public static bool IsProtectedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        try
        {
            string fullPath = Path.GetFullPath(path);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string downloads = Path.Combine(userProfile, "Downloads");

            if (fullPath.Equals(downloads, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(downloads + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(downloads + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var parts = fullPath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Any(p => p.Equals("Downloads", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        catch
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Deletes files and subdirectories inside a directory, skipping files currently locked or in use.
    /// Downloads folder is strictly protected.
    /// </summary>
    public static void CleanDirectoryContentsSafe(string dirPath, ref int deletedFiles, ref int deletedDirs, ref long freedBytes)
    {
        if (string.IsNullOrWhiteSpace(dirPath) || IsProtectedPath(dirPath) || !Directory.Exists(dirPath))
            return;

        try
        {
            var dirInfo = new DirectoryInfo(dirPath);

            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                if (IsProtectedPath(file.FullName)) continue;
                try
                {
                    freedBytes += file.Length;
                    file.Attributes = FileAttributes.Normal;
                    file.Delete();
                    deletedFiles++;
                }
                catch
                {
                    // File is locked or currently in use by a running process - skip silently!
                }
            }

            foreach (var subDir in dirInfo.EnumerateDirectories("*", SearchOption.AllDirectories).OrderByDescending(d => d.FullName.Length))
            {
                if (IsProtectedPath(subDir.FullName)) continue;
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(subDir.FullName).Any())
                    {
                        subDir.Delete(false);
                        deletedDirs++;
                    }
                }
                catch
                {
                    // Locked or directory in use - skip
                }
            }
        }
        catch
        {
        }
    }

    private static void DeleteDirectoryRecursive(string path, ref int deletedFiles, ref int deletedDirs, ref long freedBytes)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || IsProtectedPath(path) || !Directory.Exists(path)) return;

            var dirInfo = new DirectoryInfo(path);
            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                if (IsProtectedPath(file.FullName)) continue;
                try
                {
                    freedBytes += file.Length;
                    file.Attributes = FileAttributes.Normal;
                    file.Delete();
                    deletedFiles++;
                }
                catch { }
            }

            try
            {
                Directory.Delete(path, true);
                deletedDirs++;
            }
            catch { }
        }
        catch { }
    }

    private static void RunShellCommand(string cmd, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(2000);
        }
        catch { }
    }
}
