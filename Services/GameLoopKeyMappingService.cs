using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using XenomOptimizer.Models;

namespace XenomOptimizer.Services;

public class GameLoopKeyMappingService : IGameLoopKeyMappingService
{
    private readonly IGameLoopDetectionService _gameLoopDetection;
    private readonly ILogService _log;

    public GameLoopKeyMappingService(
        IGameLoopDetectionService gameLoopDetection,
        ILogService log)
    {
        _gameLoopDetection = gameLoopDetection;
        _log = log;
    }

    public async Task<GameLoopEdition> DetectInstalledEditionAsync()
    {
        var installPath = await _gameLoopDetection.GetInstallPathAsync();
        if (string.IsNullOrEmpty(installPath))
        {
            installPath = @"C:\Program Files\TxGameAssistant";
        }

        // 1. Check for Chinese version via Registry
        try
        {
            using var lmKey = Registry.LocalMachine.OpenSubKey(@"Software\WOW6432Node\Tencent\MobileGamePC")
                           ?? Registry.LocalMachine.OpenSubKey(@"Software\Tencent\MobileGamePC");
            if (lmKey?.GetValue("TSyzsVersion") != null)
            {
                _log.Log("KeyMapping", "Detected Chinese GameLoop via TSyzsVersion in registry", "INFO");
                return GameLoopEdition.Chinese;
            }

            using var cuKey = Registry.CurrentUser.OpenSubKey(@"Software\Tencent\MobileGamePC");
            if (cuKey != null)
            {
                var region = cuKey.GetValue("Region")?.ToString();
                if (string.Equals(region, "CN", StringComparison.OrdinalIgnoreCase) || cuKey.GetValue("shift_run_pubgmhd") != null)
                {
                    _log.Log("KeyMapping", "Detected Chinese GameLoop via Region=CN or shift_run_pubgmhd in HKCU", "INFO");
                    return GameLoopEdition.Chinese;
                }
            }
        }
        catch { }

        // 2. Check AppMarket.exe / AndroidEmulator VersionInfo (FileDescription / ProductName)
        try
        {
            var appMarketPaths = new[]
            {
                Path.Combine(installPath, "AppMarket", "AppMarket.exe"),
                Path.Combine(installPath, "AppMarket.exe"),
                @"C:\Program Files\TxGameAssistant\AppMarket\AppMarket.exe",
                @"D:\Program Files\TxGameAssistant\AppMarket\AppMarket.exe",
                @"E:\Program Files\TxGameAssistant\AppMarket\AppMarket.exe"
            };

            foreach (var amp in appMarketPaths)
            {
                if (File.Exists(amp))
                {
                    var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(amp);
                    if ((vi.ProductName != null && vi.ProductName.Contains("腾讯手游助手")) ||
                        (vi.FileDescription != null && vi.FileDescription.Contains("腾讯手游助手")))
                    {
                        _log.Log("KeyMapping", "Detected Chinese GameLoop via AppMarket FileDescription/ProductName", "INFO");
                        return GameLoopEdition.Chinese;
                    }
                }
            }
        }
        catch { }

        // 3. Check for 64-bit Rootfs / vshell / 64-bit AppMarket
        bool is64Bit = Directory.Exists(Path.Combine(installPath, "AOW_Rootfs_100")) ||
                       Directory.Exists(Path.Combine(installPath, "vshell")) ||
                       File.Exists(Path.Combine(installPath, "AOW_Rootfs_100", "init"));

        // 4. Check DefaultKeyMapping.xml for active APK
        try
        {
            var xmlPath = await GetMainXmlPathAsync();
            if (File.Exists(xmlPath))
            {
                var content = await File.ReadAllTextAsync(xmlPath);
                if (content.Contains("ApkName=\"com.tencent.tmgp.pubgmhd\"") && !content.Contains("ApkName=\"com.tencent.ig\""))
                {
                    return GameLoopEdition.Chinese;
                }
            }
        }
        catch { }

        return is64Bit ? GameLoopEdition.Global64 : GameLoopEdition.Global32;
    }

