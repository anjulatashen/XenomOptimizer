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
    private bool _isGameLoopUnlocked = true;

    private static readonly HashSet<string> LockedTabs = new(StringComparer.OrdinalIgnoreCase)
    {
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

    public MainViewModel(IAdminService adminService, WindowsOptimizationViewModel windowsVm)
    {
        _adminService = adminService;
        WindowsVm = windowsVm;
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

        if (page == "KeyMapping")
        {
            if (!_pageCache.TryGetValue("GameLoop", out var glCached) || glCached == null)
            {
                glCached = App.Services.GetService(typeof(GameLoopOptimizationViewModel)) as ObservableObject;
                _pageCache["GameLoop"] = glCached;
            }

            if (glCached is GameLoopOptimizationViewModel glVm)
            {
                glVm.SelectedSubTab = 2; // KeyMapping Suite tab
            }

            CurrentPageTitle = "KeyMapping";
            CurrentPage = glCached;
            OnCurrentPageTitleChanged(CurrentPageTitle);
            return;
        }

        if (!_pageCache.TryGetValue(page, out var cachedVm) || cachedVm == null)
        {
            cachedVm = page switch
            {
                "Dashboard" => App.Services.GetService(typeof(DashboardViewModel)) as ObservableObject,
                "Windows" => WindowsVm,
                "Optimizer" => App.Services.GetService(typeof(OptimizerFrameGenViewModel)) as ObservableObject,
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
}
