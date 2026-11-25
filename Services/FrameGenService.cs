using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace XenomOptimizer.Services;

public class FrameGenService
{
    public int CurrentFps { get; private set; } = 120;
    public int CurrentQualityIndex { get; private set; } = 0; // 0=Smooth, 1=Balanced, 2=HD, 3=HDR, 4=Ultra HD

    public static string GetQualityName(int index) => index switch
    {
        0 => "Smooth",
        1 => "Balanced",
        2 => "HD",
        3 => "HDR",
        4 => "Ultra HD",
        _ => "Custom"
    };

    public bool ApplyOptimization(int targetFps, int qualityIndex, out string statusMessage)
    {
        CurrentFps = targetFps;
        CurrentQualityIndex = qualityIndex;

        try
        {
            // 1. Update GameLoop Registry Keys
            const string tencentKey = @"Software\Tencent\MobileGamePC";
            using (var key = Registry.CurrentUser.CreateSubKey(tencentKey))
            {
                if (key != null)
                {
                    // FPS Limit (GameLoop Engine and PUBG configs)
                    key.SetValue("fps_max", targetFps, RegistryValueKind.DWord);
                    key.SetValue("com.tencent.ig_FPSLevel", targetFps, RegistryValueKind.DWord);
                    key.SetValue("com.vng.pubgmobile_FPSLevel", targetFps, RegistryValueKind.DWord);
                    key.SetValue("com.pubg.krmobile_FPSLevel", targetFps, RegistryValueKind.DWord);
                    key.SetValue("com.pubg.imobile_FPSLevel", targetFps, RegistryValueKind.DWord);

                    // Render Quality: 1=Smooth, 2=Balanced, 3=HD, 4=HDR, 5=Ultra HD
                    int renderQuality = qualityIndex + 1;
                    key.SetValue("com.tencent.ig_RenderQuality", renderQuality, RegistryValueKind.DWord);
                    key.SetValue("com.vng.pubgmobile_RenderQuality", renderQuality, RegistryValueKind.DWord);
                    key.SetValue("com.pubg.krmobile_RenderQuality", renderQuality, RegistryValueKind.DWord);
                    key.SetValue("com.pubg.imobile_RenderQuality", renderQuality, RegistryValueKind.DWord);

                    // Content scale: Smooth=1 (maximum speed), Balanced/HD=2, HDR/Ultra=3
                    int contentScale = qualityIndex switch
                    {
                        0 => 1,
                        1 => 2,
                        2 => 2,
                        3 => 3,
                        _ => 3
                    };
                    key.SetValue("com.tencent.ig_ContentScale", contentScale, RegistryValueKind.DWord);

                    // Device Profile Spoofing:
                    // SM-X910 (Samsung Galaxy Tab S9 Ultra) is the official 120Hz whitelisted model for 120 FPS
                    // SM-X920 (Samsung Galaxy Tab S10+) is certified for HDR 90 FPS
                    if (targetFps >= 120)
                    {
                        key.SetValue("VMDeviceManufacturer", "SAMSUNG", RegistryValueKind.String);
                        key.SetValue("VMDeviceModel", "SM-X910", RegistryValueKind.String);
                        key.SetValue("VMPhoneDevice", "Samsung SM-X910", RegistryValueKind.String);
                    }
                    else if (targetFps >= 90)
                    {
                        key.SetValue("VMDeviceManufacturer", "SAMSUNG", RegistryValueKind.String);
                        key.SetValue("VMDeviceModel", "SM-X920", RegistryValueKind.String);
                        key.SetValue("VMPhoneDevice", "Samsung SM-X920", RegistryValueKind.String);
                    }

                    key.SetValue("AdbDisable", 0, RegistryValueKind.DWord);
                    key.SetValue("FPSLock", 0, RegistryValueKind.DWord);
                    key.SetValue("ForceDirectX", 1, RegistryValueKind.DWord);
                    key.SetValue("RenderMode", 4, RegistryValueKind.DWord);
                    key.SetValue("VSyncEnabled", 0, RegistryValueKind.DWord);

                    using (var gameSavesKey = Registry.CurrentUser.CreateSubKey(@"Software\Tencent\MobileGamePC\GameSaves"))
                    {
                        if (gameSavesKey != null)
                        {
                            int gameSaveFpsLevel = targetFps >= 120 ? 5 : (targetFps >= 90 ? 4 : 3);
                            gameSavesKey.SetValue("FpsLevel", gameSaveFpsLevel, RegistryValueKind.DWord);
                        }
                    }
                }
            }

            // 2. Update GameLoop Config.ini if found
            UpdateGameLoopConfigIni(targetFps);

            // 3. Deploy Active.sav Preset to shared game folder if available
            DeployActiveSavPreset(qualityIndex);

            string qName = GetQualityName(qualityIndex);
            statusMessage = $"Active: {targetFps} FPS • {qName} applied to GameLoop!";
            return true;
        }
        catch (Exception ex)
        {
            statusMessage = $"Failed to apply settings: {ex.Message}";
            return false;
        }
    }

