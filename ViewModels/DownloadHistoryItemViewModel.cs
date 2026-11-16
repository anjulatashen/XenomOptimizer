using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XenomOptimizer.ViewModels;

public partial class DownloadHistoryItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = "";

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _filePath = "";

    [ObservableProperty]
    private string _detailsText = "";

    [ObservableProperty]
    private string _sizeText = "";

    [ObservableProperty]
    private string _timeText = "";

    [ObservableProperty]
    private string _iconGlyph = "\uE74C";

    [ObservableProperty]
    private string _iconColor = "#D1FE29";

    [ObservableProperty]
    private bool _isInstalled = false;

    [ObservableProperty]
    private bool _isInstalling = false;

    [ObservableProperty]
    private DownloadItemKindType _kind = DownloadItemKindType.DriverBooster;

    [ObservableProperty]
    private bool _isActionsRevealed = false;

    [ObservableProperty]
    private bool _isDownloading = false;

    [ObservableProperty]
    private double _downloadProgressPercent = 0.0;

    [ObservableProperty]
    private double _downloadProgressWidth = 0.0;

    [ObservableProperty]
    private string _speedText = "";

    [ObservableProperty]
    private bool _isDownloadCompletedFlash = false;

    [ObservableProperty]
    private double _downloadOverlayOpacity = 1.0;

    public Action<DownloadHistoryItemViewModel>? InstallRequested { get; set; }
    public Action<DownloadHistoryItemViewModel>? OpenLocationRequested { get; set; }
    public Action<DownloadHistoryItemViewModel>? DeleteRequested { get; set; }
    public Action<DownloadHistoryItemViewModel>? ActionsToggleRequested { get; set; }

    [RelayCommand]
    private void Install()
    {
        InstallRequested?.Invoke(this);
    }

    [RelayCommand]
    private void OpenLocation()
    {
        OpenLocationRequested?.Invoke(this);
    }

    [RelayCommand]
    private void Delete()
    {
        DeleteRequested?.Invoke(this);
    }

    [RelayCommand]
    private void ToggleActions()
    {
        IsActionsRevealed = !IsActionsRevealed;
        if (IsActionsRevealed)
        {
            ActionsToggleRequested?.Invoke(this);
        }
    }
}

