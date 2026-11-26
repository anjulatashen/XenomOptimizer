using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XenomOptimizer.Models;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

public partial class KeyMappingViewModel : ObservableObject
{
    private readonly IGameLoopKeyMappingService _keyMapping;

    [ObservableProperty] private int _selectedEditionIndex = 0; // 0=Global 64-bit, 1=Global 32-bit, 2=Chinese, 3=KR/VN/BGMI
    [ObservableProperty] private string _detectedEditionText = "Detecting edition...";
    [ObservableProperty] private int _selectedKeyMapModeId = 2; // 1=Normal 720P, 2=Smart 720P, 3=Smart 1080P, 4=Smart 2K
    [ObservableProperty] private bool _enableKeyTips;
    [ObservableProperty] private bool _autoSwitchResolution = true;
    [ObservableProperty] private bool _holdShiftToRun = true;
    [ObservableProperty] private double _keyTipsBrightness = 100.0;
    [ObservableProperty] private double _horizontalSensitivity = 100.0;
    [ObservableProperty] private double _verticalSensitivity = 100.0;
    [ObservableProperty] private string _selectedKeyCategory = "All";
    [ObservableProperty] private GameLoopKeyItem? _listeningKeyItem;
    public bool IsListening => ListeningKeyItem != null;
    partial void OnListeningKeyItemChanged(GameLoopKeyItem? value) => OnPropertyChanged(nameof(IsListening));
    [ObservableProperty] private string _keyMappingStatusMessage = "";
    [ObservableProperty] private bool _isKeyMappingBusy;

    public ObservableCollection<GameLoopKeyItem> DisplayedKeyItems { get; } = new();
    private List<GameLoopKeyItem> _allKeyItems = new();

    public KeyMappingViewModel(IGameLoopKeyMappingService keyMapping)
    {
        _keyMapping = keyMapping;
        _ = LoadKeyMappingAsync();
    }

    public async Task LoadKeyMappingAsync()
    {
        try
        {
            var detected = await _keyMapping.DetectInstalledEditionAsync();
            SelectedEditionIndex = detected switch
            {
                GameLoopEdition.Global64 => 0,
                GameLoopEdition.Global32 => 1,
                GameLoopEdition.Chinese => 2,
                _ => 0
            };

            var detectedMode = _keyMapping.DetectActiveResolutionModeId();
            SelectedKeyMapModeId = detectedMode;

            await ReloadKeyProfileAsync(detectedMode);
            UpdateDetectedBadge(detected, SelectedKeyMapModeId);
        }
        catch (Exception ex)
        {
            KeyMappingStatusMessage = $"Failed to load keymapping: {ex.Message}";
        }
    }

    private void UpdateDetectedBadge(GameLoopEdition edition, int modeId)
    {
        string editionStr = edition switch
        {
            GameLoopEdition.Chinese => "Chinese (腾讯手游助手)",
            GameLoopEdition.Global64 => "Global 64-bit",
            GameLoopEdition.Global32 => "Global 32-bit",
            _ => "Global"
        };

        string modeStr = modeId switch
        {
            4 => "Smart 2K (1440P)",
            3 => "Smart 1080P (FHD)",
            2 => "Smart 720P (HD)",
            1 => "Normal 720P (SD)",
            _ => $"Mode {modeId}"
        };

        DetectedEditionText = $"Detected: {editionStr} • {modeStr}";
    }

    private async Task ReloadKeyProfileAsync(int? modeId = null)
    {
        var edition = SelectedEditionIndex switch
        {
            0 => GameLoopEdition.Global64,
            1 => GameLoopEdition.Global32,
            2 => GameLoopEdition.Chinese,
            _ => GameLoopEdition.Global64
        };

        var profile = await _keyMapping.LoadProfileAsync(edition, modeId);
        SelectedKeyMapModeId = profile.SelectedModeId;
        EnableKeyTips = profile.EnableTips;
        AutoSwitchResolution = profile.AutoSwitchResolutionMode;
        HoldShiftToRun = profile.HoldShiftToRun;
        KeyTipsBrightness = profile.BrightnessPercent;
        HorizontalSensitivity = profile.HorizontalSensitivity;
        VerticalSensitivity = profile.VerticalSensitivity;

        _allKeyItems = profile.Keys;
        RefreshDisplayedKeys();
    }