    public int DetectActiveResolutionModeId()
    {
        try
        {
            using var cuKey = Registry.CurrentUser.OpenSubKey(@"Software\Tencent\MobileGamePC");
            using var lmKey = Registry.LocalMachine.OpenSubKey(@"Software\WOW6432Node\Tencent\MobileGamePC")
                           ?? Registry.LocalMachine.OpenSubKey(@"Software\Tencent\MobileGamePC");

            // 1. Check RenderResolutionQuality in HKLM (3 = Ultra HD 2K)
            var rrq = lmKey?.GetValue("RenderResolutionQuality");
            if (rrq is int ir && ir >= 3) return 4;
            if (rrq is string sr && int.TryParse(sr, out int pr) && pr >= 3) return 4;

            // 2. Check individual PUBG game render quality in HKCU (4 = 2K, 2 or 3 = 1080P)
            string[] renderQualityKeys = {
                "com.tencent.ig_RenderQuality",
                "com.vng.pubgmobile_RenderQuality",
                "com.pubg.krmobile_RenderQuality",
                "com.pubg.imobile_RenderQuality",
                "com.tencent.tmgp.pubgmhd_RenderQuality"
            };
            foreach (var rqKey in renderQualityKeys)
            {
                var val = cuKey?.GetValue(rqKey);
                if (val is int v && v >= 4) return 4;
                if (val is string sv && int.TryParse(sv, out int pv) && pv >= 4) return 4;
            }

            // 3. Check VM and screen resolution widths (>= 2560 = 2K, >= 1920 = 1080P)
            string[] resKeys = { "VMResWidth", "ResolutionWidth", "ClientWidth" };
            foreach (var rk in resKeys)
            {
                var val = cuKey?.GetValue(rk);
                if (val is int w && w >= 2560) return 4;
                if (val is string sw && int.TryParse(sw, out int pw) && pw >= 2560) return 4;
            }

            // Check 1080P
            foreach (var rqKey in renderQualityKeys)
            {
                var val = cuKey?.GetValue(rqKey);
                if (val is int v && (v == 2 || v == 3)) return 3;
                if (val is string sv && int.TryParse(sv, out int pv) && (pv == 2 || pv == 3)) return 3;
            }
            foreach (var rk in resKeys)
            {
                var val = cuKey?.GetValue(rk);
                if (val is int w && w >= 1920) return 3;
                if (val is string sw && int.TryParse(sw, out int pw) && pw >= 1920) return 3;
            }

            // 4. If high-end GPU is present (e.g. RTX 3070), default to Smart 2K (Mode 4)
            var glRender = cuKey?.OpenSubKey("DeviceInfo")?.GetValue("glRender")?.ToString() ?? "";
            if (glRender.Contains("RTX", StringComparison.OrdinalIgnoreCase) ||
                glRender.Contains("GTX", StringComparison.OrdinalIgnoreCase) ||
                glRender.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
            {
                return 4; // Smart 2K
            }

            return 3; // Smart 1080P default
        }
        catch
        {
            return 4; // Smart 2K fallback
        }
    }

    public async Task<GameLoopKeyMappingProfile> LoadProfileAsync(GameLoopEdition edition, int? modeId = null)
    {
        int activeDetectedMode = DetectActiveResolutionModeId();
        int initialModeId = modeId ?? activeDetectedMode;

        var profile = new GameLoopKeyMappingProfile
        {
            Edition = edition,
            SelectedModeId = initialModeId
        };

        var xmlPath = await GetMainXmlPathAsync();
        if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
        {
            _log.Log("KeyMapping", "DefaultKeyMapping.xml not found, generating defaults", "WARNING");
            profile.Keys = GetDefaultKeys(profile.SelectedModeId);
            return profile;
        }

        try
        {
            var content = await File.ReadAllTextAsync(xmlPath);
            var (primaryApk, _) = GetApkNames(edition);

            var itemMatch = Regex.Match(content, $@"(<Item ApkName=""{Regex.Escape(primaryApk)}""[^>]*>.*?</Item>)", RegexOptions.Singleline);
            if (!itemMatch.Success)
            {
                // Fallback to Global if specific variant item not found
                itemMatch = Regex.Match(content, @"(<Item ApkName=""com\.tencent\.ig""[^>]*>.*?</Item>)", RegexOptions.Singleline);
            }
            if (!itemMatch.Success)
            {
                // Fallback to Chinese PUBG if Global not found
                itemMatch = Regex.Match(content, @"(<Item ApkName=""com\.tencent\.tmgp\.pubgmhd""[^>]*>.*?</Item>)", RegexOptions.Singleline);
            }

            if (itemMatch.Success)
            {
                var itemText = itemMatch.Groups[1].Value;

                // Read LastMode
                var lastModeMatch = Regex.Match(itemText, @"<LastMode\s+([^>]+?)(?:/>|>)");
                if (lastModeMatch.Success)
                {
                    var attrs = lastModeMatch.Groups[1].Value;
                    if (modeId == null)
                    {
                        var modeMatch = Regex.Match(attrs, @"ModeID=""(\d+)""");
                        if (modeMatch.Success && int.TryParse(modeMatch.Groups[1].Value, out int id) && id > 1)
                        {
                            profile.SelectedModeId = Math.Clamp(id, 1, 4);
                        }
                        else
                        {
                            profile.SelectedModeId = activeDetectedMode;
                        }
                    }
                    else
                    {
                        profile.SelectedModeId = Math.Clamp(modeId.Value, 1, 4);
                    }

                    var tipsMatch = Regex.Match(attrs, @"EnableTips=""(\d+)""");
                    if (tipsMatch.Success) profile.EnableTips = tipsMatch.Groups[1].Value == "1";

                    var dtMatch = Regex.Match(attrs, @"EnableGameKeyDT=""(\d+)""");
                    if (dtMatch.Success) profile.AutoSwitchResolutionMode = dtMatch.Groups[1].Value == "1";

                    var lightMatch = Regex.Match(attrs, @"Lightness=""([\d\.]+)""");
                    if (lightMatch.Success && double.TryParse(lightMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double light))
                    {
                        profile.BrightnessPercent = Math.Clamp(light * 100.0, 0.0, 100.0);
                    }

                    var transMatch = Regex.Match(attrs, @"TipsTransparent=""([\d\.]+)""");
                    if (transMatch.Success && double.TryParse(transMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double trans))
                    {
                        profile.TipsTransparencyPercent = Math.Clamp(trans * 100.0, 0.0, 100.0);
                    }
                }

                // Read Registry for Hold Shift to run
                try
                {
                    using var hkcu = Registry.CurrentUser.OpenSubKey(@"Software\Tencent\MobileGamePC");
                    var shiftVal = hkcu?.GetValue("shift_run_pubgmhd") ?? hkcu?.GetValue("shift_run_pubgm");
                    if (shiftVal is int iVal)
                    {
                        profile.HoldShiftToRun = iVal == 1;
                    }
                    else if (shiftVal is string sVal && int.TryParse(sVal, out int parsed))
                    {
                        profile.HoldShiftToRun = parsed == 1;
                    }
                    else
                    {
                        profile.HoldShiftToRun = true;
                    }
                }
                catch { profile.HoldShiftToRun = true; }

                // Parse Mode's keys
                profile.Keys = ParseKeysFromMode(itemText, profile.SelectedModeId);
            }
            else
            {
                profile.Keys = GetDefaultKeys(profile.SelectedModeId);
            }
        }
        catch (Exception ex)
        {
            _log.Log("KeyMapping", $"Failed to load keymapping: {ex.Message}", "ERROR", errorMessage: ex.ToString());
            profile.Keys = GetDefaultKeys(profile.SelectedModeId);
        }

        return profile;
    }

    public async Task<bool> SaveProfileAsync(GameLoopKeyMappingProfile profile)
    {
        try
        {
            var xmlPaths = await GetAllXmlPathsAsync();
            if (xmlPaths.Count == 0)
            {
                _log.Log("KeyMapping", "No DefaultKeyMapping.xml path found to save", "ERROR");
                return false;
            }

            var (primaryApk, smartApk) = GetApkNames(profile.Edition);
            var apksToUpdate = new HashSet<string> { primaryApk, "com.tencent.ig", "com.tencent.tmgp.pubgmhd" };
            if (!string.IsNullOrEmpty(smartApk)) apksToUpdate.Add(smartApk);
            apksToUpdate.Add("com.tencent.ig_ss");
            apksToUpdate.Add("com.tencent.tmgp.pubgmhd_ss");

            foreach (var xmlPath in xmlPaths)
            {
                if (!File.Exists(xmlPath)) continue;

                // Backup before write
                try
                {
                    var backupPath = xmlPath + ".bak";
                    if (!File.Exists(backupPath))
                    {
                        File.Copy(xmlPath, backupPath, true);
                    }
                }
                catch { }

                var content = await File.ReadAllTextAsync(xmlPath);

                foreach (var apk in apksToUpdate)
                {
                    var pattern = $@"(<Item ApkName=""{Regex.Escape(apk)}""[^>]*>.*?</Item>)";
                    var match = Regex.Match(content, pattern, RegexOptions.Singleline);
                    if (!match.Success) continue;

                    var originalItem = match.Groups[1].Value;
                    var modifiedItem = UpdateItemXml(originalItem, profile);

                    content = content.Replace(originalItem, modifiedItem);
                }

                await File.WriteAllTextAsync(xmlPath, content);
                _log.Log("KeyMapping", $"Successfully saved keymapping to {xmlPath}", "SUCCESS");
            }

            // Save Registry settings
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Tencent\MobileGamePC");
                if (key != null)
                {
                    int shift = profile.HoldShiftToRun ? 1 : 0;
                    key.SetValue("shift_run_pubgmhd", shift, RegistryValueKind.DWord);
                    key.SetValue("shift_run_pubgm", shift, RegistryValueKind.DWord);
                    key.SetValue("shift_run_ig", shift, RegistryValueKind.DWord);
                    key.SetValue("KeyMindTipsPressed", profile.EnableTips ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch { }

            return true;
        }
        catch (Exception ex)
        {
            _log.Log("KeyMapping", $"SaveProfileAsync failed: {ex.Message}", "ERROR", errorMessage: ex.ToString());
            return false;
        }
    }

    public Task<GameLoopKeyMappingProfile> ResetToDefaultsAsync(GameLoopEdition edition, int modeId)
    {
        var profile = new GameLoopKeyMappingProfile
        {
            Edition = edition,
            SelectedModeId = modeId,
            EnableTips = false,
            AutoSwitchResolutionMode = true,
            HoldShiftToRun = true,
            BrightnessPercent = 100.0,
            TipsTransparencyPercent = 50.0,
            HorizontalSensitivity = 100.0,
            VerticalSensitivity = 100.0,
            Keys = GetDefaultKeys(modeId)
        };

        return Task.FromResult(profile);
    }

    private string UpdateItemXml(string itemXml, GameLoopKeyMappingProfile profile)
    {
        // 1. Update LastMode
        string lightnessStr = (profile.BrightnessPercent / 100.0).ToString("F6", CultureInfo.InvariantCulture);
        string transStr = (profile.TipsTransparencyPercent / 100.0).ToString("F6", CultureInfo.InvariantCulture);
        string tipsStr = profile.EnableTips ? "1" : "0";
        string dtStr = profile.AutoSwitchResolutionMode ? "1" : "0";
        string modeStr = profile.SelectedModeId.ToString();

        // Update LastMode attributes
        itemXml = Regex.Replace(itemXml, @"<LastMode\s+([^>]+?)(/?>)", m =>
        {
            var attrs = m.Groups[1].Value;
            var endTag = m.Groups[2].Value;
            attrs = Regex.Replace(attrs, @"ModeID=""\d+""", $@"ModeID=""{modeStr}""");
            attrs = Regex.Replace(attrs, @"EnableTips=""\d+""", $@"EnableTips=""{tipsStr}""");
            attrs = Regex.Replace(attrs, @"EnableGameKeyDT=""\d+""", $@"EnableGameKeyDT=""{dtStr}""");
            attrs = Regex.Replace(attrs, @"Lightness=""[\d\.]+""", $@"Lightness=""{lightnessStr}""");
            attrs = Regex.Replace(attrs, @"TipsTransparent=""[\d\.]+""", $@"TipsTransparent=""{transStr}""");
            return $"<LastMode {attrs}{endTag}";
        });

        // 2. Find KeyMapMode for SelectedModeId (only the 16:9 standard one, before <Resolution_16x10>)
        var modePattern = $@"(<KeyMapMode ModeID=""{profile.SelectedModeId}""[^>]*>.*?</KeyMapMode>)";
        var modeMatch = Regex.Match(itemXml, modePattern, RegexOptions.Singleline);
        if (!modeMatch.Success) return itemXml;

        var originalMode = modeMatch.Groups[1].Value;
        var modifiedMode = originalMode;

        // Apply key updates
        foreach (var key in profile.Keys)
        {
            if (key.Id == "MoveUp" || key.Id == "MoveDown" || key.Id == "MoveLeft" || key.Id == "MoveRight")
            {
                // CrossKey WASD
                modifiedMode = Regex.Replace(modifiedMode,
                    @"<KeyMappingEx\s+([^>]*ItemName=""WASD""[^>]*?)(/?>)",
                    m =>
                    {
                        var attrs = m.Groups[1].Value;
                        var endTag = m.Groups[2].Value;
                        if (key.Id == "MoveUp") attrs = Regex.Replace(attrs, @"UP_AsciiCode=""\d+""", $@"UP_AsciiCode=""{key.AsciiCode}""");
                        if (key.Id == "MoveDown") attrs = Regex.Replace(attrs, @"DOWN_AsciiCode=""\d+""", $@"DOWN_AsciiCode=""{key.AsciiCode}""");
                        if (key.Id == "MoveLeft") attrs = Regex.Replace(attrs, @"LEFT_AsciiCode=""\d+""", $@"LEFT_AsciiCode=""{key.AsciiCode}""");
                        if (key.Id == "MoveRight") attrs = Regex.Replace(attrs, @"RIGHT_AsciiCode=""\d+""", $@"RIGHT_AsciiCode=""{key.AsciiCode}""");
                        return $"<KeyMappingEx {attrs}{endTag}";
                    });
            }
            else if (key.IsEx)
            {
                // KeyMappingEx with ItemName or Type
                string searchPattern = !string.IsNullOrEmpty(key.XmlItemName)
                    ? $@"<KeyMappingEx\s+([^>]*ItemName=""{Regex.Escape(key.XmlItemName)}""[^>]*?)(/?>)"
                    : $@"<KeyMappingEx\s+([^>]*Type=""{Regex.Escape(key.XmlType)}""[^>]*?)(/?>)";

                modifiedMode = Regex.Replace(modifiedMode, searchPattern, m =>
                {
                    var attrs = m.Groups[1].Value;
                    var endTag = m.Groups[2].Value;
                    if (attrs.Contains("AsciiCode="))
                    {
                        attrs = Regex.Replace(attrs, @"AsciiCode=""\d+""", $@"AsciiCode=""{key.AsciiCode}""");
                    }
                    if (!string.IsNullOrEmpty(key.CurrentKey) && attrs.Contains("ItemName="))
                    {
                        attrs = Regex.Replace(attrs, @"ItemName=""[^""]*""", $@"ItemName=""{key.CurrentKey}""");
                    }
                    return $"<KeyMappingEx {attrs}{endTag}";
                });
            }
            else
            {
                // Standard KeyMapping
                if (!string.IsNullOrEmpty(key.XmlItemName))
                {
                    string searchPattern = $@"<KeyMapping\s+([^>]*ItemName=""{Regex.Escape(key.XmlItemName)}""[^>]*?)(/?>)";
                    modifiedMode = Regex.Replace(modifiedMode, searchPattern, m =>
                    {
                        var attrs = m.Groups[1].Value;
                        var endTag = m.Groups[2].Value;
                        attrs = Regex.Replace(attrs, @"AsciiCode=""[^""]*""", $@"AsciiCode=""{key.AsciiCode}""");
                        attrs = Regex.Replace(attrs, @"ItemName=""[^""]*""", $@"ItemName=""{key.CurrentKey}""");
                        return $"<KeyMapping {attrs}{endTag}";
                    }, RegexOptions.None);
                }
            }
        }

        return itemXml.Replace(originalMode, modifiedMode);
    }

    private List<GameLoopKeyItem> ParseKeysFromMode(string itemXml, int modeId)
    {
        var keys = GetDefaultKeys(modeId);

        var modeMatch = Regex.Match(itemXml, $@"(<KeyMapMode ModeID=""{modeId}""[^>]*>.*?</KeyMapMode>)", RegexOptions.Singleline);
        if (!modeMatch.Success) return keys;

        var modeText = modeMatch.Groups[1].Value;

        // 1. Parse WASD CrossKey
        var wasdMatch = Regex.Match(modeText, @"<KeyMappingEx\s+[^>]*ItemName=""WASD""[^>]*UP_AsciiCode=""(\d+)""[^>]*DOWN_AsciiCode=""(\d+)""[^>]*LEFT_AsciiCode=""(\d+)""[^>]*RIGHT_AsciiCode=""(\d+)""");
        if (wasdMatch.Success)
        {
            SetKeyByAscii(keys, "MoveUp", int.Parse(wasdMatch.Groups[1].Value));
            SetKeyByAscii(keys, "MoveDown", int.Parse(wasdMatch.Groups[2].Value));
            SetKeyByAscii(keys, "MoveLeft", int.Parse(wasdMatch.Groups[3].Value));
            SetKeyByAscii(keys, "MoveRight", int.Parse(wasdMatch.Groups[4].Value));
        }

        // 2. Parse single KeyMapping items (both /> and > endings)
        var keyMatches = Regex.Matches(modeText, @"<KeyMapping\s+([^>]+?)(?:/>|>)");
        foreach (Match km in keyMatches)
        {
            var raw = km.Groups[1].Value;
            var nameMatch = Regex.Match(raw, @"ItemName=""([^""]+)""");
            var asciiMatch = Regex.Match(raw, @"AsciiCode=""([^""]+)""");
            var descMatch = Regex.Match(raw, @"Description=""([^""]+)""");

            if (asciiMatch.Success && int.TryParse(asciiMatch.Groups[1].Value, out int ascii))
            {
                string name = nameMatch.Success ? nameMatch.Groups[1].Value : "";
                string desc = descMatch.Success ? descMatch.Groups[1].Value : "";

                foreach (var k in keys.Where(x => !x.IsEx))
                {
                    if ((!string.IsNullOrEmpty(k.XmlItemName) && k.XmlItemName.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(k.Description_XML) && k.Description_XML.Equals(desc, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(desc) && (
                            (k.Id == "Sprint" && (desc.Contains("加速跑") || name == "Shift" || name == "=")) ||
                            (k.Id == "Jump" && desc.Contains("跳")) ||
                            (k.Id == "Crouch" && desc.Contains("蹲")) ||
                            (k.Id == "Prone" && desc.Contains("趴")) ||
                            (k.Id == "Reload" && desc.Contains("换弹")) ||
                            (k.Id == "Weapon1" && (desc.Contains("1号武器") || name == "1")) ||
                            (k.Id == "Weapon2" && (desc.Contains("2号武器") || name == "2")) ||
                            (k.Id == "Pistol" && (desc.Contains("3号武器") || name == "3")) ||
                            (k.Id == "Melee" && (desc.Contains("冷兵器") || name == "5")) ||
                            (k.Id == "Interact" && (desc.Contains("开车、开门") || name == "F")) ||
                            (k.Id == "Drive" && (desc.Contains("乘车") || name == "G")) ||
                            (k.Id == "Loot" && (desc.Contains("捡物") || name == "H")) ||
                            (k.Id == "Backpack" && (desc.Contains("背包") || name == "Tab")) ||
                            (k.Id == "Map" && (desc.Contains("地图") || name == "M")) ||
                            (k.Id == "FppTpp" && (desc.Contains("人称") || name == "V")) ||
                            (k.Id == "Voice" && (desc.Contains("语音") || name == "T")) ||
                            (k.Id == "Settings" && (desc.Contains("设置") || name == "Esc")) ||
                            (k.Id == "Aim" && (name == "右键" || desc.Contains("开镜") || desc.Contains("瞄准")))
                        )))
                    {
                        k.AsciiCode = ascii;
                        k.CurrentKey = GetKeyNameFromAscii(ascii);
                    }
                }
            }
        }

        // 3. Parse KeyMappingEx items (both /> and > endings)
        var exMatches = Regex.Matches(modeText, @"<KeyMappingEx\s+([^>]+?)(?:/>|>)");
        foreach (Match em in exMatches)
        {
            var raw = em.Groups[1].Value;
            var nameMatch = Regex.Match(raw, @"ItemName=""([^""]*)""");
            var asciiMatch = Regex.Match(raw, @"AsciiCode=""(\d+)""");
            var typeMatch = Regex.Match(raw, @"Type=""([^""]+)""");
            var descMatch = Regex.Match(raw, @"Description=""([^""]+)""");

            string name = nameMatch.Success ? nameMatch.Groups[1].Value : "";
            string type = typeMatch.Success ? typeMatch.Groups[1].Value : "";
            string desc = descMatch.Success ? descMatch.Groups[1].Value : "";

            int? ascii = null;
            if (asciiMatch.Success && int.TryParse(asciiMatch.Groups[1].Value, out int a)) ascii = a;

            foreach (var k in keys.Where(x => x.IsEx && x.Id != "MoveUp" && x.Id != "MoveDown" && x.Id != "MoveLeft" && x.Id != "MoveRight"))
            {
                if ((type == "LClick" || desc.Contains("开火")) && k.Id == "Fire")
                {
                    k.AsciiCode = -1;
                    k.CurrentKey = "Left Click";
                }
                else if ((type == "RClick" || desc.Contains("视角") || name == "右键") && k.Id == "Aim")
                {
                    k.AsciiCode = -2;
                    k.CurrentKey = "Right Click";
                }
                else if (ascii.HasValue && (
                    (!string.IsNullOrEmpty(k.XmlItemName) && k.XmlItemName.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(k.XmlType) && k.XmlType.Equals(type, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(desc) && (
                        (k.Id == "FreeLook" && desc.Contains("自由视角")) ||
                        (k.Id == "Frag" && (desc.Contains("手雷") || name == "4")) ||
                        (k.Id == "Smoke" && (desc.Contains("烟雾") || name == "5")) ||
                        (k.Id == "Molotov" && (desc.Contains("燃烧") || name == "6")) ||
                        (k.Id == "MedKit" && (desc.Contains("急救箱") || name == "7")) ||
                        (k.Id == "FirstAid" && (desc.Contains("急救包") || name == "8")) ||
                        (k.Id == "Bandage" && (desc.Contains("绷带") || name == "9")) ||
                        (k.Id == "Painkiller" && (desc.Contains("药") || name == "0")) ||
                        (k.Id == "FireMode" && (desc.Contains("单发/连发") || name == "B"))
                    ))))
                {
                    k.AsciiCode = ascii.Value;
                    k.CurrentKey = GetKeyNameFromAscii(ascii.Value);
                }
            }
        }

        return keys;
    }

    private void SetKeyByAscii(List<GameLoopKeyItem> keys, string id, int ascii)
    {
        var item = keys.FirstOrDefault(k => k.Id == id);
        if (item != null)
        {
            item.AsciiCode = ascii;
            item.CurrentKey = GetKeyNameFromAscii(ascii);
        }
    }

    private List<GameLoopKeyItem> GetDefaultKeys(int modeId)
    {
        return new List<GameLoopKeyItem>
        {
            // Movement
            new() { Id = "MoveUp", DisplayName = "Move Forward", Category = "Movement", Description = "Move character forward", DefaultKey = "W", CurrentKey = "W", AsciiCode = 87, IsEx = true, XmlItemName = "WASD", XmlAttribute = "UP_AsciiCode" },
            new() { Id = "MoveDown", DisplayName = "Move Backward", Category = "Movement", Description = "Move character backward", DefaultKey = "S", CurrentKey = "S", AsciiCode = 83, IsEx = true, XmlItemName = "WASD", XmlAttribute = "DOWN_AsciiCode" },
            new() { Id = "MoveLeft", DisplayName = "Move Left (Strafe)", Category = "Movement", Description = "Strafe character left", DefaultKey = "A", CurrentKey = "A", AsciiCode = 65, IsEx = true, XmlItemName = "WASD", XmlAttribute = "LEFT_AsciiCode" },
            new() { Id = "MoveRight", DisplayName = "Move Right (Strafe)", Category = "Movement", Description = "Strafe character right", DefaultKey = "D", CurrentKey = "D", AsciiCode = 68, IsEx = true, XmlItemName = "WASD", XmlAttribute = "RIGHT_AsciiCode" },
            new() { Id = "Sprint", DisplayName = "Sprint / Accelerate", Category = "Movement", Description = "Fast running / boost", DefaultKey = "Shift", CurrentKey = "Shift", AsciiCode = 16, IsEx = false, XmlItemName = "Shift", Description_XML = "加速跑" },
            new() { Id = "Jump", DisplayName = "Jump / Vault", Category = "Movement", Description = "Jump, vault over walls, swim up", DefaultKey = "Space", CurrentKey = "Space", AsciiCode = 32, IsEx = false, XmlItemName = "Space" },
            new() { Id = "Crouch", DisplayName = "Crouch", Category = "Movement", Description = "Crouch / squat stance", DefaultKey = "C", CurrentKey = "C", AsciiCode = 67, IsEx = false, XmlItemName = "C" },
            new() { Id = "Prone", DisplayName = "Prone", Category = "Movement", Description = "Lie down / crawl stance", DefaultKey = "Z", CurrentKey = "Z", AsciiCode = 90, IsEx = false, XmlItemName = "Z" },
            new() { Id = "FreeLook", DisplayName = "Free Look (Eye)", Category = "Movement", Description = "Look around without turning body", DefaultKey = "Alt", CurrentKey = "Alt", AsciiCode = 18, IsEx = true, XmlItemName = "Alt", XmlType = "KeyMove" },

            // Combat & Weapons
            new() { Id = "Fire", DisplayName = "Fire / Shoot", Category = "Combat & Weapons", Description = "Left click to shoot weapon", DefaultKey = "Left Click", CurrentKey = "Left Click", AsciiCode = -1, IsEx = true, XmlType = "LClick" },
            new() { Id = "Aim", DisplayName = "Aim / Scope (Toggle)", Category = "Combat & Weapons", Description = "Right click to aim down sights", DefaultKey = "Right Click", CurrentKey = "Right Click", AsciiCode = -2, IsEx = true, XmlType = "RClick", XmlItemName = "右键" },
            new() { Id = "Reload", DisplayName = "Reload Weapon", Category = "Combat & Weapons", Description = "Reload active weapon magazine", DefaultKey = "R", CurrentKey = "R", AsciiCode = 82, IsEx = false, XmlItemName = "R" },
            new() { Id = "Weapon1", DisplayName = "Primary Weapon", Category = "Combat & Weapons", Description = "Equip slot 1 primary firearm", DefaultKey = "1", CurrentKey = "1", AsciiCode = 49, IsEx = false, XmlItemName = "1" },
            new() { Id = "Weapon2", DisplayName = "Secondary Weapon", Category = "Combat & Weapons", Description = "Equip slot 2 secondary firearm", DefaultKey = "2", CurrentKey = "2", AsciiCode = 50, IsEx = false, XmlItemName = "2" },
            new() { Id = "Pistol", DisplayName = "Sidearm (Pistol)", Category = "Combat & Weapons", Description = "Equip slot 3 handgun / pistol", DefaultKey = "3", CurrentKey = "3", AsciiCode = 51, IsEx = false, XmlItemName = "3" },
            new() { Id = "Melee", DisplayName = "Melee Weapon", Category = "Combat & Weapons", Description = "Equip pan / crowbar / machete", DefaultKey = "5", CurrentKey = "5", AsciiCode = 53, IsEx = false, XmlItemName = "5" },
            new() { Id = "FireMode", DisplayName = "Fire Mode Switch", Category = "Combat & Weapons", Description = "Single / Burst / Full Auto", DefaultKey = "B", CurrentKey = "B", AsciiCode = 66, IsEx = true, XmlItemName = "B", XmlType = "MultiPoint" },
            new() { Id = "PeekLeft", DisplayName = "Peek Left", Category = "Combat & Weapons", Description = "Lean torso left around corner", DefaultKey = "Q", CurrentKey = "Q", AsciiCode = 81, IsEx = false, XmlItemName = "Q" },
            new() { Id = "PeekRight", DisplayName = "Peek Right", Category = "Combat & Weapons", Description = "Lean torso right around corner", DefaultKey = "E", CurrentKey = "E", AsciiCode = 69, IsEx = false, XmlItemName = "E" },

            // Throwables & Meds
            new() { Id = "Frag", DisplayName = "Frag Grenade", Category = "Throwables & Meds", Description = "Equip explosive fragmentation grenade", DefaultKey = "4", CurrentKey = "4", AsciiCode = 52, IsEx = true, XmlItemName = "4", XmlType = "FoldClick2" },
            new() { Id = "Smoke", DisplayName = "Smoke Grenade", Category = "Throwables & Meds", Description = "Equip smoke grenade for cover", DefaultKey = "5", CurrentKey = "5", AsciiCode = 53, IsEx = true, XmlItemName = "5", XmlType = "FoldClick2" },
            new() { Id = "Molotov", DisplayName = "Molotov / Stun", Category = "Throwables & Meds", Description = "Equip molotov cocktail or flashbang", DefaultKey = "6", CurrentKey = "6", AsciiCode = 54, IsEx = true, XmlItemName = "6", XmlType = "FoldClick2" },
            new() { Id = "MedKit", DisplayName = "Med Kit", Category = "Throwables & Meds", Description = "Full heal 100% health restore", DefaultKey = "7", CurrentKey = "7", AsciiCode = 55, IsEx = true, XmlItemName = "7", XmlType = "FoldClick2" },
            new() { Id = "FirstAid", DisplayName = "First Aid Kit", Category = "Throwables & Meds", Description = "Restore up to 75% health", DefaultKey = "8", CurrentKey = "8", AsciiCode = 56, IsEx = true, XmlItemName = "8", XmlType = "FoldClick2" },
            new() { Id = "Bandage", DisplayName = "Bandage", Category = "Throwables & Meds", Description = "Small health pack", DefaultKey = "9", CurrentKey = "9", AsciiCode = 57, IsEx = true, XmlItemName = "9", XmlType = "FoldClick2" },
            new() { Id = "Painkiller", DisplayName = "Energy Drink / Booster", Category = "Throwables & Meds", Description = "Painkiller or energy drink boost", DefaultKey = "0", CurrentKey = "0", AsciiCode = 48, IsEx = true, XmlItemName = "0", XmlType = "FoldClick2" },

            // Interaction & System
            new() { Id = "Interact", DisplayName = "Interact / Pick Up / Parachute", Category = "Interaction & System", Description = "Open doors, drive, pick up loot, revive", DefaultKey = "F", CurrentKey = "F", AsciiCode = 70, IsEx = false, XmlItemName = "F" },
            new() { Id = "Drive", DisplayName = "Get In / Secondary Loot", Category = "Interaction & System", Description = "Enter passenger seat or grab 2nd item", DefaultKey = "G", CurrentKey = "G", AsciiCode = 71, IsEx = false, XmlItemName = "G" },
            new() { Id = "Loot", DisplayName = "Loot Slot 3", Category = "Interaction & System", Description = "Pick up 3rd item from loot crate", DefaultKey = "H", CurrentKey = "H", AsciiCode = 72, IsEx = false, XmlItemName = "H" },
            new() { Id = "Backpack", DisplayName = "Open Backpack", Category = "Interaction & System", Description = "View and manage inventory", DefaultKey = "Tab", CurrentKey = "Tab", AsciiCode = 9, IsEx = false, XmlItemName = "Tab" },
            new() { Id = "Map", DisplayName = "Open Map", Category = "Interaction & System", Description = "Toggle full map overlay", DefaultKey = "M", CurrentKey = "M", AsciiCode = 77, IsEx = false, XmlItemName = "M" },
            new() { Id = "FppTpp", DisplayName = "FPP / TPP Switch", Category = "Interaction & System", Description = "Switch 1st and 3rd person camera", DefaultKey = "V", CurrentKey = "V", AsciiCode = 86, IsEx = false, XmlItemName = "V" },
            new() { Id = "Voice", DisplayName = "Voice Mute / Channel", Category = "Interaction & System", Description = "Toggle microphone mute", DefaultKey = "T", CurrentKey = "T", AsciiCode = 84, IsEx = false, XmlItemName = "T" },
            new() { Id = "Settings", DisplayName = "Settings / Cancel", Category = "Interaction & System", Description = "Open settings or cancel throwable", DefaultKey = "Esc", CurrentKey = "Esc", AsciiCode = 27, IsEx = false, XmlItemName = "Esc" }
        };
    }

    private (string primary, string smart) GetApkNames(GameLoopEdition edition)
    {
        // 1. Check registry for user's actively played PUBG variant
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
                if (matchApk.Contains("pubgmhd")) return ("com.tencent.tmgp.pubgmhd", "com.tencent.tmgp.pubgmhd_ss");
                if (matchApk.Contains("krmobile")) return ("com.pubg.krmobile", "com.pubg.krmobile_ss");
                if (matchApk.Contains("vng")) return ("com.vng.pubgmobile", "com.vng.pubgmobile_ss");
                if (matchApk.Contains("imobile")) return ("com.pubg.imobile", "");
                if (matchApk.Contains("ig")) return ("com.tencent.ig", "com.tencent.ig_ss");
            }
        }
        catch { }

        // Default based on edition
        return edition switch
        {
            GameLoopEdition.Chinese => ("com.tencent.tmgp.pubgmhd", "com.tencent.tmgp.pubgmhd_ss"),
            GameLoopEdition.KoreaJapan => ("com.pubg.krmobile", "com.pubg.krmobile_ss"),
            GameLoopEdition.Vietnam => ("com.vng.pubgmobile", "com.vng.pubgmobile_ss"),
            GameLoopEdition.IndiaBgmi => ("com.pubg.imobile", ""),
            _ => ("com.tencent.ig", "com.tencent.ig_ss")
        };
    }

    private async Task<string?> GetMainXmlPathAsync()
    {
        var paths = await GetAllXmlPathsAsync();
        return paths.FirstOrDefault(File.Exists);
    }

    private async Task<List<string>> GetAllXmlPathsAsync()
    {
        var results = new List<string>();
        var installPath = await _gameLoopDetection.GetInstallPathAsync();

        if (!string.IsNullOrEmpty(installPath))
        {
            var cleanRoot = installPath.EndsWith("ui", StringComparison.OrdinalIgnoreCase) || installPath.EndsWith("appmarket", StringComparison.OrdinalIgnoreCase)
                ? (Directory.GetParent(installPath)?.FullName ?? installPath)
                : installPath;

            results.Add(Path.Combine(cleanRoot, "ui", "DefaultKeyMapping.xml"));
            results.Add(Path.Combine(cleanRoot, "ui", "ConfigFile", "DefaultKeyMapping.xml"));
            results.Add(Path.Combine(installPath, "DefaultKeyMapping.xml"));
            results.Add(Path.Combine(installPath, "ConfigFile", "DefaultKeyMapping.xml"));
        }

        // Common default locations
        var commonRoots = new[]
        {
            @"C:\Program Files\TxGameAssistant",
            @"D:\Program Files\TxGameAssistant",
            @"E:\Program Files\TxGameAssistant",
            @"C:\TxGameAssistant",
            @"D:\TxGameAssistant",
            @"E:\TxGameAssistant"
        };

        foreach (var root in commonRoots)
        {
            var p1 = Path.Combine(root, "ui", "DefaultKeyMapping.xml");
            var p2 = Path.Combine(root, "ui", "ConfigFile", "DefaultKeyMapping.xml");
            if (!results.Contains(p1)) results.Add(p1);
            if (!results.Contains(p2)) results.Add(p2);
        }

        return results;
    }

    public (string displayKey, int asciiCode) ConvertWpfKey(Key key)
    {
        return key switch
        {
            Key.A => ("A", 65),
            Key.B => ("B", 66),
            Key.C => ("C", 67),
            Key.D => ("D", 68),
            Key.E => ("E", 69),
            Key.F => ("F", 70),
            Key.G => ("G", 71),
            Key.H => ("H", 72),
            Key.I => ("I", 73),
            Key.J => ("J", 74),
            Key.K => ("K", 75),
            Key.L => ("L", 76),
            Key.M => ("M", 77),
            Key.N => ("N", 78),
            Key.O => ("O", 79),
            Key.P => ("P", 80),
            Key.Q => ("Q", 81),
            Key.R => ("R", 82),
            Key.S => ("S", 83),
            Key.T => ("T", 84),
            Key.U => ("U", 85),
            Key.V => ("V", 86),
            Key.W => ("W", 87),
            Key.X => ("X", 88),
            Key.Y => ("Y", 89),
            Key.Z => ("Z", 90),

            Key.D0 => ("0", 48),
            Key.D1 => ("1", 49),
            Key.D2 => ("2", 50),
            Key.D3 => ("3", 51),
            Key.D4 => ("4", 52),
            Key.D5 => ("5", 53),
            Key.D6 => ("6", 54),
            Key.D7 => ("7", 55),
            Key.D8 => ("8", 56),
            Key.D9 => ("9", 57),

            Key.Space => ("Space", 32),
            Key.Tab => ("Tab", 9),
            Key.LeftShift or Key.RightShift => ("Shift", 16),
            Key.LeftCtrl or Key.RightCtrl => ("Ctrl", 17),
            Key.LeftAlt or Key.RightAlt => ("Alt", 18),
            Key.Escape => ("Esc", 27),
            Key.Return => ("Enter", 13),
            Key.Back => ("Backspace", 8),
            Key.Capital => ("CapsLock", 20),

            Key.F1 => ("F1", 112),
            Key.F2 => ("F2", 113),
            Key.F3 => ("F3", 114),
            Key.F4 => ("F4", 115),
            Key.F5 => ("F5", 116),
            Key.F6 => ("F6", 117),
            Key.F7 => ("F7", 118),
            Key.F8 => ("F8", 119),
            Key.F9 => ("F9", 120),
            Key.F10 => ("F10", 121),
            Key.F11 => ("F11", 122),
            Key.F12 => ("F12", 123),

            Key.OemTilde => ("~", 192),
            Key.OemMinus => ("-", 189),
            Key.OemPlus => ("=", 187),
            Key.OemOpenBrackets => ("[", 219),
            Key.Oem6 => ("]", 221),
            Key.Oem1 => (";", 186),
            Key.OemQuotes => ("'", 222),
            Key.OemComma => (",", 188),
            Key.OemPeriod => (".", 190),
            Key.OemQuestion => ("/", 191),

            _ => (key.ToString(), (int)KeyInterop.VirtualKeyFromKey(key))
        };
    }

    public string GetKeyNameFromAscii(int asciiCode)
    {
        return asciiCode switch
        {
            -1 => "Left Click",
            -2 => "Right Click",
            -3 => "Middle Click",
            9 => "Tab",
            13 => "Enter",
            16 => "Shift",
            17 => "Ctrl",
            18 => "Alt",
            20 => "CapsLock",
            27 => "Esc",
            32 => "Space",
            >= 48 and <= 57 => ((char)asciiCode).ToString(),
            >= 65 and <= 90 => ((char)asciiCode).ToString(),
            >= 97 and <= 122 => ((char)(asciiCode - 32)).ToString(),
            >= 112 and <= 123 => $"F{asciiCode - 111}",
            187 => "=",
            189 => "-",
            192 => "~",
            _ => asciiCode.ToString()
        };
    }
}
