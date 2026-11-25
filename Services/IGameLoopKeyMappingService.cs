using System.Windows.Input;
using XenomOptimizer.Models;

namespace XenomOptimizer.Services;

/// <summary>
/// Service for managing GameLoop KeyMapping modes, bindings, and secondary settings across all editions.
/// </summary>
public interface IGameLoopKeyMappingService
{
    /// <summary>
    /// Detects the primarily installed GameLoop edition (Global 64-bit, Global 32-bit, or Chinese).
    /// </summary>
    Task<GameLoopEdition> DetectInstalledEditionAsync();

    /// <summary>
    /// Loads key mapping profile for specified edition and mode.
    /// If modeId is null, loads the last active mode.
    /// </summary>
    Task<GameLoopKeyMappingProfile> LoadProfileAsync(GameLoopEdition edition, int? modeId = null);

    /// <summary>
    /// Saves modified key mapping profile into GameLoop's XML and registry.
    /// </summary>
    Task<bool> SaveProfileAsync(GameLoopKeyMappingProfile profile);

    /// <summary>
    /// Resets key mapping bindings to GameLoop defaults for specified edition and mode.
    /// </summary>
    Task<GameLoopKeyMappingProfile> ResetToDefaultsAsync(GameLoopEdition edition, int modeId);

    /// <summary>
    /// Converts WPF Key event to GameLoop ASCII code and readable display name.
    /// </summary>
    (string displayKey, int asciiCode) ConvertWpfKey(Key key);

    /// <summary>
    /// Converts GameLoop ASCII code to readable key name.
    /// </summary>
    string GetKeyNameFromAscii(int asciiCode);
}