    private void RefreshDisplayedKeys()
    {
        DisplayedKeyItems.Clear();
        var filtered = SelectedKeyCategory switch
        {
            "Movement" => _allKeyItems.Where(k => k.Category == "Movement"),
            "Combat" => _allKeyItems.Where(k => k.Category == "Combat & Weapons"),
            "Grenades & Meds" => _allKeyItems.Where(k => k.Category == "Throwables & Meds"),
            "Interaction" => _allKeyItems.Where(k => k.Category == "Interaction & System"),
            _ => _allKeyItems
        };

        foreach (var k in filtered)
        {
            DisplayedKeyItems.Add(k);
        }
    }

    [RelayCommand]
    private async Task SwitchKeyMapModeAsync(object? parameter)
    {
        int modeId = 4;
        if (parameter is int i) modeId = i;
        else if (parameter != null && int.TryParse(parameter.ToString(), out int parsed)) modeId = parsed;

        SelectedKeyMapModeId = modeId;
        KeyMappingStatusMessage = $"Switching to {GetModeName(modeId)}...";
        await ReloadKeyProfileAsync(modeId);
        KeyMappingStatusMessage = $"Active Mode: {GetModeName(modeId)}";

        var edition = SelectedEditionIndex switch
        {
            0 => GameLoopEdition.Global64,
            1 => GameLoopEdition.Global32,
            2 => GameLoopEdition.Chinese,
            _ => GameLoopEdition.Global64
        };
        UpdateDetectedBadge(edition, modeId);
    }

    [RelayCommand]
    private async Task SelectEditionAsync(object? parameter)
    {
        int index = 0;
        if (parameter is int i) index = i;
        else if (parameter != null && int.TryParse(parameter.ToString(), out int parsed)) index = parsed;

        SelectedEditionIndex = index;
        KeyMappingStatusMessage = $"Loading {GetEditionName(index)} keymap...";
        await ReloadKeyProfileAsync(SelectedKeyMapModeId);
        KeyMappingStatusMessage = $"Loaded {GetEditionName(index)} • {GetModeName(SelectedKeyMapModeId)}.";

        var edition = index switch
        {
            0 => GameLoopEdition.Global64,
            1 => GameLoopEdition.Global32,
            2 => GameLoopEdition.Chinese,
            _ => GameLoopEdition.Global64
        };
        UpdateDetectedBadge(edition, SelectedKeyMapModeId);
    }

    [RelayCommand]
    private void FilterKeyCategory(object? parameter)
    {
        SelectedKeyCategory = parameter?.ToString() ?? "All";
        RefreshDisplayedKeys();
    }

    [RelayCommand]
    private void StartRebindKey(GameLoopKeyItem? item)
    {
        if (item == null) return;

        // Cancel previous listening if any
        if (ListeningKeyItem != null)
        {
            ListeningKeyItem.IsListeningForRebind = false;
        }

        ListeningKeyItem = item;
        item.IsListeningForRebind = true;
        KeyMappingStatusMessage = $"Press any key on keyboard or click a mouse action to rebind '{item.DisplayName}'...";
    }

    [RelayCommand]
    private void CancelRebindKey()
    {
        if (ListeningKeyItem != null)
        {
            ListeningKeyItem.IsListeningForRebind = false;
            ListeningKeyItem = null;
        }
        KeyMappingStatusMessage = "Rebind cancelled.";
    }

    public void ApplyRebind(Key key)
    {
        if (ListeningKeyItem == null) return;

        var (displayKey, ascii) = _keyMapping.ConvertWpfKey(key);
        ListeningKeyItem.CurrentKey = displayKey;
        ListeningKeyItem.AsciiCode = ascii;
        ListeningKeyItem.IsListeningForRebind = false;

        KeyMappingStatusMessage = $"Rebound '{ListeningKeyItem.DisplayName}' to [{displayKey}]";
        ListeningKeyItem = null;
    }

