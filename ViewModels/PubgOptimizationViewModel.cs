using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

/// <summary>
/// ViewModel for PUBG-specific performance optimizations.
/// Detects PUBG configuration paths and provides safe optimizations.
/// Does NOT modify anti-cheat, game memory, or gameplay mechanics.
/// </summary>
public partial class PubgOptimizationViewModel : ObservableObject
{
    private readonly IGameLoopDetectionService _gameLoop;
    private readonly IBackupService _backup;
    private readonly ILogService _log;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _isDetected;
    [ObservableProperty] private string _configPath = "Detecting...";
    [ObservableProperty] private string _statusMessage = "";

    public ObservableCollection<OptimizationCardViewModel> Optimizations { get; } = new();

    public PubgOptimizationViewModel(IGameLoopDetectionService gameLoop, IBackupService backup, ILogService log)
    {
        _gameLoop = gameLoop;
        _backup = backup;
        _log = log;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            IsDetected = await _gameLoop.IsPubgInstalledAsync();
            if (IsDetected)
            {
                ConfigPath = await _gameLoop.GetPubgConfigPathAsync() ?? "Configuration path not found";
                // PUBG-specific optimizations will be loaded here in Phase 4
            }
            else
            {
                ConfigPath = "N/A";
                StatusMessage = "PUBG Mobile not detected in GameLoop. Install PUBG Mobile through GameLoop to enable these optimizations.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
