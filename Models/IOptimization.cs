namespace XenomOptimizer.Models;

/// <summary>
/// Defines the contract for all optimization modules.
/// Each optimization is a self-contained unit with apply, restore, and state detection.
/// </summary>
public interface IOptimization
{
    /// <summary>Unique identifier for this optimization.</summary>
    string Id { get; }

    /// <summary>Human-readable name.</summary>
    string Name { get; }

    /// <summary>Category grouping (e.g., "Power & Performance", "Gaming Settings").</summary>
    string Category { get; }

    /// <summary>Detailed description of what this optimization does.</summary>
    string Description { get; }

    /// <summary>Risk level of this optimization.</summary>
    RiskLevel RiskLevel { get; }

    /// <summary>Whether this optimization requires administrator privileges.</summary>
    bool RequiresAdmin { get; }

    /// <summary>Gets the current state/value of the setting.</summary>
    Task<OptimizationState> GetCurrentStateAsync();

    /// <summary>Applies the optimization. Returns true if successful.</summary>
    Task<OptimizationResult> ApplyAsync();

    /// <summary>Restores the original value from backup. Returns true if successful.</summary>
    Task<OptimizationResult> RestoreAsync();

    /// <summary>Checks whether this optimization is supported on the current system.</summary>
    Task<bool> IsSupportedAsync();
}

/// <summary>Risk level classification for optimizations.</summary>
public enum RiskLevel
{
    /// <summary>No risk, easily reversible.</summary>
    Low,

    /// <summary>Minor changes, may require restart.</summary>
    Medium,

    /// <summary>Significant changes, requires admin and restart.</summary>
    High
}

/// <summary>Represents the current state of an optimization setting.</summary>
public class OptimizationState
{
    /// <summary>Current value of the setting as a readable string.</summary>
    public string CurrentValue { get; set; } = "Unknown";

    /// <summary>Recommended value for optimal performance.</summary>
    public string RecommendedValue { get; set; } = "Unknown";

    /// <summary>Whether the current value matches the recommended value.</summary>
    public bool IsOptimal { get; set; }

    /// <summary>Whether this optimization is supported on the current system.</summary>
    public bool IsSupported { get; set; } = true;

    /// <summary>Additional status message.</summary>
    public string? StatusMessage { get; set; }
}

/// <summary>Result of applying or restoring an optimization.</summary>
public class OptimizationResult
{
    /// <summary>Whether the operation was successful.</summary>
    public bool Success { get; set; }

    /// <summary>Human-readable message describing the result.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Whether a system restart is required for changes to take effect.</summary>
    public bool RequiresRestart { get; set; }

    /// <summary>The previous value before the change.</summary>
    public string? PreviousValue { get; set; }

    /// <summary>The new value after the change.</summary>
    public string? NewValue { get; set; }

    public static OptimizationResult Succeeded(string message, string? previousValue = null, string? newValue = null, bool requiresRestart = false)
        => new() { Success = true, Message = message, PreviousValue = previousValue, NewValue = newValue, RequiresRestart = requiresRestart };

    public static OptimizationResult Failed(string message)
        => new() { Success = false, Message = message };
}