    [RelayCommand]
    public void ApplyMouseRebind(string? buttonName)
    {
        if (ListeningKeyItem == null || string.IsNullOrEmpty(buttonName)) return;

        int ascii = buttonName switch
        {
            "Left Click" => -1,
            "Right Click" => -2,
            "Middle Click" => -3,
            _ => -1
        };

        ListeningKeyItem.CurrentKey = buttonName;
        ListeningKeyItem.AsciiCode = ascii;
        ListeningKeyItem.IsListeningForRebind = false;

        KeyMappingStatusMessage = $"Rebound '{ListeningKeyItem.DisplayName}' to [{buttonName}]";
        ListeningKeyItem = null;
    }

    [RelayCommand]
    private async Task SaveKeyMappingAsync()
    {
        IsKeyMappingBusy = true;
        KeyMappingStatusMessage = "Saving keymapping to GameLoop XML & Registry...";
        try
        {
            var profile = new GameLoopKeyMappingProfile
            {
                Edition = SelectedEditionIndex switch
                {
                    0 => GameLoopEdition.Global64,
                    1 => GameLoopEdition.Global32,
                    2 => GameLoopEdition.Chinese,
                    _ => GameLoopEdition.Global64
                },
                SelectedModeId = SelectedKeyMapModeId,
                EnableTips = EnableKeyTips,
                AutoSwitchResolutionMode = AutoSwitchResolution,
                HoldShiftToRun = HoldShiftToRun,
                BrightnessPercent = KeyTipsBrightness,
                HorizontalSensitivity = HorizontalSensitivity,
                VerticalSensitivity = VerticalSensitivity,
                Keys = _allKeyItems
            };

            bool success = await _keyMapping.SaveProfileAsync(profile);
            if (success)
            {
                KeyMappingStatusMessage = "KeyMapping saved successfully! Changes active immediately in GameLoop.";
            }
            else
            {
                KeyMappingStatusMessage = "Failed to save KeyMapping. Run as Administrator or check GameLoop installation.";
            }
        }
        catch (Exception ex)
        {
            KeyMappingStatusMessage = $"Error saving: {ex.Message}";
        }
        finally
        {
            IsKeyMappingBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetKeyMappingDefaultsAsync()
    {
        IsKeyMappingBusy = true;
        KeyMappingStatusMessage = "Resetting KeyMapping to GameLoop stock defaults...";
        try
        {
            var edition = SelectedEditionIndex switch
            {
                0 => GameLoopEdition.Global64,
                1 => GameLoopEdition.Global32,
                2 => GameLoopEdition.Chinese,
                _ => GameLoopEdition.Global64
            };

            var defaultProfile = await _keyMapping.ResetToDefaultsAsync(edition, SelectedKeyMapModeId);
            _allKeyItems = defaultProfile.Keys;
            EnableKeyTips = defaultProfile.EnableTips;
            AutoSwitchResolution = defaultProfile.AutoSwitchResolutionMode;
            HoldShiftToRun = defaultProfile.HoldShiftToRun;
            KeyTipsBrightness = defaultProfile.BrightnessPercent;
            HorizontalSensitivity = defaultProfile.HorizontalSensitivity;
            VerticalSensitivity = defaultProfile.VerticalSensitivity;

            RefreshDisplayedKeys();
            await SaveKeyMappingAsync();
        }
        catch (Exception ex)
        {
            KeyMappingStatusMessage = $"Error resetting defaults: {ex.Message}";
        }
        finally
        {
            IsKeyMappingBusy = false;
        }
    }

    private static string GetEditionName(int index) => index switch
    {
        0 => "Global 64-bit",
        1 => "Global 32-bit",
        2 => "Chinese Edition (腾讯手游助手)",
        _ => "Standard"
    };

    private static string GetModeName(int modeId) => modeId switch
    {
        1 => "Normal 720P",
        2 => "Smart 720P",
        3 => "Smart 1080P",
        4 => "Smart 2K",
        _ => $"Mode {modeId}"
    };
}
