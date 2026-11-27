using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private static void FocusGameLoopWindow()
    {
        try
        {
            var proc = Process.GetProcessesByName("AndroidEmulatorEn")
                .Concat(Process.GetProcessesByName("AndroidEmulator"))
                .Concat(Process.GetProcessesByName("aow_exe"))
                .FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
            if (proc != null)
            {
                ShowWindow(proc.MainWindowHandle, 9); // SW_RESTORE
                SetForegroundWindow(proc.MainWindowHandle);
            }
        }
        catch { }
    }

    private void EnforceUserRenderEngineAndSettings(string installPath)
    {
        try
        {
            using var cuKey = Registry.CurrentUser.OpenSubKey(@"Software\Tencent\MobileGamePC", true);
            if (cuKey != null)
            {
                // Detect user's configured engine mode (default to OpenGL+ = 2 if not set)
                int engine = 2;
                var curEngineObj = cuKey.GetValue("RendererType") ?? cuKey.GetValue("RenderEngine");
                if (curEngineObj != null && int.TryParse(curEngineObj.ToString(), out int parsed) && parsed > 0)
                {
                    engine = parsed;
                }

                if (engine == 2) // OpenGL+
                {
                    cuKey.SetValue("RendererType", 2, RegistryValueKind.DWord);
                    cuKey.SetValue("RenderEngine", 2, RegistryValueKind.DWord);
                    cuKey.SetValue("RenderMode", 1, RegistryValueKind.DWord);
                    cuKey.SetValue("ForceDirectX", 0, RegistryValueKind.DWord);
                    cuKey.SetValue("EnableGLESv3", 1, RegistryValueKind.DWord);
                    cuKey.SetValue("UseVulkan", 0, RegistryValueKind.DWord);

                    try
                    {
                        using var vmKey = cuKey.CreateSubKey("VM");
                        vmKey.SetValue("RendererType", 2, RegistryValueKind.DWord);
                        vmKey.SetValue("RenderEngine", 2, RegistryValueKind.DWord);
                        vmKey.SetValue("RenderMode", 1, RegistryValueKind.DWord);
                        vmKey.SetValue("ForceDirectX", 0, RegistryValueKind.DWord);
                        vmKey.SetValue("EnableGLESv3", 1, RegistryValueKind.DWord);
                    }
                    catch { }

                    try
                    {
                        using var r100 = cuKey.CreateSubKey(@"RunFalgs\_100");
                        r100.SetValue("RendererEngine", 2, RegistryValueKind.DWord);
                    }
                    catch { }

                    try
                    {
                        using var raow = cuKey.CreateSubKey(@"RunFalgs\aow_100");
                        raow.SetValue("RendererEngine", 2, RegistryValueKind.DWord);
                    }
                    catch { }

                    try
                    {
                        using var lmKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Tencent\MobileGamePC", true);
                        if (lmKey != null)
                        {
                            lmKey.SetValue("ForceDirectX", 0, RegistryValueKind.DWord);
                            lmKey.SetValue("UseVulkan", 0, RegistryValueKind.DWord);
                        }
                    }
                    catch { }
                }

                // Clear Tencent's auto MatchRuleId so Rule 408 is not cached or enforced
                try
                {
                    using var runFlags = cuKey.OpenSubKey("RunFalgs", true);
                    if (runFlags != null)
                    {
                        runFlags.SetValue("MatchRuleId", 0, RegistryValueKind.DWord);
                        runFlags.DeleteValue("MatchRuleApk", false);
                    }
                }
                catch { }

                // Synchronize Config.ini: ensure Renderer = 2 (or engine) and remove enable_vulkan
                string[] candidateInis = {
                    Path.Combine(installPath, "ui", "Config.ini"),
                    Path.Combine(installPath, "TxGameAssistant", "ui", "Config.ini"),
                    Path.Combine(installPath, "Config.ini"),
                    @"C:\Program Files\TxGameAssistant\ui\Config.ini",
                    @"E:\Program Files\TxGameAssistant\ui\Config.ini"
                };

                foreach (var ini in candidateInis)
                {
                    if (File.Exists(ini))
                    {
                        SyncIniEngine(ini, engine);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Log("Launcher", $"Error enforcing render engine settings: {ex.Message}", "WARN");
        }
    }

    private static void SyncIniEngine(string filePath, int engine)
    {
        try
        {
            if (!File.Exists(filePath)) return;
            var encoding = System.Text.Encoding.Default;
            byte[] raw = File.ReadAllBytes(filePath);
            if (raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE)
            {
                encoding = System.Text.Encoding.Unicode;
            }

            var text = encoding.GetString(raw);
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            bool inEngine = false;

            for (int i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    var sec = trimmed[1..^1].Trim().ToLowerInvariant();
                    inEngine = sec == "engine" || sec == "engine_100038";
                    continue;
                }

                if (inEngine)
                {
                    var parts = trimmed.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim().ToLowerInvariant();
                        if (key == "renderer" || key == "render_engine" || key == "renderer_type")
                        {
                            lines[i] = $"{parts[0].Trim()} = {engine}";
                        }
                        else if (key == "render_type")
                        {
                            lines[i] = $"{parts[0].Trim()} = {(engine == 2 ? "opengl" : (engine == 4 ? "vulkan" : "directx"))}";
                        }
                    }
                }

                if (trimmed.StartsWith("enable_vulkan", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = "; enable_vulkan disabled";
                }
            }

            File.WriteAllText(filePath, string.Join("\r\n", lines) + "\r\n", encoding);
        }
        catch { }
    }

    public async Task<bool> LaunchAppAsync(string packageName)
    {
        if (string.Equals(packageName, "appmarket", StringComparison.OrdinalIgnoreCase))
        {
            return await LaunchAppMarketAsync();
        }

        var installPath = await _detection.GetInstallPathAsync();
        if (string.IsNullOrEmpty(installPath)) installPath = @"C:\Program Files\TxGameAssistant";

        // Pre-enforce user's configured engine settings (OpenGL+ / DirectX+) and clear Rule 408
        EnforceUserRenderEngineAndSettings(installPath);

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
            bool isAlreadyRunning = Process.GetProcessesByName("AndroidEmulatorEn").Length > 0 
                                 || Process.GetProcessesByName("AndroidEmulator").Length > 0 
                                 || Process.GetProcessesByName("aow_exe").Length > 0;

            if (isAlreadyRunning)
            {
                _log.Log("Launcher", $"GameLoop is already running with user settings. Launching {packageName} directly via ADB...", "INFO");
                await TrySendAdbLaunchIntentAsync(packageName, uiDir);
                FocusGameLoopWindow();
                return true;
            }

            // Cold start: Launch clean VM (-vm 100) WITHOUT -startpkg to prevent GameLoop from invoking Rule 408 override
            var psi = new ProcessStartInfo
            {
                FileName = selectedExe,
                Arguments = "-vm 100",
                WorkingDirectory = Path.GetDirectoryName(selectedExe) ?? uiDir,
                UseShellExecute = true
            };

            Process.Start(psi);
            _log.Log("Launcher", $"Started GameLoop in clean VM mode (-vm 100) with user's configured engine. Launching {packageName} as soon as VM is ready...", "SUCCESS");

            // Background task to trigger app inside VM as soon as Android/ADB is ready
            _ = Task.Run(async () =>
            {
                await EnsureAppLaunchedAsync(packageName, uiDir);
            });

            return true;
        }
        catch (Exception ex)
        {
            _log.Log("Launcher", $"Error launching package {packageName}: {ex.Message}", "ERROR");
            return false;
        }
    }

    private static string GetAdbLaunchArgs(string packageName)
    {
        return packageName switch
        {
            "com.tencent.ig" => "shell am start -n com.tencent.ig/com.epicgames.ue4.GameActivity",
            "com.pubg.krmobile" => "shell am start -n com.pubg.krmobile/com.epicgames.ue4.GameActivity",
            "com.vng.pubgmobile" => "shell am start -n com.vng.pubgmobile/com.epicgames.ue4.GameActivity",
            "com.pubg.imobile" => "shell am start -n com.pubg.imobile/com.epicgames.ue4.GameActivity",
            "com.tencent.tmgp.pubgmhd" => "shell am start -n com.tencent.tmgp.pubgmhd/com.epicgames.ue4.GameActivity",
            "com.android.settings" => "shell am start -a android.settings.SETTINGS",
            "nextapp.fx" => "shell am start -n nextapp.fx/.FileActivity",
            "com.android.browser" => "shell am start -a android.intent.action.MAIN -c android.intent.category.APP_BROWSER",
            "com.android.vending" => "shell am start -n com.android.vending/.AssetBrowserActivity",
            _ => $"shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1"
        };
    }

    private async Task EnsureAppLaunchedAsync(string packageName, string uiDir)
    {
        string adbExe = Path.Combine(uiDir, "adb.exe");
        if (!File.Exists(adbExe)) adbExe = @"C:\Program Files\TxGameAssistant\ui\adb.exe";
        if (!File.Exists(adbExe)) return;

        string adbArgs = GetAdbLaunchArgs(packageName);

        // Wait up to 25 seconds for the Android VM / ADB to respond
        for (int i = 0; i < 25; i++)
        {
            await Task.Delay(1000);
            try
            {
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
                    if (proc.ExitCode == 0)
                    {
                        _log.Log("Launcher", $"Successfully launched {packageName} via ADB in clean VM!", "SUCCESS");
                        FocusGameLoopWindow();
                        break;
                    }
                }
            }
            catch { }
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
            string adbArgs = GetAdbLaunchArgs(packageName);

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
