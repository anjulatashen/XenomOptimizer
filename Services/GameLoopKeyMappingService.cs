using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Microsoft.Win32;
using XenomOptimizer.Models;

namespace XenomOptimizer.Services;

/// <summary>
/// Implements full keymapping management for GameLoop across Global 32-bit, Global 64-bit, and Chinese editions.
/// </summary>
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

        // Check for 64-bit Rootfs / vshell / 64-bit AppMarket
        bool is64Bit = Directory.Exists(Path.Combine(installPath, "AOW_Rootfs_100")) ||
                       Directory.Exists(Path.Combine(installPath, "vshell")) ||
                       File.Exists(Path.Combine(installPath, "AOW_Rootfs_100", "init"));

        // Check for Chinese version in registry or directory
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Tencent\MobileGamePC");
            var region = key?.GetValue("Region")?.ToString();
            if (string.Equals(region, "CN", StringComparison.OrdinalIgnoreCase))
            {
                // If it has Peace Elite installed or CN region
                return is64Bit ? GameLoopEdition.Global64 : GameLoopEdition.Global64;
            }
        }
        catch { }

        // Check DefaultKeyMapping.xml for active APK
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

    public async Task<GameLoopKeyMappingProfile> LoadProfileAsync(GameLoopEdition edition, int? modeId = null)
    {
        var profile = new GameLoopKeyMappingProfile
        {
            Edition = edition,
            SelectedModeId = modeId ?? 2
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

            if (itemMatch.Success)
            {
                var itemText = itemMatch.Groups[1].Value;

                // Read LastMode
                var lastModeMatch = Regex.Match(itemText, @"<LastMode\s+([^>]+)/>");
                if (lastModeMatch.Success)
                {
                    var attrs = lastModeMatch.Groups[1].Value;
                    if (modeId == null)
                    {
                        var modeMatch = Regex.Match(attrs, @"ModeID=""(\d+)""");
                        if (modeMatch.Success && int.TryParse(modeMatch.Groups[1].Value, out int id))
                        {
                            profile.SelectedModeId = Math.Clamp(id, 1, 4);
                        }
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
                    var shiftVal = hkcu?.GetValue("shift_run_pubgmhd");
                    if (shiftVal is int iVal)
                    {
                        profile.HoldShiftToRun = iVal == 1;
                    }
                    else if (shiftVal is string sVal && int.TryParse(sVal, out int parsed))
                    {
                        profile.HoldShiftToRun = parsed == 1;
                    }
                }
                catch { }

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
            var apksToUpdate = new List<string> { primaryApk };
            if (!string.IsNullOrEmpty(smartApk))
            {
                apksToUpdate.Add(smartApk);
            }

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
                using var hkcu = Registry.CurrentUser.CreateSubKey(@"Software\Tencent\MobileGamePC");
                hkcu?.SetValue("shift_run_pubgmhd", profile.HoldShiftToRun ? 1 : 0, RegistryValueKind.DWord);
                hkcu?.SetValue("KeyMindTipsPressed", profile.EnableTips ? 1 : 0, RegistryValueKind.DWord);
            }
            catch (Exception regEx)
            {
                _log.Log("KeyMapping", $"Warning writing registry: {regEx.Message}", "WARNING");
            }

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
        itemXml = Regex.Replace(itemXml, @"<LastMode\s+([^>]+)/>", m =>
        {
            var attrs = m.Groups[1].Value;
            attrs = Regex.Replace(attrs, @"ModeID=""\d+""", $@"ModeID=""{modeStr}""");
            attrs = Regex.Replace(attrs, @"EnableTips=""\d+""", $@"EnableTips=""{tipsStr}""");
            attrs = Regex.Replace(attrs, @"EnableGameKeyDT=""\d+""", $@"EnableGameKeyDT=""{dtStr}""");
            attrs = Regex.Replace(attrs, @"Lightness=""[\d\.]+""", $@"Lightness=""{lightnessStr}""");
            attrs = Regex.Replace(attrs, @"TipsTransparent=""[\d\.]+""", $@"TipsTransparent=""{transStr}""");
            return $"<LastMode {attrs}/>";
        });

        // 2. Find KeyMapMode for SelectedModeId
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
                    @"<KeyMappingEx\s+([^>]*ItemName=""WASD""[^>]*)/>",
                    m =>
                    {
                        var attrs = m.Groups[1].Value;
                        if (key.Id == "MoveUp") attrs = Regex.Replace(attrs, @"UP_AsciiCode=""\d+""", $@"UP_AsciiCode=""{key.AsciiCode}""");
                        if (key.Id == "MoveDown") attrs = Regex.Replace(attrs, @"DOWN_AsciiCode=""\d+""", $@"DOWN_AsciiCode=""{key.AsciiCode}""");
                        if (key.Id == "MoveLeft") attrs = Regex.Replace(attrs, @"LEFT_AsciiCode=""\d+""", $@"LEFT_AsciiCode=""{key.AsciiCode}""");
                        if (key.Id == "MoveRight") attrs = Regex.Replace(attrs, @"RIGHT_AsciiCode=""\d+""", $@"RIGHT_AsciiCode=""{key.AsciiCode}""");
                        return $"<KeyMappingEx {attrs}/>";
                    });
            }
            else if (key.IsEx)
            {
                // KeyMappingEx with ItemName or Type
                string searchPattern = !string.IsNullOrEmpty(key.XmlItemName)
                    ? $@"<KeyMappingEx\s+([^>]*ItemName=""{Regex.Escape(key.XmlItemName)}""[^>]*)/>"
                    : $@"<KeyMappingEx\s+([^>]*Type=""{Regex.Escape(key.XmlType)}""[^>]*)/>";

                modifiedMode = Regex.Replace(modifiedMode, searchPattern, m =>
                {
                    var attrs = m.Groups[1].Value;
                    if (attrs.Contains("AsciiCode="))
                    {
                        attrs = Regex.Replace(attrs, @"AsciiCode=""\d+""", $@"AsciiCode=""{key.AsciiCode}""");
                    }
                    if (!string.IsNullOrEmpty(key.CurrentKey) && attrs.Contains("ItemName="))
                    {
                        attrs = Regex.Replace(attrs, @"ItemName=""[^""]*""", $@"ItemName=""{key.CurrentKey}""");
                    }
                    return $"<KeyMappingEx {attrs}/>";
                });
            }
            else
            {
                // Standard KeyMapping
                // Match by either ItemName or Description
                string searchPattern = $@"<KeyMapping\s+([^>]*AsciiCode=""[^""]*""[^>]*)/>";
                // Target exact key mapping by default item name or description
                if (!string.IsNullOrEmpty(key.XmlItemName))
                {
                    searchPattern = $@"<KeyMapping\s+([^>]*ItemName=""{Regex.Escape(key.XmlItemName)}""[^>]*)/>";
                    modifiedMode = Regex.Replace(modifiedMode, searchPattern, m =>
                    {
                        var attrs = m.Groups[1].Value;
                        attrs = Regex.Replace(attrs, @"AsciiCode=""[^""]*""", $@"AsciiCode=""{key.AsciiCode}""");
                        attrs = Regex.Replace(attrs, @"ItemName=""[^""]*""", $@"ItemName=""{key.CurrentKey}""");
                        return $"<KeyMapping {attrs}/>";
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

        // Parse WASD CrossKey
        var wasdMatch = Regex.Match(modeText, @"<KeyMappingEx\s+[^>]*ItemName=""WASD""[^>]*UP_AsciiCode=""(\d+)""[^>]*DOWN_AsciiCode=""(\d+)""[^>]*LEFT_AsciiCode=""(\d+)""[^>]*RIGHT_AsciiCode=""(\d+)""");
        if (wasdMatch.Success)
        {
            SetKeyByAscii(keys, "MoveUp", int.Parse(wasdMatch.Groups[1].Value));
            SetKeyByAscii(keys, "MoveDown", int.Parse(wasdMatch.Groups[2].Value));
            SetKeyByAscii(keys, "MoveLeft", int.Parse(wasdMatch.Groups[3].Value));
            SetKeyByAscii(keys, "MoveRight", int.Parse(wasdMatch.Groups[4].Value));
        }

        // Parse single keys
        var keyMatches = Regex.Matches(modeText, @"<KeyMapping\s+([^>]+)/>");
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

                // Match against existing keys by Name or Desc
                foreach (var k in keys.Where(x => !x.IsEx))
                {
                    if ((!string.IsNullOrEmpty(k.XmlItemName) && k.XmlItemName.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(k.Description) && k.Description.Equals(desc, StringComparison.OrdinalIgnoreCase)))
                    {
                        k.AsciiCode = ascii;
                        k.CurrentKey = GetKeyNameFromAscii(ascii);
                    }
                }
            }
        }

        // Parse KeyMappingEx items (Alt freelook, Grenades, Meds, etc.)
        var exMatches = Regex.Matches(modeText, @"<KeyMappingEx\s+([^>]+)/>");
        foreach (Match em in exMatches)
        {
            var raw = em.Groups[1].Value;
            var nameMatch = Regex.Match(raw, @"ItemName=""([^""]*)""");
            var asciiMatch = Regex.Match(raw, @"AsciiCode=""(\d+)""");
            var typeMatch = Regex.Match(raw, @"Type=""([^""]+)""");

            if (asciiMatch.Success && int.TryParse(asciiMatch.Groups[1].Value, out int ascii))
            {
                string name = nameMatch.Success ? nameMatch.Groups[1].Value : "";
                string type = typeMatch.Success ? typeMatch.Groups[1].Value : "";

                foreach (var k in keys.Where(x => x.IsEx && x.Id != "MoveUp" && x.Id != "MoveDown" && x.Id != "MoveLeft" && x.Id != "MoveRight"))
                {
                    if ((!string.IsNullOrEmpty(k.XmlItemName) && k.XmlItemName.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(k.XmlType) && k.XmlType.Equals(type, StringComparison.OrdinalIgnoreCase)))
                    {
                        k.AsciiCode = ascii;
                        k.CurrentKey = GetKeyNameFromAscii(ascii);
                    }
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
            new() { Id = "Fire", DisplayName = "Fire / Shoot", Category = "Combat & Weapons", Description = "Trigger primary weapon fire", DefaultKey = "Left Click", CurrentKey = "Left Click", AsciiCode = -1, IsEx = true, XmlItemName = "鼠标左键", XmlType = "LClick" },
            new() { Id = "Aim", DisplayName = "Aim Down Sight (ADS)", Category = "Combat & Weapons", Description = "Scope / right click aim", DefaultKey = "Right Click", CurrentKey = "Right Click", AsciiCode = -2, IsEx = false, XmlItemName = "右键" },
            new() { Id = "Reload", DisplayName = "Reload Weapon", Category = "Combat & Weapons", Description = "Reload active magazine", DefaultKey = "R", CurrentKey = "R", AsciiCode = 82, IsEx = false, XmlItemName = "R" },
            new() { Id = "PeekLeft", DisplayName = "Lean / Peek Left", Category = "Combat & Weapons", Description = "Lean body left behind cover", DefaultKey = "Q", CurrentKey = "Q", AsciiCode = 81, IsEx = false, XmlItemName = "Q" },
            new() { Id = "PeekRight", DisplayName = "Lean / Peek Right", Category = "Combat & Weapons", Description = "Lean body right behind cover", DefaultKey = "E", CurrentKey = "E", AsciiCode = 69, IsEx = false, XmlItemName = "E" },
            new() { Id = "FireMode", DisplayName = "Switch Fire Mode", Category = "Combat & Weapons", Description = "Single / Burst / Full-Auto", DefaultKey = "B", CurrentKey = "B", AsciiCode = 66, IsEx = true, XmlItemName = "B", XmlType = "MultiPoint" },
            new() { Id = "Weapon1", DisplayName = "Primary Weapon", Category = "Combat & Weapons", Description = "Equip 1st main gun", DefaultKey = "1", CurrentKey = "1", AsciiCode = 49, IsEx = false, XmlItemName = "1" },
            new() { Id = "Weapon2", DisplayName = "Secondary Weapon", Category = "Combat & Weapons", Description = "Equip 2nd main gun", DefaultKey = "2", CurrentKey = "2", AsciiCode = 50, IsEx = false, XmlItemName = "2" },
            new() { Id = "Sidearm", DisplayName = "Pistol / Sidearm", Category = "Combat & Weapons", Description = "Equip pistol", DefaultKey = "3", CurrentKey = "3", AsciiCode = 51, IsEx = false, XmlItemName = "3" },
            new() { Id = "Melee", DisplayName = "Holster / Melee Weapon", Category = "Combat & Weapons", Description = "Put away guns / hold melee weapon", DefaultKey = "X", CurrentKey = "X", AsciiCode = 88, IsEx = true, XmlItemName = "X", XmlType = "FoldClick2" },

            // Throwables & Meds
            new() { Id = "Frag", DisplayName = "Frag Grenade", Category = "Throwables & Meds", Description = "Quick select frag grenade", DefaultKey = "4", CurrentKey = "4", AsciiCode = 52, IsEx = true, XmlItemName = "4", XmlType = "FoldClick2" },
            new() { Id = "Smoke", DisplayName = "Smoke Grenade", Category = "Throwables & Meds", Description = "Quick select smoke grenade", DefaultKey = "5", CurrentKey = "5", AsciiCode = 53, IsEx = true, XmlItemName = "5", XmlType = "FoldClick2" },
            new() { Id = "Molotov", DisplayName = "Molotov Cocktail", Category = "Throwables & Meds", Description = "Quick select fire bomb", DefaultKey = "6", CurrentKey = "6", AsciiCode = 54, IsEx = true, XmlItemName = "6", XmlType = "FoldClick2" },
            new() { Id = "MedKit", DisplayName = "Med Kit (100% HP)", Category = "Throwables & Meds", Description = "Quick use Med Kit", DefaultKey = "7", CurrentKey = "7", AsciiCode = 55, IsEx = true, XmlItemName = "7", XmlType = "FoldClick2" },
            new() { Id = "FirstAid", DisplayName = "First Aid Kit (75% HP)", Category = "Throwables & Meds", Description = "Quick use First Aid Kit", DefaultKey = "8", CurrentKey = "8", AsciiCode = 56, IsEx = true, XmlItemName = "8", XmlType = "FoldClick2" },
            new() { Id = "Bandage", DisplayName = "Bandage (10% HP)", Category = "Throwables & Meds", Description = "Quick use Bandage", DefaultKey = "9", CurrentKey = "9", AsciiCode = 57, IsEx = true, XmlItemName = "9", XmlType = "FoldClick2" },
            new() { Id = "EnergyDrink", DisplayName = "Energy Drink / Painkiller", Category = "Throwables & Meds", Description = "Quick use boost drink/pills", DefaultKey = "0", CurrentKey = "0", AsciiCode = 48, IsEx = true, XmlItemName = "0", XmlType = "FoldClick2" },

            // Interaction & System
            new() { Id = "InteractF", DisplayName = "Main Action / Drive / Loot", Category = "Interaction & System", Description = "Drive, open doors, pick up item 1, parachute, revive", DefaultKey = "F", CurrentKey = "F", AsciiCode = 70, IsEx = false, XmlItemName = "F" },
            new() { Id = "InteractG", DisplayName = "Passenger / Secondary Loot", Category = "Interaction & System", Description = "Get in as passenger, pick up item 2", DefaultKey = "G", CurrentKey = "G", AsciiCode = 71, IsEx = false, XmlItemName = "G" },
            new() { Id = "InteractH", DisplayName = "Third Loot Slot", Category = "Interaction & System", Description = "Pick up item 3", DefaultKey = "H", CurrentKey = "H", AsciiCode = 72, IsEx = false, XmlItemName = "H" },
            new() { Id = "Backpack", DisplayName = "Inventory / Backpack", Category = "Interaction & System", Description = "Open player bag", DefaultKey = "Tab", CurrentKey = "Tab", AsciiCode = 9, IsEx = false, XmlItemName = "Tab" },
            new() { Id = "Map", DisplayName = "Full Map", Category = "Interaction & System", Description = "Open / close tactical map", DefaultKey = "M", CurrentKey = "M", AsciiCode = 77, IsEx = false, XmlItemName = "M" },
            new() { Id = "MouseLock", DisplayName = "Mouse Lock / Free Cursor", Category = "Interaction & System", Description = "Show or lock mouse cursor in game", DefaultKey = "Ctrl", CurrentKey = "Ctrl", AsciiCode = 17, IsEx = true, XmlItemName = "鼠标移动", XmlType = "RClick" },
            new() { Id = "KeyReminder", DisplayName = "Key Reminder Overlay", Category = "Interaction & System", Description = "Toggle key overlay on screen", DefaultKey = "F1", CurrentKey = "F1", AsciiCode = 112, IsEx = false, XmlItemName = "F1" }
        };
    }

    private (string primary, string smart) GetApkNames(GameLoopEdition edition)
    {
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
            results.Add(Path.Combine(installPath, "ui", "DefaultKeyMapping.xml"));
            results.Add(Path.Combine(installPath, "ui", "ConfigFile", "DefaultKeyMapping.xml"));
        }

        // Common default locations
        var commonRoots = new[]
        {
            @"C:\Program Files\TxGameAssistant",
            @"D:\Program Files\TxGameAssistant",
            @"E:\Program Files\TxGameAssistant",
            @"C:\TxGameAssistant",
            @"D:\TxGameAssistant"
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
