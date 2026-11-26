using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using XenomOptimizer.Models;

namespace XenomOptimizer.Services;

public class GameLoopLauncherService : IGameLoopLauncherService
{
    private readonly IGameLoopDetectionService _detection;
    private readonly ILogService _log;

    public GameLoopLauncherService(IGameLoopDetectionService detection, ILogService log)
    {
        _detection = detection;
        _log = log;
    }

    public async Task<string> DetectActivePubgPackageAsync()
    {
        try
        {
            using var cuKey = Registry.CurrentUser.OpenSubKey(@"Software\Tencent\MobileGamePC");
            string matchApk = cuKey?.OpenSubKey("RunFalgs")?.GetValue("MatchRuleApk")?.ToString() ?? "";
            if (string.IsNullOrEmpty(matchApk))
            {
                matchApk = cuKey?.OpenSubKey("RunFalgs")?.OpenSubKey("_100")?.GetValue("ApkName")?.ToString() ?? "";
            }
            if (string.IsNullOrEmpty(matchApk))
            {
                var history = cuKey?.OpenSubKey("GameHistory")?.GetValue("ReverseOrder")?.ToString() ?? "";
                var first = history.Split('|', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrEmpty(first) && first.StartsWith("com.")) matchApk = first;
            }

            if (!string.IsNullOrEmpty(matchApk))
            {
                if (matchApk.Contains("krmobile")) return "com.pubg.krmobile";
                if (matchApk.Contains("vng")) return "com.vng.pubgmobile";
                if (matchApk.Contains("imobile")) return "com.pubg.imobile";
                if (matchApk.Contains("pubgmhd")) return "com.tencent.tmgp.pubgmhd";
                if (matchApk.Contains("ig")) return "com.tencent.ig";
            }
        }
        catch { }

        await Task.CompletedTask;
        return "com.tencent.ig"; // Default to PUBG Mobile Global
    }

    public async Task<bool> LaunchPubgAsync()
    {
        var pkg = await DetectActivePubgPackageAsync();
        _log.Log("Launcher", $"Launching PUBG Mobile ({pkg})...", "INFO");
        return await LaunchAppAsync(pkg);
    }

    public async Task<bool> LaunchSettingsAsync()
    {
        _log.Log("Launcher", "Launching internal Android Settings (F9 direct)...", "INFO");
        // com.android.settings is the internal Android settings app accessible via F9
        return await LaunchAppAsync("com.android.settings");
    }