    public bool ResetToDefaults(out string statusMessage)
    {
        try
        {
            const string tencentKey = @"Software\Tencent\MobileGamePC";
            using (var key = Registry.CurrentUser.CreateSubKey(tencentKey))
            {
                if (key != null)
                {
                    key.SetValue("fps_max", 90, RegistryValueKind.DWord);
                    key.SetValue("com.tencent.ig_FPSLevel", 90, RegistryValueKind.DWord);
                    key.SetValue("com.tencent.ig_RenderQuality", 1, RegistryValueKind.DWord);
                }
            }
            statusMessage = "Reset to GameLoop standard 90 FPS • Smooth profile.";
            return true;
        }
        catch (Exception ex)
        {
            statusMessage = $"Reset failed: {ex.Message}";
            return false;
        }
    }

    private static void UpdateGameLoopConfigIni(int targetFps)
    {
        try
        {
            var candidateRoots = new List<string>
            {
                @"E:\Program Files\TxGameAssistant",
                @"E:\Program Files\TxGameAssistant\ui",
                @"C:\Program Files\TxGameAssistant",
                @"C:\Program Files (x86)\TxGameAssistant",
                @"D:\Program Files\TxGameAssistant",
                @"E:\Temp\TxGameDownload\Component\UI"
            };

            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
            {
                var r = drive.RootDirectory.FullName;
                candidateRoots.Add(Path.Combine(r, "Program Files", "TxGameAssistant"));
                candidateRoots.Add(Path.Combine(r, "Program Files", "TxGameAssistant", "UI"));
                candidateRoots.Add(Path.Combine(r, "TxGameAssistant"));
                candidateRoots.Add(Path.Combine(r, "TxGameAssistant", "UI"));
            }

            foreach (var root in candidateRoots.Distinct())
            {
                if (!Directory.Exists(root)) continue;

                var iniFiles = new[]
                {
                    Path.Combine(root, "Config.ini"),
                    Path.Combine(root, "UI", "Config.ini"),
                    Path.Combine(root, "AppMarket", "Config.ini"),
                    Path.Combine(root, "vm_configuration.ini")
                };

                foreach (var ini in iniFiles)
                {
                    if (!File.Exists(ini)) continue;
                    try
                    {
                        var lines = File.ReadAllLines(ini).ToList();
                        bool fpsUpdated = false;
                        for (int i = 0; i < lines.Count; i++)
                        {
                            if (lines[i].Trim().StartsWith("fps_max=", StringComparison.OrdinalIgnoreCase) ||
                                lines[i].Trim().StartsWith("fps_max =", StringComparison.OrdinalIgnoreCase))
                            {
                                lines[i] = $"fps_max={targetFps}";
                                fpsUpdated = true;
                            }
                        }
                        if (!fpsUpdated)
                        {
                            lines.Add($"fps_max={targetFps}");
                        }

                        // Also update under [Engine]
                        int engineIdx = lines.FindIndex(l => l.Trim().Equals("[Engine]", StringComparison.OrdinalIgnoreCase));
                        if (engineIdx >= 0)
                        {
                            bool hasEngineFps = false;
                            for (int j = engineIdx + 1; j < lines.Count && !lines[j].Trim().StartsWith("["); j++)
                            {
                                if (lines[j].Trim().StartsWith("fps_max", StringComparison.OrdinalIgnoreCase))
                                {
                                    lines[j] = $"fps_max = {targetFps}";
                                    hasEngineFps = true;
                                    break;
                                }
                            }
                            if (!hasEngineFps)
                            {
                                lines.Insert(engineIdx + 1, $"fps_max = {targetFps}");
                            }
                        }

                        File.WriteAllLines(ini, lines);
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    public bool ApplyActiveSavSettings(
        int targetFps,
        string quality,
        string style,
        string antiAliasing,
        bool powerSaving,
        bool shadows,
        bool adaptiveGraphics,
        bool lobbyEnhancement,
        double brightness,
        double nonStandardScreen,
        out string statusMessage)
    {
        CurrentFps = targetFps;

        try
        {
            // 1. Map parameters to PUBG Active.sav integer codes
            int fpsVal = targetFps switch
            {
                >= 120 => 8,
                >= 90 => 7,
                >= 60 => 6,
                _ => 4 // 30 FPS
            };

            int qVal = quality.ToLowerInvariant() switch
            {
                "super smooth" => 1,
                "smooth" => 1,
                "balanced" => 2,
                "hd" => 3,
                "hdr" => 4,
                "ultra hdr" => 5,
                _ => 1
            };

            int sVal = style.ToLowerInvariant() switch
            {
                "classic" => 1,
                "colorful" => 2,
                "realistic" => 3,
                "soft" => 4,
                "movie" => 5,
                _ => 2
            };

            // 2. Locate base Active.sav template
            string subFolder = quality.ToLowerInvariant() switch
            {
                "super smooth" => "Smooth",
                "smooth" => "Smooth",
                "balanced" => "HD",
                "hd" => "HD",
                "hdr" => "HDR",
                "ultra hdr" => "Ultra HDR",
                _ => "Smooth"
            };

            var sourceCandidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Presets", subFolder, "Active.sav"),
                Path.Combine(@"d:\Shop\Xenom Lag Fix\Assets\Presets", subFolder, "Active.sav"),
                Path.Combine(@"D:\Lag Pix", subFolder + " 120", "Active.sav"),
                Path.Combine(@"D:\Lag Pix", subFolder + " 120", "Normal", "Active.sav"),
                Path.Combine(@"D:\Lag Pix", subFolder, "Active.sav"),
                Path.Combine(@"D:\Lag Pix\Smooth 120\Active.sav")
            };

            string? srcFile = sourceCandidates.FirstOrDefault(File.Exists);
            byte[]? savBytes = null;
            if (srcFile != null)
            {
                savBytes = File.ReadAllBytes(srcFile);
            }

            if (savBytes != null)
            {
                // 3. Binary patch IntProperties inside Active.sav
                PatchIntProperty(savBytes, "BattleFPS", fpsVal);
                PatchIntProperty(savBytes, "LobbyFPS", fpsVal);
                PatchIntProperty(savBytes, "FPSLevel", fpsVal);
                PatchIntProperty(savBytes, "MainCityFPS", fpsVal);
                PatchIntProperty(savBytes, "BattleRenderQuality", qVal);
                PatchIntProperty(savBytes, "LobbyRenderQuality", qVal);
                PatchIntProperty(savBytes, "BattleRenderStyle", sVal);
                PatchIntProperty(savBytes, "LobbyRenderStyle", sVal);
                PatchIntProperty(savBytes, "nEnhancedLobbyQuality", lobbyEnhancement ? 2 : 1);
                PatchIntProperty(savBytes, "FPSFineTuneSwitch", 1);

                // Save patched file to temp staging
                string tempPatchedFile = Path.Combine(Path.GetTempPath(), "Active.sav");
                File.WriteAllBytes(tempPatchedFile, savBytes);

                // Deploy to shared folders
                var targetDestinations = new[]
                {
                    @"E:\Temp\TxGameDownload\MobileGamePCShared\Active.sav",
                    @"C:\Temp\TxGameDownload\MobileGamePCShared\Active.sav",
                    @"D:\Temp\TxGameDownload\MobileGamePCShared\Active.sav",
                    @"D:\Lag Pix\Active.sav"
                };

                foreach (var dest in targetDestinations)
                {
                    try
                    {
                        var dir = Path.GetDirectoryName(dest);
                        if (dir != null && Directory.Exists(dir))
                        {
                            File.Copy(tempPatchedFile, dest, true);
                        }
                    }
                    catch { }
                }

                // Deploy via ADB into running GameLoop Android container
                DeploySavViaAdb(tempPatchedFile);
            }

            // 4. Update GameLoop Windows Registry Keys
            const string tencentKey = @"Software\Tencent\MobileGamePC";
            using (var key = Registry.CurrentUser.CreateSubKey(tencentKey))
            {
                if (key != null)
                {
                    key.SetValue("fps_max", targetFps, RegistryValueKind.DWord);
                    key.SetValue("com.tencent.ig_FPSLevel", targetFps, RegistryValueKind.DWord);
                    key.SetValue("com.vng.pubgmobile_FPSLevel", targetFps, RegistryValueKind.DWord);
                    key.SetValue("com.pubg.krmobile_FPSLevel", targetFps, RegistryValueKind.DWord);
                    key.SetValue("com.pubg.imobile_FPSLevel", targetFps, RegistryValueKind.DWord);

                    key.SetValue("com.tencent.ig_RenderQuality", qVal, RegistryValueKind.DWord);
                    key.SetValue("com.vng.pubgmobile_RenderQuality", qVal, RegistryValueKind.DWord);
                    key.SetValue("com.pubg.krmobile_RenderQuality", qVal, RegistryValueKind.DWord);
                    key.SetValue("com.pubg.imobile_RenderQuality", qVal, RegistryValueKind.DWord);

                    if (targetFps >= 120)
                    {
                        key.SetValue("VMDeviceManufacturer", "SAMSUNG", RegistryValueKind.String);
                        key.SetValue("VMDeviceModel", "SM-X910", RegistryValueKind.String);
                        key.SetValue("VMPhoneDevice", "Samsung SM-X910", RegistryValueKind.String);
                    }
                    else if (targetFps >= 90)
                    {
                        key.SetValue("VMDeviceManufacturer", "SAMSUNG", RegistryValueKind.String);
                        key.SetValue("VMDeviceModel", "SM-X920", RegistryValueKind.String);
                        key.SetValue("VMPhoneDevice", "Samsung SM-X920", RegistryValueKind.String);
                    }

                    key.SetValue("AdbDisable", 0, RegistryValueKind.DWord);
                    key.SetValue("FPSLock", 0, RegistryValueKind.DWord);
                    key.SetValue("ForceDirectX", 1, RegistryValueKind.DWord);
                    key.SetValue("RenderMode", 4, RegistryValueKind.DWord);
                    key.SetValue("VSyncEnabled", 0, RegistryValueKind.DWord);
                }
            }

            // 5. Update GameLoop Config.ini
            UpdateGameLoopConfigIni(targetFps);

            statusMessage = $"Active.sav Applied: {targetFps} FPS • {quality} • {style} injected successfully!";
            return true;
        }
        catch (Exception ex)
        {
            statusMessage = $"Active.sav error: {ex.Message}";
            return false;
        }
    }

    private static void PatchIntProperty(byte[] data, string propName, int newValue)
    {
        try
        {
            byte[] propBytes = System.Text.Encoding.ASCII.GetBytes(propName);
            byte[] pattern = new byte[propBytes.Length + 16];
            Buffer.BlockCopy(propBytes, 0, pattern, 0, propBytes.Length);

            // Pattern suffix: \0\x0c\0\0\0IntProperty\0\x04\0\0\0\0\0\0\0\0
            byte[] suffix = new byte[] {
                0x00, 0x0c, 0x00, 0x00, 0x00,
                0x49, 0x6e, 0x74, 0x50, 0x72, 0x6f, 0x70, 0x65, 0x72, 0x74, 0x79, 0x00, // IntProperty\0
                0x04, 0x00, 0x00, 0x00, // 4 bytes size
                0x00, 0x00, 0x00, 0x00, // index 0
                0x00 // terminator
            };

            byte[] fullPattern = new byte[propBytes.Length + suffix.Length];
            Buffer.BlockCopy(propBytes, 0, fullPattern, 0, propBytes.Length);
            Buffer.BlockCopy(suffix, 0, fullPattern, propBytes.Length, suffix.Length);

            int idx = 0;
            byte[] valBytes = BitConverter.GetBytes(newValue);
            while (idx <= data.Length - fullPattern.Length - 4)
            {
                bool match = true;
                for (int i = 0; i < fullPattern.Length; i++)
                {
                    if (data[idx + i] != fullPattern[i])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    int valOffset = idx + fullPattern.Length;
                    Buffer.BlockCopy(valBytes, 0, data, valOffset, 4);
                    idx += fullPattern.Length + 4;
                }
                else
                {
                    idx++;
                }
            }
        }
        catch { }
    }

    private static void DeploySavViaAdb(string localSavFile)
    {
        try
        {
            var adbCandidates = new[]
            {
                @"E:\Program Files\TxGameAssistant\ui\adb.exe",
                @"C:\Program Files\TxGameAssistant\ui\adb.exe",
                @"D:\Program Files\TxGameAssistant\ui\adb.exe",
                @"E:\Temp\TxGameDownload\Component\UI\cf915be3939e5a6b1717b28f6a13869e\Setup\adb.exe"
            };

            string? adbExe = adbCandidates.FirstOrDefault(File.Exists);
            if (adbExe == null) return;

            foreach (int port in new[] { 5555, 6555, 7555 })
            {
                try
                {
                    var pConn = Process.Start(new ProcessStartInfo
                    {
                        FileName = adbExe,
                        Arguments = $"connect 127.0.0.1:{port}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    pConn?.WaitForExit(1000);
                }
                catch { }
            }

            var packages = new[]
            {
                "com.tencent.ig",
                "com.vng.pubgmobile",
                "com.pubg.krmobile",
                "com.pubg.imobile"
            };

            foreach (var pkg in packages)
            {
                string targetAndroidDir = $"/sdcard/Android/data/{pkg}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/SaveGames";
                try
                {
                    var pMkdir = Process.Start(new ProcessStartInfo
                    {
                        FileName = adbExe,
                        Arguments = $"shell mkdir -p {targetAndroidDir}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    pMkdir?.WaitForExit(1000);

                    var pPush = Process.Start(new ProcessStartInfo
                    {
                        FileName = adbExe,
                        Arguments = $"push \"{localSavFile}\" \"{targetAndroidDir}/Active.sav\"",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    pPush?.WaitForExit(2000);

                    var pChmod = Process.Start(new ProcessStartInfo
                    {
                        FileName = adbExe,
                        Arguments = $"shell chmod 666 {targetAndroidDir}/Active.sav",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    pChmod?.WaitForExit(1000);
                }
                catch { }
            }
        }
        catch { }
    }

    private static void DeployActiveSavPreset(int qualityIndex)
    {
        try
        {
            string subFolder = qualityIndex switch
            {
                0 => "Smooth",
                1 => "HD",
                2 => "HD",
                3 => "HDR",
                4 => "UHD",
                _ => "Smooth"
            };

            var sourceCandidates = new[]
            {
                Path.Combine(@"D:\Lag Pix", subFolder + " 120", "Active.sav"),
                Path.Combine(@"D:\Lag Pix", subFolder + " 120", "Normal", "Active.sav"),
                Path.Combine(@"D:\Lag Pix", subFolder, "Active.sav"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Presets", subFolder, "Active.sav"),
                Path.Combine(@"d:\Shop\Xenom Lag Fix\Assets\Presets", subFolder, "Active.sav")
            };

            string? srcFile = null;
            foreach (var s in sourceCandidates)
            {
                if (File.Exists(s)) { srcFile = s; break; }
            }

            if (srcFile == null) return;

            // 1. Target destination: GameLoop Shared folder on Windows host
            var targetDestinations = new[]
            {
                @"E:\Temp\TxGameDownload\MobileGamePCShared\Active.sav",
                @"C:\Temp\TxGameDownload\MobileGamePCShared\Active.sav",
                @"D:\Temp\TxGameDownload\MobileGamePCShared\Active.sav"
            };

            foreach (var dest in targetDestinations)
            {
                var dir = Path.GetDirectoryName(dest);
                if (dir != null && Directory.Exists(dir))
                {
                    File.Copy(srcFile, dest, true);
                }
            }

            // 2. Direct ADB Injection into running GameLoop Android storage
            var adbCandidates = new[]
            {
                @"E:\Program Files\TxGameAssistant\ui\adb.exe",
                @"C:\Program Files\TxGameAssistant\ui\adb.exe",
                @"D:\Program Files\TxGameAssistant\ui\adb.exe",
                @"E:\Temp\TxGameDownload\Component\UI\cf915be3939e5a6b1717b28f6a13869e\Setup\adb.exe"
            };

            string? adbExe = adbCandidates.FirstOrDefault(File.Exists);
            if (adbExe != null)
            {
                foreach (int port in new[] { 5555, 6555, 7555 })
                {
                    try
                    {
                        var pConn = Process.Start(new ProcessStartInfo
                        {
                            FileName = adbExe,
                            Arguments = $"connect 127.0.0.1:{port}",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        pConn?.WaitForExit(1500);
                    }
                    catch { }
                }

                var packages = new[]
                {
                    "com.tencent.ig",
                    "com.vng.pubgmobile",
                    "com.pubg.krmobile",
                    "com.pubg.imobile"
                };

                foreach (var pkg in packages)
                {
                    string targetAndroidDir = $"/sdcard/Android/data/{pkg}/files/UE4Game/ShadowTrackerExtra/ShadowTrackerExtra/Saved/SaveGames";
                    try
                    {
                        var pMkdir = Process.Start(new ProcessStartInfo
                        {
                            FileName = adbExe,
                            Arguments = $"shell mkdir -p {targetAndroidDir}",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        pMkdir?.WaitForExit(1500);

                        var pPush = Process.Start(new ProcessStartInfo
                        {
                            FileName = adbExe,
                            Arguments = $"push \"{srcFile}\" \"{targetAndroidDir}/Active.sav\"",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        pPush?.WaitForExit(3000);

                        var pChmod = Process.Start(new ProcessStartInfo
                        {
                            FileName = adbExe,
                            Arguments = $"shell chmod 666 {targetAndroidDir}/Active.sav",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                        pChmod?.WaitForExit(1000);
                    }
                    catch { }
                }
            }
        }
        catch { }
    }
}
