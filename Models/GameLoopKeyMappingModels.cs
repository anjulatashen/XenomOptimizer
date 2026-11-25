using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace XenomOptimizer.Models;

/// <summary>
/// Supported GameLoop emulator editions.
/// </summary>
public enum GameLoopEdition
{
    Global64,
    Global32,
    Chinese,
    KoreaJapan,
    Vietnam,
    IndiaBgmi
}

/// <summary>
/// KeyMapping mode matching GameLoop's resolution calibration.
/// </summary>
public class GameLoopKeyMapMode
{
    public int ModeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Represents a single key binding in GameLoop.
/// </summary>
public partial class GameLoopKeyItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = "General"; // Movement, Combat, Grenades & Meds, Interaction & System
    public string Description { get; set; } = string.Empty;
    public string Description_XML { get; set; } = string.Empty;
    public string DefaultKey { get; set; } = string.Empty;

    [ObservableProperty] private string _currentKey = string.Empty;
    [ObservableProperty] private int _asciiCode;
    [ObservableProperty] private bool _isListeningForRebind;

    public bool IsEx { get; set; } // KeyMappingEx vs KeyMapping
    public string XmlItemName { get; set; } = string.Empty;
    public string XmlAttribute { get; set; } = "AsciiCode"; // UP_AsciiCode, DOWN_AsciiCode, LEFT_AsciiCode, RIGHT_AsciiCode, AsciiCode, etc.
    public string XmlType { get; set; } = string.Empty; // CrossKey, LClick, RClick, KeyMove, FoldClick2, MultiPoint
}

/// <summary>
/// Profile containing current KeyMapping settings for an edition and mode.
/// </summary>
public class GameLoopKeyMappingProfile
{
    public GameLoopEdition Edition { get; set; } = GameLoopEdition.Global64;
    public int SelectedModeId { get; set; } = 2; // Default Smart 720P or Smart 1080P
    public bool EnableTips { get; set; } // Display key mapping tips (F1)
    public bool AutoSwitchResolutionMode { get; set; } = true; // Auto switch mode based on screen resolution
    public bool HoldShiftToRun { get; set; } = true; // Hold Shift to run
    public double BrightnessPercent { get; set; } = 100.0;
    public double TipsTransparencyPercent { get; set; } = 50.0;
    public double HorizontalSensitivity { get; set; } = 100.0;
    public double VerticalSensitivity { get; set; } = 100.0;

    public List<GameLoopKeyItem> Keys { get; set; } = new();
}