    public async Task<bool> LaunchAppMarketAsync()
    {
        var installPath = await _detection.GetInstallPathAsync();
        if (string.IsNullOrEmpty(installPath)) installPath = @"C:\Program Files\TxGameAssistant";

        var candidates = new[]
        {
            Path.Combine(installPath, "AppMarket", "AppMarket.exe"),
            Path.Combine(installPath, "AppMarket.exe"),
            @"C:\Program Files\TxGameAssistant\AppMarket\AppMarket.exe"
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = candidate,
                        WorkingDirectory = Path.GetDirectoryName(candidate) ?? "",
                        UseShellExecute = true
                    });
                    _log.Log("Launcher", $"Launched AppMarket: {candidate}", "SUCCESS");
                    return true;
                }
                catch (Exception ex)
                {
                    _log.Log("Launcher", $"Failed to launch AppMarket: {ex.Message}", "ERROR");
                }
            }
        }

        return false;
    }

    public async Task<bool> LaunchAppAsync(string packageName)
    {
        if (string.Equals(packageName, "appmarket", StringComparison.OrdinalIgnoreCase))
        {
            return await LaunchAppMarketAsync();
        }

        var installPath = await _detection.GetInstallPathAsync();
        if (string.IsNullOrEmpty(installPath)) installPath = @"C:\Program Files\TxGameAssistant";

        string uiDir = Path.Combine(installPath, "ui");
        if (!Directory.Exists(uiDir)) uiDir = installPath;

        var emulatorExes = new[]
        {
            Path.Combine(uiDir, "AndroidEmulatorEn.exe"),
            Path.Combine(uiDir, "AndroidEmulator.exe"),
            Path.Combine(uiDir, "AndroidEmulatorEx.exe"),
            Path.Combine(installPath, "ui", "AndroidEmulatorEn.exe"),
            Path.Combine(installPath, "ui", "AndroidEmulator.exe")
        };

        string? selectedExe = emulatorExes.FirstOrDefault(File.Exists);
        if (selectedExe == null)
        {
            _log.Log("Launcher", "AndroidEmulator executable not found in GameLoop installation", "ERROR");
            return false;
        }

        try
        {
            // 1. Try launching emulator with -vm 100 -startpkg
            var psi = new ProcessStartInfo
            {
                FileName = selectedExe,
                Arguments = $"-vm 100 -startpkg \"{packageName}\"",
                WorkingDirectory = Path.GetDirectoryName(selectedExe) ?? uiDir,
                UseShellExecute = true
            };

            Process.Start(psi);
            _log.Log("Launcher", $"Started {selectedExe} with package: {packageName}", "SUCCESS");

            // 2. If GameLoop is already running, send ADB intent in background to switch directly
            _ = Task.Run(async () =>
            {
                await Task.Delay(1200); // Small grace period
                await TrySendAdbLaunchIntentAsync(packageName, uiDir);
            });

            return true;
        }
        catch (Exception ex)
        {
            _log.Log("Launcher", $"Error launching package {packageName}: {ex.Message}", "ERROR");
            return false;
        }
    }

    private async Task TrySendAdbLaunchIntentAsync(string packageName, string uiDir)
    {
        string adbExe = Path.Combine(uiDir, "adb.exe");
        if (!File.Exists(adbExe))
        {
            adbExe = @"C:\Program Files\TxGameAssistant\ui\adb.exe";
            if (!File.Exists(adbExe)) return;
        }

        try
        {
            // Determine action or component
            string adbArgs = packageName switch
            {
                "com.android.settings" => "shell am start -a android.settings.SETTINGS",
                "nextapp.fx" => "shell am start -n nextapp.fx/.FileActivity",
                "com.android.browser" => "shell am start -a android.intent.action.MAIN -c android.intent.category.APP_BROWSER",
                "com.android.vending" => "shell am start -n com.android.vending/.AssetBrowserActivity",
                _ => $"shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1"
            };

            var psi = new ProcessStartInfo
            {
                FileName = adbExe,
                Arguments = adbArgs,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
            }
        }
        catch { }
    }

    public async Task<bool> InstallLocalApkAsync(string apkPath)
    {
        if (!File.Exists(apkPath)) return false;

        var installPath = await _detection.GetInstallPathAsync();
        if (string.IsNullOrEmpty(installPath)) installPath = @"C:\Program Files\TxGameAssistant";

        string adbExe = Path.Combine(installPath, "ui", "adb.exe");
        if (!File.Exists(adbExe)) adbExe = @"C:\Program Files\TxGameAssistant\ui\adb.exe";

        if (File.Exists(adbExe))
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = adbExe,
                    Arguments = $"install -r \"{apkPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                    return proc.ExitCode == 0;
                }
            }
            catch { }
        }

        // Fallback: Copy to shared directory so user can click it or run via pcyyb_installer
        try
        {
            string sharedDir = @"E:\Temp\TxGameDownload\MobileGamePCShared";
            if (!Directory.Exists(sharedDir)) sharedDir = @"C:\Temp\TxGameDownload\MobileGamePCShared";
            if (Directory.Exists(sharedDir))
            {
                string target = Path.Combine(sharedDir, Path.GetFileName(apkPath));
                File.Copy(apkPath, target, true);
                _log.Log("Launcher", $"Copied APK to GameLoop Shared Folder: {target}", "SUCCESS");
                return true;
            }
        }
        catch { }

        return false;
    }

    public async Task<List<GameLoopAppItem>> GetLauncherAppsAsync()
    {
        var activePubg = await DetectActivePubgPackageAsync();
        string pubgTitle = activePubg switch
        {
            "com.pubg.krmobile" => "PUBG Mobile (Korea/Japan)",
            "com.vng.pubgmobile" => "PUBG Mobile (Vietnam VN)",
            "com.pubg.imobile" => "Battlegrounds Mobile India (BGMI)",
            "com.tencent.tmgp.pubgmhd" => "Game For Peace (Peacekeeper Elite)",
            _ => "PUBG Mobile (Global Edition)"
        };

        var items = new List<GameLoopAppItem>
        {
            new()
            {
                Id = "pubg",
                Name = pubgTitle,
                PackageName = activePubg,
                Description = "Launch PUBG Mobile directly with 120 FPS unlocker, Smart 2K calibration & zero-latency keyboard mapping.",
                BadgeText = "120 FPS • 2K Ready",
                BadgeColor = "#10B981", // Emerald Green
                IconGlyph = "\uE7FC",   // Controller
                ActionButtonText = "Launch Game",
                Category = "Game"
            },
            new()
            {
                Id = "settings",
                Name = "Android System Settings (F9)",
                PackageName = "com.android.settings",
                Description = "Direct access to internal Android VM settings (Language, Apps, Developer Options, Accounts) without pressing F9.",
                BadgeText = "F9 Direct Access",
                BadgeColor = "#00D4FF", // Electric Blue
                IconGlyph = "\uE713",   // Gear / Settings
                ActionButtonText = "Open Settings",
                Category = "System"
            },
            new()
            {
                Id = "files",
                Name = "FX File Explorer & OBB Manager",
                PackageName = "nextapp.fx",
                Description = "Browse Android internal storage, manage /Android/data and /Android/obb files, and paste custom configs directly.",
                BadgeText = "OBB & Storage",
                BadgeColor = "#F59E0B", // Amber Gold
                IconGlyph = "\uED25",   // Folder
                ActionButtonText = "Open File Explorer",
                Category = "Tool"
            },
            new()
            {
                Id = "browser",
                Name = "Android Web Browser",
                PackageName = "com.android.browser",
                Description = "Fast lightweight browser inside Android to download tools, custom files or patches directly into the VM.",
                BadgeText = "Web & Downloads",
                BadgeColor = "#3B82F6", // Blue
                IconGlyph = "\uE12B",   // Globe / Web
                ActionButtonText = "Open Browser",
                Category = "Tool"
            },
            new()
            {
                Id = "appmarket",
                Name = "GameLoop AppMarket (Official)",
                PackageName = "appmarket",
                Description = "Official Tencent AppMarket store interface to update games, install new games, and manage GameLoop accounts.",
                BadgeText = "Tencent Official",
                BadgeColor = "#8B5CF6", // Purple
                IconGlyph = "\uE719",   // Store / Bag
                ActionButtonText = "Open AppMarket",
                Category = "Store"
            },
            new()
            {
                Id = "playstore",
                Name = "Google Play Store & Services",
                PackageName = "com.android.vending",
                Description = "Access Google Play Services to update accounts, verify licenses, and download Google Play games.",
                BadgeText = "Google Services",
                BadgeColor = "#EC4899", // Pink
                IconGlyph = "\uE896",   // Download / Store
                ActionButtonText = "Open Play Store",
                Category = "Store"
            },
            new()
            {
                Id = "install_apk",
                Name = "Install External APK File",
                PackageName = "",
                Description = "Select any .apk or .xapk package from your PC to sideload and install directly into the GameLoop emulator.",
                BadgeText = "Direct Sideload",
                BadgeColor = "#14B8A6", // Teal
                IconGlyph = "\uE898",   // Package / Install
                ActionButtonText = "Browse APK File...",
                Category = "Utility",
                IsSpecialAction = true,
                SpecialActionId = "install_apk"
            }
        };

        return items;
    }
}
