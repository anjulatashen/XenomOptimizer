using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

/// <summary>
/// Main ViewModel that manages sidebar navigation and the current page.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IAdminService _adminService;
    private readonly ISettingsService? _settingsService;
    private readonly IGameLoopLauncherService _launcherService;

    [ObservableProperty]
    private ObservableObject? _currentPage;

    [ObservableProperty]
    private string _currentPageTitle = "Dashboard";

    public bool IsDashboardActive => CurrentPageTitle == "Dashboard";
    public bool IsWindowsActive => CurrentPageTitle == "Windows";
    public bool IsOptimizerActive => CurrentPageTitle == "Optimizer";
    public bool IsGameLoopActive => CurrentPageTitle == "GameLoop";
    public bool IsKeyMappingActive => CurrentPageTitle == "KeyMapping";
    public bool IsRegistryActive => CurrentPageTitle == "Registry";
    public bool IsPubgActive => CurrentPageTitle == "PUBG";
    public bool IsNetworkActive => CurrentPageTitle == "Network";
    public bool IsCleanupActive => CurrentPageTitle == "Cleanup";
    public bool IsBackupActive => CurrentPageTitle == "Backup";
    public bool IsSystemActive => CurrentPageTitle == "System";
    public bool IsSettingsActive => CurrentPageTitle == "Settings";

    partial void OnCurrentPageTitleChanged(string value)
    {
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsWindowsActive));
        OnPropertyChanged(nameof(IsOptimizerActive));
        OnPropertyChanged(nameof(IsGameLoopActive));
        OnPropertyChanged(nameof(IsKeyMappingActive));
        OnPropertyChanged(nameof(IsRegistryActive));
        OnPropertyChanged(nameof(IsPubgActive));
        OnPropertyChanged(nameof(IsNetworkActive));
        OnPropertyChanged(nameof(IsCleanupActive));
        OnPropertyChanged(nameof(IsBackupActive));
        OnPropertyChanged(nameof(IsSystemActive));
        OnPropertyChanged(nameof(IsSettingsActive));
    }

    [ObservableProperty]
    private bool _isAdmin;

    [ObservableProperty]
    private int _selectedNavIndex;

    public WindowsOptimizationViewModel WindowsVm { get; }

    [ObservableProperty]
    private bool _isDownloadModalActive;

    [ObservableProperty]
    private bool _isDriverBoosterPromptActive;

    [ObservableProperty]
    private bool _isDriverWarningModalActive;

    [ObservableProperty]
    private bool _isInstructionModalActive;

    [ObservableProperty]
    private bool _isPasscodeModalActive;

    [ObservableProperty]
    private bool _isGameLoopUnlocked = false;

    [ObservableProperty]
    private bool _isAppDrawerOpen = false;

    [ObservableProperty]
    private string _appDrawerStatusMessage = "Ready to launch GameLoop apps";

    private static readonly HashSet<string> LockedTabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "GameLoop",
        "PUBG",
        "Network",
        "Cleanup",
        "Backup",
        "System"
    };

    private string? _pendingLockedPage;

    public bool CanNavigateToOtherTabs =>
        WindowsVm?.IsScanningDrivers != true &&
        WindowsVm?.IsRescanningDrivers != true &&
        WindowsVm?.IsRescanCompleted != true &&
        WindowsVm?.IsCustomPresetEditActive != true;

    public MainViewModel(IAdminService adminService, WindowsOptimizationViewModel windowsVm, IGameLoopLauncherService launcherService)
    {
        _adminService = adminService;
        WindowsVm = windowsVm;
        _launcherService = launcherService;
        WindowsVm.RequestPageNavigation = page => NavigateTo(page);
        _isAdmin = adminService.IsRunningAsAdmin;
        _isDownloadModalActive = WindowsVm.IsDownloadFlyoutOpen;
        _isDriverBoosterPromptActive = WindowsVm.IsDriverBoosterPromptVisible;
        _isDriverWarningModalActive = WindowsVm.IsDriverWarningModalVisible;
        _isInstructionModalActive = WindowsVm.IsInstructionModalVisible;

        WindowsVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsDownloadFlyoutOpen))
            {
                IsDownloadModalActive = WindowsVm.IsDownloadFlyoutOpen;
            }
            else if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsDriverBoosterPromptVisible))
            {
                IsDriverBoosterPromptActive = WindowsVm.IsDriverBoosterPromptVisible;
            }
            else if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsDriverWarningModalVisible))
            {
                IsDriverWarningModalActive = WindowsVm.IsDriverWarningModalVisible;
            }
            else if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsInstructionModalVisible))
            {
                IsInstructionModalActive = WindowsVm.IsInstructionModalVisible;
            }
            else if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsScanningDrivers) ||
                     e.PropertyName == nameof(WindowsOptimizationViewModel.IsRescanningDrivers) ||
                     e.PropertyName == nameof(WindowsOptimizationViewModel.IsRescanCompleted) ||
                     e.PropertyName == nameof(WindowsOptimizationViewModel.IsCustomPresetEditActive))
            {
                OnPropertyChanged(nameof(CanNavigateToOtherTabs));
            }
        };

        _pageCache["Windows"] = WindowsVm;

        // Always launch on Dashboard (Home)
        _settingsService = App.Services.GetService(typeof(ISettingsService)) as ISettingsService;
        NavigateTo("Dashboard");
    }

    private readonly Dictionary<string, ObservableObject?> _pageCache = new();

    [RelayCommand]
    private void NavigateTo(string page)
    {
        if (WindowsVm?.IsCustomPresetEditActive == true && page != "Windows")
        {
            WindowsVm.TriggerShakeCategory();
            return;
        }

        if ((WindowsVm?.IsScanningDrivers == true || WindowsVm?.IsRescanningDrivers == true || WindowsVm?.IsRescanCompleted == true) && page != "Windows")
        {
            return;
        }

        // Restrict the 6 unfinished tabs with Passcode 10105
        if (LockedTabs.Contains(page) && !IsGameLoopUnlocked)
        {
            _pendingLockedPage = page;
            IsPasscodeModalActive = true;
            OnCurrentPageTitleChanged(CurrentPageTitle);
            return;
        }

        if (CurrentPageTitle == page && CurrentPage != null)
        {
            return;
        }

        CurrentPageTitle = page;



        // Step tracking for Onboarding Guide
        if (page == "Windows")
        {
            WindowsVm?.MarkGuideStepCompleted((WindowsVm.SelectedSlideIndex) + 1);
        }
        else if (page == "Registry")
        {
            WindowsVm?.MarkGuideStepCompleted(4);
        }
        else if (page == "GameLoop" || page == "PUBG" || page == "KeyMapping")
        {
            WindowsVm?.MarkGuideStepCompleted(5);
        }
        else if (page == "Cleanup" || page == "Network")
        {
            WindowsVm?.MarkGuideStepCompleted(6);
        }

        if (!_pageCache.TryGetValue(page, out var cachedVm) || cachedVm == null)
        {
            cachedVm = page switch
            {
                "Dashboard" => App.Services.GetService(typeof(DashboardViewModel)) as ObservableObject,
                "Windows" => WindowsVm,
                "Optimizer" => App.Services.GetService(typeof(OptimizerFrameGenViewModel)) as ObservableObject,
                "KeyMapping" => App.Services.GetService(typeof(KeyMappingViewModel)) as ObservableObject,
                "GameLoop" => App.Services.GetService(typeof(GameLoopOptimizationViewModel)) as ObservableObject,
                "Registry" => App.Services.GetService(typeof(RegistryOptimizationViewModel)) as ObservableObject,
                "PUBG" => App.Services.GetService(typeof(PubgOptimizationViewModel)) as ObservableObject,
                "Network" => App.Services.GetService(typeof(NetworkOptimizationViewModel)) as ObservableObject,
                "Cleanup" => App.Services.GetService(typeof(CleanupViewModel)) as ObservableObject,
                "Backup" => App.Services.GetService(typeof(BackupRestoreViewModel)) as ObservableObject,
                "System" => App.Services.GetService(typeof(SystemInfoViewModel)) as ObservableObject,
                "Settings" => App.Services.GetService(typeof(SettingsViewModel)) as ObservableObject,
                _ => App.Services.GetService(typeof(DashboardViewModel)) as ObservableObject
            };
            if (cachedVm != null)
            {
                _pageCache[page] = cachedVm;
            }
        }
        CurrentPage = cachedVm;
        if (cachedVm is DashboardViewModel dvm)
        {
            dvm.TriggerEntranceAnimation();
        }
        else if (cachedVm is WindowsOptimizationViewModel wvm)
        {
            wvm.TriggerEntranceAnimation();
        }
    }

    [RelayCommand]
    private void RequestAdmin()
    {
        _adminService.RequestElevation();
    }

    [RelayCommand]
    public void VerifyPasscode(string enteredCode)
    {
        if (enteredCode == "10105")
        {
            IsGameLoopUnlocked = true;
            IsPasscodeModalActive = false;
            if (!string.IsNullOrEmpty(_pendingLockedPage))
            {
                string target = _pendingLockedPage;
                _pendingLockedPage = null;
                NavigateTo(target);
            }
        }
    }

    [RelayCommand]
    public void ClosePasscodeModal()
    {
        IsPasscodeModalActive = false;
        _pendingLockedPage = null;
        OnCurrentPageTitleChanged(CurrentPageTitle);
    }

    [RelayCommand]
    public void ToggleAppDrawer()
    {
        IsAppDrawerOpen = !IsAppDrawerOpen;
    }

    [RelayCommand]
    public void OpenAppDrawer()
    {
        IsAppDrawerOpen = true;
        AppDrawerStatusMessage = "GameLoop Environment Ready";
    }

    [RelayCommand]
    public void CloseAppDrawer()
    {
        IsAppDrawerOpen = false;
    }

    [RelayCommand]
    public async Task LaunchPubgAsync()
    {
        AppDrawerStatusMessage = "Launching PUBG Mobile in GameLoop...";
        var success = await _launcherService.LaunchPubgAsync();
        AppDrawerStatusMessage = success ? "PUBG Mobile launched successfully" : "Failed to launch PUBG Mobile (GameLoop not found)";
    }

    [RelayCommand]
    public async Task LaunchSettingsAsync()
    {
        AppDrawerStatusMessage = "Opening internal Android Settings (F9)...";
        var success = await _launcherService.LaunchSettingsAsync();
        AppDrawerStatusMessage = success ? "Android Settings launched successfully" : "Failed to launch Android Settings";
    }

    [RelayCommand]
    public async Task LaunchFilesAsync()
    {
        AppDrawerStatusMessage = "Opening File Explorer / OBB storage...";
        var success = await _launcherService.LaunchAppAsync("nextapp.fx");
        AppDrawerStatusMessage = success ? "File Explorer launched" : "Failed to open File Explorer";
    }

    [RelayCommand]
    public async Task LaunchBrowserAsync()
    {
        AppDrawerStatusMessage = "Opening Android Web Browser...";
        var success = await _launcherService.LaunchAppAsync("com.android.browser");
        AppDrawerStatusMessage = success ? "Browser launched" : "Failed to open Browser";
    }

    [RelayCommand]
    public async Task LaunchAppMarketAsync()
    {
        AppDrawerStatusMessage = "Opening Tencent AppMarket...";
        var success = await _launcherService.LaunchAppMarketAsync();
        AppDrawerStatusMessage = success ? "AppMarket launched" : "Failed to open AppMarket";
    }

    [RelayCommand]
    public async Task LaunchPlayStoreAsync()
    {
        AppDrawerStatusMessage = "Opening Google Play Store...";
        var success = await _launcherService.LaunchAppAsync("com.android.vending");
        AppDrawerStatusMessage = success ? "Google Play Store launched" : "Failed to open Play Store";
    }

    [RelayCommand]
    public async Task InstallApkFileAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "APK Package (*.apk)|*.apk|All files (*.*)|*.*",
                Title = "Select APK File to Install into GameLoop"
            };

            if (dialog.ShowDialog() == true)
            {
                AppDrawerStatusMessage = $"Installing {System.IO.Path.GetFileName(dialog.FileName)}...";
                var success = await _launcherService.InstallLocalApkAsync(dialog.FileName);
                AppDrawerStatusMessage = success ? $"Installed {System.IO.Path.GetFileName(dialog.FileName)} successfully" : "APK installation failed. Ensure GameLoop is running.";
            }
        }
        catch (Exception ex)
        {
            AppDrawerStatusMessage = $"Error: {ex.Message}";
        }
    }
}
