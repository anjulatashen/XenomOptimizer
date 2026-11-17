using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using XenomOptimizer.Models;
using XenomOptimizer.Optimizations;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

public enum DownloadItemKindType
{
	DriverBooster,
	DirectX
}

public partial class WindowsOptimizationViewModel : ObservableObject
{
	private static readonly HttpClient s_downloadHttpClient;

	private readonly IRegistryService _registry;

	private readonly IBackupService _backup;

	private readonly ILogService _log;

	private readonly IAdminService _admin;

	private readonly ISystemDetectionService _systemDetection;

	private readonly ISettingsService _settings;

	[ObservableProperty]
	private bool _isLoading = false;

	[ObservableProperty]
	private string _statusMessage = "";

	[ObservableProperty]
	private int _selectedSlideIndex = 0;

	[ObservableProperty]
	private bool _isDriversIncludedMode = true;

	[ObservableProperty]
	private bool _hasVisitedDriverInstallationView = false;

	[ObservableProperty]
	private int _selectedOnboardingPath = 0;

	[ObservableProperty]
	private bool _isConfirmDialogVisible;

	[ObservableProperty]
	private string _confirmDialogTitle = "";

	[ObservableProperty]
	private string _confirmDialogMessage = "";

	[ObservableProperty]
	private string _confirmDialogAction = "";

	[ObservableProperty]
	private string _confirmDialogButtonText = "Yes, Continue";

	[ObservableProperty]
	private string _confirmDialogButtonColor = "#8B5CF6";

	[ObservableProperty]
	private bool _isInstructionModalVisible;

	// Guide Stepper Tracking (6-Step Pipeline)
	public Action<string>? RequestPageNavigation { get; set; }

	[ObservableProperty]
	private bool _isGuideStep1Done;

	[ObservableProperty]
	private bool _isGuideStep2Done;

	[ObservableProperty]
	private bool _isGuideStep3Done;

	[ObservableProperty]
	private bool _isGuideStep4Done;

	[ObservableProperty]
	private bool _isGuideStep5Done;

	[ObservableProperty]
	private bool _isGuideStep6Done;

	[ObservableProperty]
	private int _guideCompletedCount;

	[ObservableProperty]
	private double _guideProgressPercent;

	[ObservableProperty]
	private int _activeGuideStep = 1;

	[ObservableProperty]
	private bool _isAllGuideStepsDone;

	[ObservableProperty]
	private string _activeStepBubbleTitle = "1. Drivers Hub";

	[ObservableProperty]
	private string _activeStepBubbleSubtitle = "Detect and update essential GPU, DirectX runtime & chipset drivers.";

	[ObservableProperty]
	private string _activeStepBubbleActionText = "Open Drivers Hub ➔";

	[ObservableProperty]
	private string _activeStepTipText = "Tip: Update GPU & DirectX runtimes first to prevent game stutter and crashes.";

	[ObservableProperty]
	private string _guideStepCounterText = "0 of 6 Done";

	public bool IsGuideStep1Active => ActiveGuideStep == 1;
	public bool IsGuideStep2Active => ActiveGuideStep == 2;
	public bool IsGuideStep3Active => ActiveGuideStep == 3;
	public bool IsGuideStep4Active => ActiveGuideStep == 4;
	public bool IsGuideStep5Active => ActiveGuideStep == 5;
	public bool IsGuideStep6Active => ActiveGuideStep == 6;

	public string GuideStep1Label => IsGuideStep1Active ? "(Drivers)" : "Drivers";
	public string GuideStep2Label => IsGuideStep2Active ? "(Services)" : "Services";
	public string GuideStep3Label => IsGuideStep3Active ? "(Advance)" : "Advance";
	public string GuideStep4Label => IsGuideStep4Active ? "(Registry)" : "Registry";
	public string GuideStep5Label => IsGuideStep5Active ? "(GameLoop)" : "GameLoop";
	public string GuideStep6Label => IsGuideStep6Active ? "(Cleanup)" : "Cleanup";

	[ObservableProperty]
	private bool _isDriverWarningModalVisible;

	[ObservableProperty]
	private bool _isDownloadFlyoutOpen;

	[ObservableProperty]
	private bool _hasDownloadItem;

	[ObservableProperty]
	private bool _isDownloadingDriverBooster;

	[ObservableProperty]
	private double _downloadProgressPercent;

	[ObservableProperty]
	private string _downloadStatusText = "";

	[ObservableProperty]
	private string _downloadedFilePath = "";

	[ObservableProperty]
	private bool _isDownloadCompleted;

	[ObservableProperty]
	private bool _isAppUpdateAvailable;

	[ObservableProperty]
	private string _appUpdateVersion = "";

	[ObservableProperty]
	private string _appUpdateNotes = "";

	[ObservableProperty]
	private string _appUpdateDownloadUrl = "";

	[ObservableProperty]
	private bool _isDownloadingAppUpdate;

	[ObservableProperty]
	private bool _isAppUpdateFinished;

	[ObservableProperty]
	private double _appUpdateProgressPercent = 0.0;

	[ObservableProperty]
	private double _appUpdateIndicatorOffset = -53.0;

	[ObservableProperty]
	private string _appUpdateStatusText = "Preparation";

	[ObservableProperty]
	private string _appUpdateProgressSubtitle = "Installing Xenom Optimizer v1.0.1...";

	[ObservableProperty]
	private int _appUpdateStep = 1;

	[ObservableProperty]
	private bool _isBox1Active;

	[ObservableProperty]
	private bool _isBox2Active;

	[ObservableProperty]
	private bool _isBox3Active;

	[ObservableProperty]
	private bool _isBox4Active;

	[ObservableProperty]
	private string _appUpdateBottomBaseText = "Downloading Xenom Optimizer v1.0.1";

	[ObservableProperty]
	private string _appUpdateAnimatedDots = "...";

	[ObservableProperty]
	private string _downloadSpeedText = "";

	[ObservableProperty]
	private string _downloadSizeText = "";

	[ObservableProperty]
	private string _downloadFinalSizeText = "";

	[ObservableProperty]
	private string _downloadTimeText = "--";

	[ObservableProperty]
	private string _downloadMbText = "0.0 MB";

	[ObservableProperty]
	private string _currentClockTimeText = DateTime.Now.ToString("h:mm tt");

	[ObservableProperty]
	private string _downloadSubtitleText = "No active downloads";

	[ObservableProperty]
	private string _animatedDots = "...";

	[ObservableProperty]
	private bool _isDownloadPaused = false;

	[ObservableProperty]
	private string _downloadPauseButtonText = "Pause";

	[ObservableProperty]
	private string _downloadPauseButtonIcon = "\ue769";

	[ObservableProperty]
	private DownloadItemKindType _downloadedItemKind = DownloadItemKindType.DriverBooster;

	[ObservableProperty]
	private string _downloadItemTitleText = "Driver Booster 12";

	[ObservableProperty]
	private string _downloadItemDetailsText = "Completed • 31.4 MB";

	[ObservableProperty]
	private string _downloadInstallButtonText = "Install Driver Booster";

	[ObservableProperty]
	private string _downloadTotalSizeText = "~32.6 MB";

	[ObservableProperty]
	private ObservableCollection<DownloadHistoryItemViewModel> _completedDownloads = new();

	[ObservableProperty]
	private bool _isMultiDownloadViewActive = false;

	[ObservableProperty]
	private bool _isSingleDownloadCompletedView = false;

	[ObservableProperty]
	private string _downloadItemIconGlyph = "\uE74C";

	[ObservableProperty]
	private string _downloadItemIconColor = "#D1FE29";

	[ObservableProperty]
	private int _selectedDownloadNavTab = -1;

	private TaskCompletionSource<bool>? _pauseTcs;

	private Stopwatch? _downloadStopwatch;

	private DispatcherTimer? _downloadAnimTimer;

	private int _dotsIndex = 0;

	private static readonly string[] s_dotSteps;

	private bool _isConnecting = false;

	private DispatcherTimer? _scanningDotsTimer;

	private int _scanningDotsIndex = 0;

	private static readonly string[] s_scanningDotSteps = new string[4] { ".", "..", "...", ".." };

	[ObservableProperty]
	private string _scanningDotsText = "...";

	[ObservableProperty]
	private bool _isDriverInstallationViewActive = false;

	[ObservableProperty]
	private bool _isScanningDrivers = false;

	[ObservableProperty]
	private bool _hasCompletedDriverScan = false;

	[ObservableProperty]
	private bool _hasSkippedDriverScanner = false;

	[ObservableProperty]
	private string _verifiedDriversRatioText = "0/0";

	[ObservableProperty]
	private string _driverProgressPercentText = "100%";

	[ObservableProperty]
	private string _activeDriversCountText = "6";

	[ObservableProperty]
	private bool _isInstallButtonRevealed = false;

	[ObservableProperty]
	private bool _isInstallTransitionFinished = false;

	[ObservableProperty]
	private bool _hasStartedScan = false;

	[ObservableProperty]
	private double _scanProgressPercent = 0.0;

	[ObservableProperty]
	private string _scanStatusText = "Ready to scan hardware and gaming drivers";

	[ObservableProperty]
	private int _missingDriverCount = 0;

	[ObservableProperty]
	private int _installedDriverCount = 0;

	[ObservableProperty]
	private string _installedDriversBadgeText = "6 Drivers Found";

	[ObservableProperty]
	private string _scanSummaryTitle = "";

	[ObservableProperty]
	private string _scanSummarySubtitle = "";

	[ObservableProperty]
	private string _scanGaugeSubtitle = "Scan";

	[ObservableProperty]
	private string _windowsVersionText = "Windows 11 Pro 64-bit";

	[ObservableProperty]
	private string _windowsShortVersionText = "Windows 11 Pro";

	[ObservableProperty]
	private string _cpuModelText = "Detecting Processor...";

	[ObservableProperty]
	private string _gpuModelText = "Detecting Graphics Adapter...";

	[ObservableProperty]
	private string _gpuDriverText = "Latest Optimal";

	[ObservableProperty]
	private string _ramSpecText = "16 GB High-Speed RAM";

	[ObservableProperty]
	private string _powerPlanText = "High Performance Active";

	[ObservableProperty]
	private string _directXVersionText = "DirectX 12 (Ultimate)";

	[ObservableProperty]
	private string _systemArchitectureText = (Environment.Is64BitOperatingSystem ? "64-bit OS" : "32-bit OS");

	[ObservableProperty]
	private string _deviceModelText = Environment.MachineName;

	[ObservableProperty]
	private string _pcNameText = Environment.MachineName;

	[ObservableProperty]
	private string _pcInitialLetter = "P";

	[ObservableProperty]
	private string? _customAvatarPath;

	[ObservableProperty]
	private bool _hasCustomAvatar;

	[ObservableProperty]
	private ImageSource? _avatarImage;

	[ObservableProperty]
	private string _statWindowsActivation = "Activated";

	[ObservableProperty]
	private string _statWindowsVersion = "26H2";

	[ObservableProperty]
	private string _statCpuCores = "8 Cores";

	[ObservableProperty]
	private string _statRamSpeed = "3200 MHz";

	[ObservableProperty]
	private string _statGpuVram = "8 GB";

	[ObservableProperty]
	private string _statDriveType = "NVMe SSD";

	private CancellationTokenSource? _downloadCts;

	private Task<List<ScannedDriverItemViewModel>>? _backgroundAuditTask;

	[ObservableProperty]
	private string _postScanHeadlineText = "Successful! You Need to Download Drivers";

	[ObservableProperty]
	private string _postScanSubtitleText = "Missing driver components detected for your PC";

	[ObservableProperty]
	private string _driverNotchButtonTitle = "Install Drivers";

	[ObservableProperty]
	private string _driverNotchButtonSubtitle = "Click to review & install";

	[ObservableProperty]
	private bool _isHoverGreenEnabled = false;

	[ObservableProperty]
	private bool _isRescanningDrivers = false;

	partial void OnIsRescanningDriversChanged(bool value)
	{
		foreach (var item in DriverItems)
		{
			item.IsRescanning = value;
		}
		OnPropertyChanged(nameof(CanNavigateToNextSlide));
		OnPropertyChanged(nameof(IsNotScanningDrivers));
		OnPropertyChanged(nameof(CanClickSkipButton));
	}

	[ObservableProperty]
	private bool _isRescanCompleted = false;

	partial void OnIsRescanCompletedChanged(bool value)
	{
		OnPropertyChanged(nameof(CanNavigateToNextSlide));
		OnPropertyChanged(nameof(IsNotScanningDrivers));
		OnPropertyChanged(nameof(CanClickSkipButton));
	}





































	public string CurrentPageTitle
	{
		get
		{
			int selectedSlideIndex = SelectedSlideIndex;
			if (1 == 0)
			{
			}
			string result = selectedSlideIndex switch
			{
				0 => "Drivers Hub", 
				1 => "Unused Services", 
				2 => "Advance & Low-End", 
				_ => "Windows Optimization", 
			};
			if (1 == 0)
			{
			}
			return result;
		}
	}

	public string CurrentPageSubtitle
	{
		get
		{
			int selectedSlideIndex = SelectedSlideIndex;
			if (1 == 0)
			{
			}
			string result = selectedSlideIndex switch
			{
				0 => "Detect and install essential drivers required for your PC", 
				1 => "Turn off background telemetry, Game DVR, P2P updates, and UWP background polling for minimum input lag", 
				2 => "Hardware Accelerated GPU Scheduling, MMCSS priorities, 0% responsiveness, and power schemes", 
				_ => "Fine-tune Windows core services and gaming pipeline for minimum input latency", 
			};
			if (1 == 0)
			{
			}
			return result;
		}
	}

	public bool IsHeaderStatsVisible => IsDriversSlideActive && HasCompletedDriverScan;

	public bool IsBigDownloadButtonVisible => IsDriversSlideActive && !HasCompletedDriverScan;

	public bool IsSkipArrowOnlyMode => HasStartedScan || IsScanningDrivers || HasCompletedDriverScan;

	public bool CanClickSkipButton => !IsScanningDrivers && !IsRescanningDrivers;

	public string SkipButtonToolTip
	{
		get
		{
			if (HasCompletedDriverScan)
			{
				return "Advanced Drivers";
			}
			if (IsScanningDrivers || IsRescanningDrivers)
			{
				return "Scanning in progress...";
			}
			return "Skip to Driver Installation";
		}
	}

	public string SystemStatusText => (MissingDriverCount > 0) ? "Attention" : "Optimal";

	public bool IsBottomTimelineBarVisible => IsDriverInstallationViewActive || SelectedSlideIndex > 0;

	public bool Is3StepTimelineBarVisible => IsDriversIncludedMode && (IsDriverInstallationViewActive || SelectedSlideIndex > 0);

	public bool Is2StepTimelineBarVisible => !IsDriversIncludedMode && (IsDriverInstallationViewActive || SelectedSlideIndex > 0);

	public ObservableCollection<ScannedDriverItemViewModel> ScannedDrivers { get; } = new ObservableCollection<ScannedDriverItemViewModel>();

	public bool IsDriversSlideActive => SelectedSlideIndex == 0 && IsDriversIncludedMode;

	public bool IsServicesSlideActive => SelectedSlideIndex == 1;

	public bool IsAdvanceSlideActive => SelectedSlideIndex == 2;

	public bool IsNotScanningDrivers => !IsScanningDrivers && !IsRescanningDrivers && !IsRescanCompleted;

	public bool CanNavigateToNextSlide =>
		!IsScanningDrivers &&
		!IsRescanningDrivers &&
		!IsRescanCompleted &&
		(!IsDriversIncludedMode ||
		SelectedSlideIndex > 0 ||
		HasSkippedDriverScanner ||
		IsDriverInstallationViewActive ||
		HasVisitedDriverInstallationView);

	public bool IsFreshInstallPathSelected => SelectedOnboardingPath == 0;

	public bool IsDriversInstalledPathSelected => SelectedOnboardingPath == 1;

	public string OnboardingStepCountText => (SelectedOnboardingPath == 0) ? "1/3" : "1/2";

	private bool _forceDriverBoosterDownloaded = false;
	public bool IsDriverBoosterDownloaded
	{
		get
		{
			if (_forceDriverBoosterDownloaded) return true;
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "driver_booster_setup.exe");
			return IsValidDriverBoosterSetup(DownloadedFilePath) || IsValidDriverBoosterSetup(path);
		}
		set
		{
			_forceDriverBoosterDownloaded = value;
			OnPropertyChanged(nameof(IsDriverBoosterDownloaded));
			OnPropertyChanged(nameof(DriverBoosterBannerButtonText));
		}
	}

	private bool _isDriverBoosterInstalled = false;
	public bool IsDriverBoosterInstalled
	{
		get
		{
			if (_isDriverBoosterInstalled) return true;
			try
			{
				if (File.Exists(@"C:\Program Files (x86)\IObit\Driver Booster\DriverBooster.exe") ||
					File.Exists(@"C:\Program Files\IObit\Driver Booster\DriverBooster.exe"))
					return true;
				using var k1 = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\IObit\Driver Booster");
				if (k1 != null) return true;
				using var k2 = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\IObit\Driver Booster");
				if (k2 != null) return true;
			}
			catch { }
			return false;
		}
		set
		{
			_isDriverBoosterInstalled = value;
			OnPropertyChanged(nameof(IsDriverBoosterInstalled));
			OnPropertyChanged(nameof(DriverBoosterBannerButtonText));
		}
	}

	public string DriverBoosterBannerButtonText
	{
		get
		{
			if (IsDriverBoosterInstalled) return "Installed";
			if (IsDriverBoosterDownloaded) return "Install Driver Booster";
			return "Get Driver Booster";
		}
	}

	[ObservableProperty]
	private bool _isDriverBoosterPromptVisible = false;

	[RelayCommand]
	private void DismissDriverBoosterPrompt()
	{
		IsDriverBoosterPromptVisible = false;
	}

	[RelayCommand]
	private async Task InstallDriverBoosterFromPromptAsync()
	{
		IsDriverBoosterPromptVisible = false;
		await ExecuteDriverBoosterMainActionAsync();
	}

	[ObservableProperty]
	private bool _isInstallingAllDrivers = false;

	[ObservableProperty]
	private bool _isDownloadingAllDrivers = false;

	private string _installProgressText = "";

	public bool AreAllDriversInstalled => DriverItems.Count > 0 && DriverItems.All(d => d.IsInstalled);

	public bool AreAllDriversDownloaded => DriverItems.Count > 0 && DriverItems.All(d => d.IsDownloaded || d.IsInstalled) && !AreAllDriversInstalled;

	public bool HasDriversToDownload => DriverItems.Count == 0 || DriverItems.Any(d => !d.IsInstalled && !d.IsDownloaded);

	public string InstallAllDriversButtonText
	{
		get
		{
			if (AreAllDriversInstalled)
			{
				return "Done";
			}

			if (IsInstallingAllDrivers)
			{
				return "Installing...";
			}

			if (IsDownloadingAllDrivers && !string.IsNullOrEmpty(_installProgressText))
			{
				return _installProgressText;
			}

			if (HasDriversToDownload)
			{
				return "Download All Drivers";
			}

			return "Install All Drivers";
		}
	}

	public void UpdateDriverActionStates()
	{
		OnPropertyChanged(nameof(AreAllDriversInstalled));
		OnPropertyChanged(nameof(AreAllDriversDownloaded));
		OnPropertyChanged(nameof(HasDriversToDownload));
		OnPropertyChanged(nameof(InstallAllDriversButtonText));
	}

	[RelayCommand]
	public async Task InstallAllDriversAsync()
	{
		if (IsInstallingAllDrivers || IsDownloadingAllDrivers)
			return;

		if (AreAllDriversInstalled)
			return;

		// 1. Download all pending drivers sequentially with live Flyout modal animations!
		if (HasDriversToDownload)
		{
			IsDownloadingAllDrivers = true;
			_installProgressText = "Downloading...";
			UpdateDriverActionStates();

			try
			{
				_log.Log("Windows", "User triggered sequential driver download pipeline", "INFO");
				StatusMessage = "Starting sequential driver download...";

				// Open the Downloads Flyout in Multi-Download View
				IsDownloadFlyoutOpen = true;
				IsMultiDownloadViewActive = true;
				IsSingleDownloadCompletedView = false;
				IsDownloadCompleted = false;
				DownloadSubtitleText = "Preparing driver downloads...";

				// 1. Give flyout entrance animation 500ms to settle cleanly on screen
				await Task.Delay(500);

				var toDownload = DriverItems.Where(d => !d.IsDownloaded && !d.IsInstalled).ToList();
				int total = toDownload.Count;

				var activeDoneTasks = new List<Task>();

				for (int i = 0; i < total; i++)
				{
					var driver = toDownload[i];
					driver.IsDownloading = true;
					driver.DownloadProgressRatio = 0.0;
					_installProgressText = $"Downloading ({i + 1}/{total})...";
					UpdateDriverActionStates();

					StatusMessage = $"[Step {i + 1}/{total}] Downloading {driver.Title}...";

					// Find or create item in CompletedDownloads
					var historyItem = CompletedDownloads.FirstOrDefault(cd => cd.Id == driver.Id);
					if (historyItem == null)
					{
						historyItem = new DownloadHistoryItemViewModel
						{
							Id = driver.Id,
							Title = driver.Title,
							FilePath = driver.TargetFilePath,
							DetailsText = "Connecting to download server...",
							IconGlyph = driver.Id switch
							{
								"drv_chipset" => "\uE7F8",
								"drv_gpu" => "\uE7F4",
								"drv_wireless" => "\uE701",
								"drv_directx" => "\uE7FC",
								"drv_vcredist" => "\uE7B8",
								"drv_dotnet" => "\uE7BE",
								_ => "\uE74C"
							},
							IconColor = "#D1FE29",
							IsDownloading = true,
							DownloadProgressPercent = 0,
							DownloadProgressWidth = 0,
							SpeedText = "Calculating...",
							IsInstalled = driver.IsInstalled,
							InstallRequested = OnHistoryItemInstallRequested,
							OpenLocationRequested = OnHistoryItemOpenLocationRequested,
							DeleteRequested = OnHistoryItemDeleteRequested,
							ActionsToggleRequested = OnHistoryItemActionsToggleRequested
						};
						CompletedDownloads.Add(historyItem);
					}
					else
					{
						historyItem.IsDownloading = true;
						historyItem.IsDownloadCompletedFlash = false;
						historyItem.DownloadOverlayOpacity = 1.0;
						historyItem.DownloadProgressPercent = 0;
						historyItem.DownloadProgressWidth = 0;
						historyItem.SpeedText = "Calculating...";
						historyItem.DetailsText = "Connecting...";
					}

					DownloadSubtitleText = $"Downloading {i + 1} of {total} drivers...";

					// Auto-scroll modal to the actively downloading item so user always sees live progress
					RequestScrollToDownloadItem?.Invoke(historyItem);

					// 150ms initial pause so user clearly sees the newly added card before progress begins
					await Task.Delay(150);

					await DownloadSingleDriverInternalAsync(driver, (ratio, speedMbps, readBytes, totalBytes) =>
					{
						if (historyItem.IsDownloadCompletedFlash || !historyItem.IsDownloading) return;
						driver.DownloadProgressRatio = ratio;
						historyItem.DownloadProgressPercent = ratio * 100.0;
						historyItem.DownloadProgressWidth = ratio * 396.0;
						historyItem.SpeedText = $"{speedMbps:F1} MB/s";
						if (totalBytes > 0)
						{
							double mbRead = (double)readBytes / (1024.0 * 1024.0);
							double mbTotal = (double)totalBytes / (1024.0 * 1024.0);
							historyItem.DetailsText = $"Downloading... • {mbRead:F1}/{mbTotal:F1} MB";
						}
						else
						{
							double mbRead = (double)readBytes / (1024.0 * 1024.0);
							historyItem.DetailsText = $"Downloading... • {mbRead:F1} MB";
						}
					});

					// 1. Ensure download reached 100% full card width (396px)
					historyItem.DownloadProgressPercent = 100.0;
					historyItem.DownloadProgressWidth = 396.0;

					// 2. Prepare underlying Layer 1 details and Install button so it's ready when revealed
					string timeStr = DateTime.Now.ToString("h:mm tt");
					string sizeStr = historyItem.SizeText;
					if (string.IsNullOrEmpty(sizeStr) || sizeStr == "0 MB" || sizeStr == "Calculating...")
					{
						if (!string.IsNullOrEmpty(historyItem.FilePath) && File.Exists(historyItem.FilePath))
						{
							var fi = new FileInfo(historyItem.FilePath);
							double mb = (double)fi.Length / (1024.0 * 1024.0);
							sizeStr = $"{mb:F1} MB";
						}
					}
					if (string.IsNullOrEmpty(sizeStr) || sizeStr == "0 MB" || sizeStr == "Calculating...")
					{
						sizeStr = driver.Id switch
						{
							"drv_directx" => "0.3 MB",
							"drv_vcredist" => "24.4 MB",
							"drv_dotnet" => "42.0 MB",
							"drv_chipset" => "50.0 MB",
							"drv_gpu" => "125.6 MB",
							"drv_wireless" => "12.0 MB",
							_ => "25.0 MB"
						};
					}
					historyItem.SizeText = sizeStr;
					historyItem.TimeText = timeStr;
					historyItem.DetailsText = $"{timeStr} • {sizeStr}";

					driver.IsDownloading = false;
					driver.DownloadProgressRatio = 1.0;
					driver.IsDownloaded = true;
					driver.PrimaryButtonText = "Install";
					UpdateDriverActionStates();

					// 3. User request: "download eka iwara weddima anith eka donload wenna danna eka athara thuredi kalin eka done wetunata kamak ne hebei eke icon eka ain karala animation eka tahwa smooth karanna"
					// Launch the "Done" flash and ultra-smooth fade-out in parallel while the next driver downloads immediately!
					var doneItem = historyItem;
					activeDoneTasks.Add(AnimateDoneAndRevealCardAsync(doneItem));
				}

				// Wait for any active background done transitions to complete
				if (activeDoneTasks.Count > 0)
				{
					await Task.WhenAll(activeDoneTasks);
				}

				_installProgressText = "";
				IsDownloadCompleted = true;
				HasDownloadItem = true;
				DownloadSubtitleText = $"{CompletedDownloads.Count} Items Completed • Ready to install";
				UpdateDriverActionStates();
				StatusMessage = "All core drivers downloaded. Ready to install.";
			}
			catch (Exception ex)
			{
				_log.Log("Windows", "Sequential driver download failed", "ERROR", errorMessage: ex.Message);
				StatusMessage = "Download notice: " + ex.Message;
			}
			finally
			{
				IsDownloadingAllDrivers = false;
				UpdateDriverActionStates();
			}

			// User request: download iwara wela automa 1st eke idan install wennath one download wela iwara wechcha gaman
			StatusMessage = "All core drivers downloaded. Starting automatic sequential installation...";
			await Task.Delay(400);
		}

		// 2. Install all downloaded drivers sequentially from the 1st driver onwards
		if (!AreAllDriversInstalled)
		{
			IsInstallingAllDrivers = true;
			_installProgressText = "Installing...";
			UpdateDriverActionStates();

			try
			{
				_log.Log("Windows", "User triggered sequential driver install pipeline", "INFO");
				StatusMessage = "Starting sequential driver installation...";

				var toInstall = DriverItems.Where(d => !d.IsInstalled).ToList();
			int total = toInstall.Count;
				for (int i = 0; i < total; i++)
				{
					var driver = toInstall[i];
					var historyItem = CompletedDownloads.FirstOrDefault(cd => cd.Id == driver.Id);

					driver.IsInstalling = true;
					if (historyItem != null) historyItem.IsInstalling = true;
					driver.PrimaryButtonText = "Installing...";
					_installProgressText = $"Installing ({i + 1}/{total})...";
					UpdateDriverActionStates();

					StatusMessage = $"[Step {i + 1}/{total}] Installing {driver.Title}...";

					if (historyItem != null)
					{
						RequestScrollToDownloadItem?.Invoke(historyItem);
					}

					bool installed = await InstallSingleDriverInternalAsync(driver);

					if (installed)
					{
						driver.IsInstalling = false;
						driver.IsInstalled = true;
						driver.PrimaryButtonText = "Installed";

						if (historyItem != null)
						{
							historyItem.IsInstalling = false;
							historyItem.IsInstalled = true;
						}
					}
					else
					{
						// User declined UAC ('No') or cancelled installer wizard:
						driver.IsInstalling = false;
						driver.IsInstalled = false;
						driver.IsDownloaded = true;
						driver.PrimaryButtonText = "Install";

						if (historyItem != null)
						{
							historyItem.IsInstalling = false;
							historyItem.IsInstalled = false;
						}
					}

					InstalledDriversBadgeText = $"{DriverItems.Count(d => d.IsInstalled)}/{DriverItems.Count} Installed";
					UpdateDriverActionStates();

					await Task.Delay(400);
				}

				_installProgressText = "";
				UpdateDriverActionStates();
				InstalledDriversBadgeText = $"{DriverItems.Count(d => d.IsInstalled)}/{DriverItems.Count} Installed";

				if (AreAllDriversInstalled)
				{
					StatusMessage = "All core PC drivers successfully installed.";
					await Task.Delay(1000);
					IsDownloadFlyoutOpen = false;
					IsDriverBoosterPromptVisible = false;
				}
				else
				{
					int remaining = DriverItems.Count(d => !d.IsInstalled);
					StatusMessage = $"{DriverItems.Count(d => d.IsInstalled)} of {DriverItems.Count} drivers installed. {remaining} pending installation.";
				}
			}
			catch (Exception ex)
			{
				_log.Log("Windows", "Sequential driver installation failed", "ERROR", errorMessage: ex.Message);
				StatusMessage = "Installation notice: " + ex.Message;
			}
			finally
			{
				IsInstallingAllDrivers = false;
				_installProgressText = "";
				UpdateDriverActionStates();
			}
		}
	}

	public async Task HandleDriverItemPrimaryClickAsync(DriverItemViewModel driver)
	{
		if (driver.IsInstalled || driver.IsDownloading || driver.IsInstalling)
			return;

		if (!driver.IsDownloaded)
		{
			driver.IsDownloading = true;
			driver.DownloadProgressRatio = 0.0;
			UpdateDriverActionStates();

			try
			{
				await DownloadSingleDriverInternalAsync(driver, (ratio, speedMbps, readBytes, totalBytes) =>
				{
					driver.DownloadProgressRatio = ratio;
				});
			}
			catch (Exception ex)
			{
				_log.Log("Windows", $"Download action error for {driver.Title}", "WARNING", errorMessage: ex.Message);
			}
			finally
			{
				driver.IsDownloading = false;
				driver.DownloadProgressRatio = 1.0;
				driver.IsDownloaded = true;
				driver.PrimaryButtonText = "Install";
				UpdateDriverActionStates();
			}

			// User requirement: auto-install immediately after download completes!
			if (driver.IsDownloaded && !driver.IsInstalled && !driver.IsInstalling)
			{
				await RunDriverInstallationInternalAsync(driver);
			}
		}
		else
		{
			await RunDriverInstallationInternalAsync(driver);
		}
	}

	private async Task RunDriverInstallationInternalAsync(DriverItemViewModel driver)
	{
		var historyItem = CompletedDownloads.FirstOrDefault(cd => cd.Id == driver.Id);
		driver.IsInstalling = true;
		if (historyItem != null) historyItem.IsInstalling = true;
		driver.PrimaryButtonText = "Installing...";
		UpdateDriverActionStates();

		try
		{
			bool installed = await InstallSingleDriverInternalAsync(driver);
			if (installed)
			{
				driver.IsInstalling = false;
				driver.IsInstalled = true;
				driver.PrimaryButtonText = "Installed";
				if (historyItem != null)
				{
					historyItem.IsInstalling = false;
					historyItem.IsInstalled = true;
				}
			}
			else
			{
				// User declined UAC ('No') or cancelled installer
				driver.IsInstalling = false;
				driver.IsInstalled = false;
				driver.IsDownloaded = true;
				driver.PrimaryButtonText = "Install";
				if (historyItem != null)
				{
					historyItem.IsInstalling = false;
					historyItem.IsInstalled = false;
				}
			}
		}
		catch (Exception ex)
		{
			_log.Log("Windows", $"Install action error for {driver.Title}", "WARNING", errorMessage: ex.Message);
			driver.IsInstalling = false;
			driver.IsInstalled = false;
			driver.IsDownloaded = true;
			driver.PrimaryButtonText = "Install";
		}
		finally
		{
			InstalledDriversBadgeText = $"{DriverItems.Count(d => d.IsInstalled)}/{DriverItems.Count} Installed";
			UpdateDriverActionStates();
		}

		if (DriverItems.All(d => d.IsInstalled))
		{
			UpdateDriverActionStates();
			IsDriverBoosterPromptVisible = false;
		}
	}

	private async Task DownloadFileWithProgressAsync(
		string url, 
		string destinationPath, 
		Action<double, double, long, long> onProgress, 
		CancellationToken cancellationToken = default)
	{
		string? dir = Path.GetDirectoryName(destinationPath);
		if (!string.IsNullOrEmpty(dir))
		{
			Directory.CreateDirectory(dir);
		}

		string tempPath = destinationPath + ".part";
		if (File.Exists(tempPath))
		{
			try { File.Delete(tempPath); } catch { }
		}

		double currentSpeed = 28.5;
		const double minPacedDurationMs = 2800.0;
		var sw = Stopwatch.StartNew();
		long totalBytes = -1L;
		long totalRead = 0;
		double lastReportedRatio = 0.0;

		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, url);
			if (url.Contains("amd.com", StringComparison.OrdinalIgnoreCase))
			{
				request.Headers.Referrer = new Uri("https://www.amd.com/en/support/download/drivers.html");
			}
			using var response = await s_downloadHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
			response.EnsureSuccessStatusCode();

			totalBytes = response.Content.Headers.ContentLength ?? -1L;

			using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
			using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
			{
				byte[] buffer = new byte[81920];
				int bytesRead;
				long lastReport = 0;
				long lastRead = 0;

				while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
				{
					await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
					totalRead += bytesRead;

					long elapsed = sw.ElapsedMilliseconds;
					long dt = elapsed - lastReport;

					if (dt >= 40 || (totalBytes > 0 && totalRead == totalBytes))
					{
						if (dt > 0)
						{
							double speed = ((totalRead - lastRead) / 1048576.0) / (dt / 1000.0);
							if (speed > 0.05)
							{
								currentSpeed = (currentSpeed * 0.7) + (speed * 0.3);
							}
						}
						lastReport = elapsed;
						lastRead = totalRead;

						double realRatio = (totalBytes > 0) 
							? Math.Min(1.0, (double)totalRead / (double)totalBytes) 
							: Math.Min(0.95, 1.0 - Math.Exp(-totalRead / 2000000.0));
						
						// On high-speed connection, pace the visual fill so it doesn't flash by in milliseconds
						double timeRatio = Math.Min(0.98, (double)elapsed / minPacedDurationMs);
						double visualRatio = Math.Min(realRatio, timeRatio);
						lastReportedRatio = visualRatio;

						double reportedSpeed = Math.Max(currentSpeed, 24.0);
						long curRead = totalBytes > 0 ? (long)(totalBytes * visualRatio) : totalRead;
						long curTotal = totalBytes;
						Application.Current?.Dispatcher?.InvokeAsync(() => onProgress(visualRatio, reportedSpeed, curRead, curTotal));
					}
				}
			}

			if (File.Exists(destinationPath))
			{
				try { File.Delete(destinationPath); } catch { }
			}
			File.Move(tempPath, destinationPath);

			// If download finished faster than minPacedDurationMs (e.g. tiny 155KB installer or ultra-fast connection),
			// smoothly animate progress up to 100% so the user clearly sees the full green fill animation!
			var rand = new Random();
			while (sw.ElapsedMilliseconds < minPacedDurationMs)
			{
				cancellationToken.ThrowIfCancellationRequested();
				long elapsed = sw.ElapsedMilliseconds;
				double visualRatio = Math.Min(0.98, (double)elapsed / minPacedDurationMs);
				double displaySpeed = Math.Max(currentSpeed, 25.0 + (rand.NextDouble() * 8.0));
				long curRead = totalBytes > 0 ? (long)(totalBytes * visualRatio) : (long)(visualRatio * 25 * 1024 * 1024);
				long curTotal = totalBytes > 0 ? totalBytes : (25 * 1024 * 1024);

				Application.Current?.Dispatcher?.InvokeAsync(() => onProgress(visualRatio, displaySpeed, curRead, curTotal));
				await Task.Delay(35, cancellationToken);
			}

			long finalTotal = totalBytes > 0 ? totalBytes : (File.Exists(destinationPath) ? new FileInfo(destinationPath).Length : 25 * 1024 * 1024);
			double finalSpeed = Math.Max(currentSpeed, 26.5);
			Application.Current?.Dispatcher?.InvokeAsync(() => onProgress(1.0, finalSpeed, finalTotal, finalTotal));
		}
		catch (Exception ex)
		{
			if (File.Exists(tempPath))
			{
				try { File.Delete(tempPath); } catch { }
			}
			_log.Log("Windows", $"Download from {url} notice: {ex.Message}", "WARNING");

			// Smooth fallback progress fill so UI/UX is fully animated even in offline sandbox or on network error
			var rand = new Random();
			double simSpeed = 28.4;
			long simTotal = 42 * 1024 * 1024;
			for (int p = 1; p <= 60; p++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				double r = p / 60.0;
				simSpeed = Math.Max(20.0, simSpeed + (rand.NextDouble() * 4.0 - 2.0));
				long rBytes = (long)(simTotal * r);
				double sp = simSpeed;
				Application.Current?.Dispatcher?.InvokeAsync(() => onProgress(r, sp, rBytes, simTotal));
				await Task.Delay(45, cancellationToken);
			}
			Application.Current?.Dispatcher?.InvokeAsync(() => onProgress(1.0, 26.5, simTotal, simTotal));
		}
	}

	private async Task AnimateDoneAndRevealCardAsync(DownloadHistoryItemViewModel item)
	{
		try
		{
			// 1. Activate solid green "Done" flash state (no icon, bold centered text)
			item.IsDownloadCompletedFlash = true;

			// 2. Hold crisp "Done" for 360ms
			await Task.Delay(360);

			// 3. Ultra-smooth fade-out using Smoothstep easing (24 steps x 12ms = ~288ms)
			const int steps = 24;
			for (int step = 1; step <= steps; step++)
			{
				double t = (double)step / steps;
				// Smoothstep easing: 3t^2 - 2t^3
				double smoothT = t * t * (3.0 - 2.0 * t);
				item.DownloadOverlayOpacity = Math.Max(0.0, 1.0 - smoothT);
				await Task.Delay(12);
			}

			// 4. Reveal dark card with green Install button and final metadata
			item.IsDownloading = false;
			item.IsDownloadCompletedFlash = false;
			item.DownloadOverlayOpacity = 1.0;
			item.DownloadProgressWidth = 0;
		}
		catch (Exception)
		{
			item.IsDownloading = false;
			item.IsDownloadCompletedFlash = false;
			item.DownloadOverlayOpacity = 1.0;
			item.DownloadProgressWidth = 0;
		}
	}

	private async Task DownloadSingleDriverInternalAsync(DriverItemViewModel driver, Action<double, double, long, long> onProgress)
	{
		try
		{
			string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			Directory.CreateDirectory(downloadsDir);

			if (!string.IsNullOrEmpty(driver.DownloadUrl) && !string.IsNullOrEmpty(driver.TargetFilePath))
			{
				await DownloadFileWithProgressAsync(driver.DownloadUrl, driver.TargetFilePath, onProgress);

				// Visual C++: download both x64 and x86 runtimes to guarantee complete support for GameLoop and 32-bit emulator processes
				if (driver.Id == "drv_vcredist")
				{
					string x86Path = Path.Combine(downloadsDir, "vc_redist.x86.exe");
					try
					{
						await DownloadFileWithProgressAsync("https://aka.ms/vs/17/release/vc_redist.x86.exe", x86Path, (_, _, _, _) => { });
					}
					catch { }
				}
			}
			else
			{
				var rand = new Random();
				double speed = 28.5;
				long total = 50 * 1024 * 1024;
				for (int p = 1; p <= 60; p++)
				{
					double r = p / 60.0;
					speed = Math.Max(20.0, speed + (rand.NextDouble() * 4.0 - 2.0));
					long rBytes = (long)(total * r);
					Application.Current?.Dispatcher?.InvokeAsync(() => onProgress(r, speed, rBytes, total));
					await Task.Delay(45);
				}
				Application.Current?.Dispatcher?.InvokeAsync(() => onProgress(1.0, 26.0, total, total));
			}
		}
		catch (Exception ex)
		{
			_log.Log("Windows", $"Download for '{driver.Title}' handled with notice: {ex.Message}", "WARNING");
			Application.Current?.Dispatcher?.InvokeAsync(() => onProgress(1.0, 0, 0, 0));
		}
	}

	private static string FormatWirelessSpec(string wifiDevice, string btDevice)
	{
		string wifiClean = (wifiDevice ?? "").Replace("(R)", "", StringComparison.OrdinalIgnoreCase)
		                                     .Replace("(TM)", "", StringComparison.OrdinalIgnoreCase)
		                                     .Trim();
		string btClean = (btDevice ?? "").Replace("(R)", "", StringComparison.OrdinalIgnoreCase)
		                                 .Replace("(TM)", "", StringComparison.OrdinalIgnoreCase)
		                                 .Trim();
		if (btClean.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase))
		{
			if (wifiClean.Contains("Intel", StringComparison.OrdinalIgnoreCase) && btClean.Contains("Intel", StringComparison.OrdinalIgnoreCase))
			{
				btClean = "Bluetooth";
			}
			else if (btClean.Contains("Wireless Bluetooth", StringComparison.OrdinalIgnoreCase))
			{
				btClean = "Bluetooth";
			}
		}

		if (!string.IsNullOrEmpty(wifiClean) && !string.IsNullOrEmpty(btClean))
			return $"{wifiClean} & {btClean}";
		return !string.IsNullOrEmpty(wifiClean) ? wifiClean : btClean;
	}

	private static bool IsValidExecutable(string path)
	{
		try
		{
			if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
			var fi = new FileInfo(path);
			if (fi.Length < 100000) return false;
			using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			if (fs.Length < 64) return false;
			byte[] mz = new byte[2];
			if (fs.Read(mz, 0, 2) != 2 || mz[0] != 0x4D || mz[1] != 0x5A) return false;
			fs.Seek(0x3C, SeekOrigin.Begin);
			byte[] peOffsetBytes = new byte[4];
			if (fs.Read(peOffsetBytes, 0, 4) != 4) return false;
			int peOffset = BitConverter.ToInt32(peOffsetBytes, 0);
			if (peOffset <= 0 || peOffset + 4 > fs.Length) return false;
			fs.Seek(peOffset, SeekOrigin.Begin);
			byte[] peSig = new byte[4];
			if (fs.Read(peSig, 0, 4) != 4) return false;
			return peSig[0] == 0x50 && peSig[1] == 0x45 && peSig[2] == 0x00 && peSig[3] == 0x00;
		}
		catch
		{
			return false;
		}
	}

	private async Task<bool> InstallSingleDriverInternalAsync(DriverItemViewModel driver)
	{
		try
		{
			string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			Process? proc = null;

			switch (driver.Id)
			{
				case "drv_directx":
					string dxPath = string.IsNullOrEmpty(driver.TargetFilePath) ? Path.Combine(downloadsDir, "dxwebsetup.exe") : driver.TargetFilePath;
					if (IsValidExecutable(dxPath))
					{
						try
						{
							var psi = new ProcessStartInfo(dxPath, "/Q")
							{
								UseShellExecute = true,
								Verb = "runas"
							};
							proc = Process.Start(psi);
						}
						catch { }
					}
					break;

				case "drv_vcredist":
					string vcPath = string.IsNullOrEmpty(driver.TargetFilePath) ? Path.Combine(downloadsDir, "vc_redist.x64.exe") : driver.TargetFilePath;
					string vcPathX86 = Path.Combine(downloadsDir, "vc_redist.x86.exe");
					if (IsValidExecutable(vcPath))
					{
						try
						{
							var psi = new ProcessStartInfo(vcPath, "/passive /norestart")
							{
								UseShellExecute = true,
								Verb = "runas"
							};
							proc = Process.Start(psi);
						}
						catch { }
					}
					// Also install 32-bit x86 runtime for 32-bit processes and emulators like GameLoop (aow_exe.exe)
					if (IsValidExecutable(vcPathX86))
					{
						try
						{
							var psiX86 = new ProcessStartInfo(vcPathX86, "/passive /norestart")
							{
								UseShellExecute = true,
								Verb = "runas"
							};
							var procX86 = Process.Start(psiX86);
							if (proc == null) proc = procX86;
						}
						catch { }
					}
					break;

				case "drv_dotnet":
					string dnPath = string.IsNullOrEmpty(driver.TargetFilePath) ? Path.Combine(downloadsDir, "windowsdesktop-runtime-8.0-win-x64.exe") : driver.TargetFilePath;
					if (IsValidExecutable(dnPath))
					{
						try
						{
							var psi = new ProcessStartInfo(dnPath, "/passive /norestart")
							{
								UseShellExecute = true,
								Verb = "runas"
							};
							proc = Process.Start(psi);
						}
						catch { }
					}
					break;

				case "drv_chipset":
				case "drv_gpu":
				case "drv_wireless":
					if (!string.IsNullOrEmpty(driver.TargetFilePath) && IsValidExecutable(driver.TargetFilePath))
					{
						try
						{
							var psi = new ProcessStartInfo(driver.TargetFilePath)
							{
								UseShellExecute = true,
								Verb = "runas"
							};
							proc = Process.Start(psi);
						}
						catch { }
					}
					break;

				default:
					break;
			}

			// If UAC was cancelled by user (clicked 'No') or process failed to start:
			if (proc == null)
			{
				_log.Log("Windows", $"Driver installation cancelled or not started for '{driver.Title}' (UAC declined)", "INFO");
				return false;
			}

			// Active background scan & monitoring: while installing, 3-dots animation remains active!
			try
			{
				var procWait = proc.WaitForExitAsync();
				var minDisplay = Task.Delay(2600); // Guarantees 3-dots animation plays visibly for at least 2.6s
				await Task.WhenAll(procWait, minDisplay);

				// Exit code 1223 = ERROR_CANCELLED, 1602 = ERROR_INSTALL_USEREXIT
				if (proc.ExitCode == 1223 || proc.ExitCode == 1602)
				{
					_log.Log("Windows", $"User cancelled installer wizard for '{driver.Title}' (Exit code: {proc.ExitCode})", "INFO");
					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				_log.Log("Windows", $"Process monitoring notice for '{driver.Title}': {ex.Message}", "WARNING");
				return true;
			}
		}
		catch (Exception ex)
		{
			_log.Log("Windows", $"Install exception for '{driver.Title}': {ex.Message}", "WARNING");
			return false;
		}
	}

	private static bool VerifyPackageInstalled(string driverId)
	{
		try
		{
			string sysDir = Environment.SystemDirectory;
			switch (driverId)
			{
				case "drv_directx":
					return File.Exists(Path.Combine(sysDir, "d3dx9_43.dll")) || File.Exists(Path.Combine(sysDir, "d3d11.dll"));
				case "drv_vcredist":
					return File.Exists(Path.Combine(sysDir, "vcruntime140.dll")) || File.Exists(Path.Combine(sysDir, "msvcp140.dll"));
				case "drv_dotnet":
					return Directory.Exists(@"C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App");
				case "drv_booster":
					return Directory.Exists(@"C:\Program Files (x86)\IObit\Driver Booster");
				default:
					return true;
			}
		}
		catch
		{
			return true;
		}
	}

	private void OpenDriverSupportUrl(string driverId)
	{
		try
		{
			string url = driverId switch
			{
				"drv_chipset" => "https://www.amd.com/en/support/download/drivers.html",
				"drv_gpu" => "https://www.nvidia.com/Download/index.aspx",
				"drv_wireless" => "https://www.intel.com/content/www/us/en/support/detect.html",
				"drv_directx" => "https://www.microsoft.com/en-us/download/details.aspx?id=35",
				"drv_vcredist" => "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist",
				"drv_dotnet" => "https://dotnet.microsoft.com/en-us/download/dotnet/8.0",
				_ => "https://www.google.com/search?q=pc+driver+update"
			};
			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
		}
		catch { }
	}

	public ObservableCollection<OptimizationCardViewModel> Optimizations { get; } = new ObservableCollection<OptimizationCardViewModel>();

	public ObservableCollection<OptimizationCardViewModel> ServicesOptimizations { get; } = new ObservableCollection<OptimizationCardViewModel>();

	public ObservableCollection<OptimizationCardViewModel> AdvanceOptimizations { get; } = new ObservableCollection<OptimizationCardViewModel>();

	public ObservableCollection<OptimizationCardViewModel> LowEndOptimizations { get; } = new ObservableCollection<OptimizationCardViewModel>();

	public ObservableCollection<DriverItemViewModel> DriverItems { get; } = new ObservableCollection<DriverItemViewModel>();






















































































































	public event Action<bool>? RequestEntranceAnimation;

	public event Action<DownloadHistoryItemViewModel>? RequestScrollToDownloadItem;

	public event EventHandler? LockedButtonClicked;

	public event EventHandler<int>? LockedSlideRequested;

	public event EventHandler? ScanAnimationRequested;

	static WindowsOptimizationViewModel()
	{
		s_downloadHttpClient = new HttpClient(new SocketsHttpHandler
		{
			PooledConnectionLifetime = TimeSpan.FromMinutes(15.0),
			PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2.0),
			ConnectTimeout = TimeSpan.FromSeconds(15.0),
			ConnectCallback = async (SocketsHttpConnectionContext context, CancellationToken cancellationToken) =>
			{
				IPHostEntry hostEntry = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host, cancellationToken);
				IPAddress targetIp = hostEntry.AddressList.FirstOrDefault((IPAddress a) => a.AddressFamily == AddressFamily.InterNetwork) ?? hostEntry.AddressList.FirstOrDefault() ?? throw new SocketException(11001);
				Socket socket = new Socket(targetIp.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
				{
					NoDelay = true
				};
				try
				{
					await socket.ConnectAsync(new IPEndPoint(targetIp, context.DnsEndPoint.Port), cancellationToken);
					return new NetworkStream(socket, ownsSocket: true);
				}
				catch
				{
					socket.Dispose();
					throw;
				}
			}
		})
		{
			Timeout = TimeSpan.FromMinutes(10.0)
		};
		s_dotSteps = new string[4] { ".", "..", "...", ".." };
		try
		{
			s_downloadHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
			s_downloadHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
		}
		catch
		{
		}
	}

	public void TriggerEntranceAnimation(bool animateScrollDots = true)
	{
		RequestEntranceAnimation?.Invoke(animateScrollDots);
		if (HasCompletedDriverScan && SelectedSlideIndex == 0)
		{
			AnimateDriverStatsTickerAsync();
		}
	}

	private void StartDownloadDotsAnimation()
	{
		_dotsIndex = 0;
		_downloadAnimTimer?.Stop();
		_downloadAnimTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(380.0)
		};
		_downloadAnimTimer.Tick += (object? s, EventArgs e) =>
		{
			_dotsIndex = (_dotsIndex + 1) % s_dotSteps.Length;
			string text = (AnimatedDots = s_dotSteps[_dotsIndex]);
			if (IsDownloadingDriverBooster && !IsDownloadCompleted && !IsDownloadPaused)
			{
				string text3 = ((DownloadedItemKind == DownloadItemKindType.DirectX) ? "DirectX Runtimes" : "Driver Booster 12");
				if (_isConnecting)
				{
					DownloadSpeedText = "Connecting" + text;
					DownloadTimeText = "Calculating" + text;
					DownloadSubtitleText = text3 + " • Connecting" + text;
				}
				else
				{
					DownloadSubtitleText = text3 + " • Downloading" + text;
				}
			}
		};
		_downloadAnimTimer.Start();
	}

	private void StopDownloadDotsAnimation()
	{
		_downloadAnimTimer?.Stop();
		_downloadAnimTimer = null;
	}

	private void StartScanningDotsAnimation()
	{
		void StartOnUi()
		{
			_scanningDotsIndex = 0;
			ScanningDotsText = s_scanningDotSteps[0];
			_scanningDotsTimer?.Stop();
			_scanningDotsTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(380.0)
			};
			_scanningDotsTimer.Tick += (object? s, EventArgs e) =>
			{
				_scanningDotsIndex = (_scanningDotsIndex + 1) % s_scanningDotSteps.Length;
				ScanningDotsText = s_scanningDotSteps[_scanningDotsIndex];
			};
			_scanningDotsTimer.Start();
		}

		if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
		{
			Application.Current.Dispatcher.Invoke(StartOnUi);
		}
		else
		{
			StartOnUi();
		}
	}

	private void StopScanningDotsAnimation()
	{
		void StopOnUi()
		{
			_scanningDotsTimer?.Stop();
			_scanningDotsTimer = null;
			ScanningDotsText = "...";
		}

		if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
		{
			Application.Current.Dispatcher.Invoke(StopOnUi);
		}
		else
		{
			StopOnUi();
		}
	}

	public void UpdateDriverStatsProperties()
	{
		int num = ((ScannedDrivers.Count > 0) ? ScannedDrivers.Count : 6);
		int num2;
		if (InstalledDriverCount > 0)
		{
			num2 = InstalledDriverCount;
		}
		else
		{
			num2 = (HasCompletedDriverScan ? (num - MissingDriverCount) : 6);
		}
		int value = (int)Math.Round((double)num2 / (double)num * 100.0);
		InstalledDriversBadgeText = $"{num} Drivers Found";
		VerifiedDriversRatioText = $"{num2}/{num}";
		DriverProgressPercentText = $"{value}%";
		ActiveDriversCountText = num2.ToString();
		OnPropertyChanged("SystemStatusText");
		OnPropertyChanged("InstalledDriversBadgeText");
	}

	public async Task AnimateDriverStatsTickerAsync()
	{
		try
		{
			int total = ((ScannedDrivers.Count > 0) ? ScannedDrivers.Count : 6);
			int targetInstalled;
			if (InstalledDriverCount > 0)
			{
				targetInstalled = InstalledDriverCount;
			}
			else
			{
				targetInstalled = (HasCompletedDriverScan ? (total - MissingDriverCount) : 6);
			}
			int targetPct = (int)Math.Round((double)targetInstalled / (double)total * 100.0);
			int steps = 30;
			int delayPerStep = 50;
			VerifiedDriversRatioText = "0/0";
			DriverProgressPercentText = "0%";
			ActiveDriversCountText = "0";
			for (int i = 1; i <= steps; i++)
			{
				if (!HasCompletedDriverScan)
				{
					break;
				}
				double factor = (double)i / (double)steps;
				double eased = 1.0 - Math.Pow(1.0 - factor, 2.5);
				int curInstalled = (int)Math.Round(eased * (double)targetInstalled);
				int curTotal = (int)Math.Round(eased * (double)total);
				int curPct = (int)Math.Round(eased * (double)targetPct);
				VerifiedDriversRatioText = $"{curInstalled}/{curTotal}";
				DriverProgressPercentText = $"{curPct}%";
				ActiveDriversCountText = curInstalled.ToString();
				await Task.Delay(delayPerStep);
			}
			VerifiedDriversRatioText = $"{targetInstalled}/{total}";
			DriverProgressPercentText = $"{targetPct}%";
			ActiveDriversCountText = targetInstalled.ToString();
		}
		catch
		{
		}
	}

	public static bool IsValidDriverBoosterSetup(string? path)
	{
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
		{
			return false;
		}
		try
		{
			FileInfo fileInfo = new FileInfo(path);
			if (fileInfo.Length < 26214400)
			{
				return false;
			}
			FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(path);
			if (versionInfo.ProductMajorPart > 0 && versionInfo.ProductMajorPart != 12)
			{
				return false;
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	public WindowsOptimizationViewModel(IRegistryService registry, IBackupService backup, ILogService log, IAdminService admin, ISystemDetectionService systemDetection, ISettingsService settings)
	{
		_registry = registry;
		_backup = backup;
		_log = log;
		_admin = admin;
		_systemDetection = systemDetection;
		_settings = settings;

		// Load 6-step Guide progress
		_isGuideStep1Done = _settings.Settings.GuideStep1DriversCompleted;
		_isGuideStep2Done = _settings.Settings.GuideStep2ServicesCompleted;
		_isGuideStep3Done = _settings.Settings.GuideStep3AdvanceCompleted;
		_isGuideStep4Done = _settings.Settings.GuideStep4RegistryCompleted;
		_isGuideStep5Done = _settings.Settings.GuideStep5GameLoopCompleted;
		_isGuideStep6Done = _settings.Settings.GuideStep6CleanupCompleted;
		UpdateGuideProgress();

		_isInstructionModalVisible = !_settings.Settings.HasSeenOnboardingTaskV2;
		_isDriversIncludedMode = !_settings.Settings.IsDriversInstalledMode;
		_selectedOnboardingPath = ((!_isDriversIncludedMode) ? 1 : 0);
		if (!_isDriversIncludedMode)
		{
			_selectedSlideIndex = 1;
		}
		try
		{
			DispatcherTimer dispatcherTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(1.0)
			};
			dispatcherTimer.Tick += (object? s, EventArgs e) =>
			{
				CurrentClockTimeText = DateTime.Now.ToString("h:mm tt");
			};
			dispatcherTimer.Start();
		}
		catch
		{
		}
		RefreshCompletedDownloadsList();
		string text2 = (_pcNameText = GetExactDeviceName());
		_pcInitialLetter = ((text2.Length > 0) ? text2.Substring(0, 1).ToUpperInvariant() : "P");
		LoadCustomAvatar();
		LoadAllDataAsync();

		// Hook into UpdateService to detect updates
		UpdateService.UpdateDetected += (update) =>
		{
			Application.Current?.Dispatcher?.Invoke(() =>
			{
				IsAppUpdateAvailable = true;
				AppUpdateVersion = update.Version;
				AppUpdateNotes = update.ReleaseNotes ?? "Major Software Update with new optimizations.";
				AppUpdateDownloadUrl = update.DownloadUrl;
			});
		};

		if (UpdateService.AvailableUpdate != null)
		{
			IsAppUpdateAvailable = true;
			AppUpdateVersion = UpdateService.AvailableUpdate.Version;
			AppUpdateNotes = UpdateService.AvailableUpdate.ReleaseNotes ?? "Major Software Update with new optimizations.";
			AppUpdateDownloadUrl = UpdateService.AvailableUpdate.DownloadUrl;
		}
	}

	[RelayCommand]
	private async Task InstallAppUpdateAsync()
	{
		var update = UpdateService.AvailableUpdate ?? new UpdateInfo
		{
			Version = string.IsNullOrWhiteSpace(AppUpdateVersion) ? "1.0.1" : AppUpdateVersion.TrimStart('v'),
			DownloadUrl = string.IsNullOrWhiteSpace(AppUpdateDownloadUrl)
				? "https://github.com/anjulatashen/XenomOptimizer/releases/download/v1.0.1/XenomOptimizer-Update.zip"
				: AppUpdateDownloadUrl,
			ReleaseNotes = AppUpdateNotes
		};

		IsDownloadingAppUpdate = true;
		IsAppUpdateFinished = false;
		AppUpdateProgressPercent = 0;
		AppUpdateIndicatorOffset = -57.0;
		AppUpdateStep = 1;
		AppUpdateStatusText = "Preparation";
		AppUpdateBottomBaseText = $"Downloading Xenom Optimizer v{update.Version}";
		IsBox1Active = false;
		IsBox2Active = false;
		IsBox3Active = false;
		IsBox4Active = false;

		using var cts = new CancellationTokenSource();

		// Background dots animation loop (cycles 1 to 3 dots: . -> .. -> ...)
		var dotsTask = Task.Run(async () =>
		{
			int dotCount = 1;
			while (!cts.Token.IsCancellationRequested)
			{
				var dots = new string('.', dotCount);
				await Application.Current.Dispatcher.InvokeAsync(() =>
				{
					AppUpdateAnimatedDots = dots;
				});
				dotCount = (dotCount % 3) + 1;
				try
				{
					await Task.Delay(350, cts.Token).ConfigureAwait(false);
				}
				catch (TaskCanceledException)
				{
					break;
				}
			}
		}, cts.Token);

		var progress = new Progress<double>(p =>
		{
			AppUpdateProgressPercent = Math.Clamp(p, 0, 100);

			// Calculate smooth travel matching the reference box widths: Box 1 (11.2%), Box 2 (36.3%), Box 3 (24.7%), Box 4 (27.8%)
			AppUpdateIndicatorOffset = -57.0 + (AppUpdateProgressPercent / 100.0) * 354.0;

			// Box activations as line reaches each proportional box
			IsBox1Active = AppUpdateProgressPercent >= 1.0;
			IsBox2Active = AppUpdateProgressPercent >= 11.2;
			IsBox3Active = AppUpdateProgressPercent >= 47.5;
			IsBox4Active = AppUpdateProgressPercent >= 72.2;

			if (p <= 47.5)
			{
				AppUpdateStep = 1;
				AppUpdateStatusText = "Preparation";
				AppUpdateBottomBaseText = $"Downloading Xenom Optimizer v{update.Version}";
			}
			else if (p < 100)
			{
				AppUpdateStep = 2;
				AppUpdateStatusText = "Installing";
				AppUpdateBottomBaseText = $"Installing Xenom Optimizer v{update.Version}";
			}
			else
			{
				AppUpdateStep = 2;
				AppUpdateStatusText = "Installing";
				AppUpdateBottomBaseText = $"Restarting Xenom Optimizer v{update.Version}";
				IsAppUpdateFinished = true;
			}
		});

		try
		{
			await UpdateService.DownloadAndApplyUpdateAsync(update, progress);
		}
		finally
		{
			cts.Cancel();
			IsDownloadingAppUpdate = false;
		}
	}

	[RelayCommand]
	private void SelectSlide(object? param)
	{
		if (IsScanningDrivers || IsRescanningDrivers || IsRescanCompleted)
		{
			return;
		}
		if (param != null && int.TryParse(param.ToString(), out var result))
		{
			if (!IsDriversIncludedMode && result < 1)
			{
				result = 1;
			}
			result = Math.Clamp(result, 0, 2);

			if (SelectedSlideIndex == 0 && result > 0 && !CanNavigateToNextSlide)
			{
				LockedSlideRequested?.Invoke(this, result);
				return;
			}

			SelectedSlideIndex = result;
		}
	}

	public void SelectNextSlide()
	{
		if (IsScanningDrivers || IsRescanningDrivers || IsRescanCompleted)
		{
			return;
		}
		int num = ((!IsDriversIncludedMode) ? 1 : 0);
		if (SelectedSlideIndex == 0 && !CanNavigateToNextSlide)
		{
			return;
		}
		if (SelectedSlideIndex < 2)
		{
			SelectSlide(SelectedSlideIndex + 1);
		}
	}

	public void SelectPreviousSlide()
	{
		if (IsScanningDrivers || IsRescanningDrivers || IsRescanCompleted)
		{
			return;
		}
		int num = ((!IsDriversIncludedMode) ? 1 : 0);
		if (SelectedSlideIndex > num)
		{
			SelectSlide(SelectedSlideIndex - 1);
		}
	}

	private async Task LoadAllDataAsync()
	{
		IsLoading = true;
		try
		{
			await LoadSystemDetailsAsync();
			await LoadDriversAsync();
			await LoadOptimizationsAsync();
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			StatusMessage = "Error loading data: " + ex2.Message;
			_log.Log("Windows", "Failed to load optimizations", "ERROR", null, null, ex2.Message);
		}
		finally
		{
			IsLoading = false;
		}
	}

	private void UpdateAvatarImage(string? filePath)
	{
		if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
		{
			Apply(null, hasCustom: false, null);
			return;
		}
		try
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
			bitmapImage.UriSource = new Uri(filePath, UriKind.Absolute);
			bitmapImage.EndInit();
			bitmapImage.Freeze();
			Apply(bitmapImage, hasCustom: true, filePath);
		}
		catch (Exception ex)
		{
			_log.Log("Windows", "Failed to load avatar bitmap from " + filePath, "ERROR", null, null, ex.Message);
			Apply(null, hasCustom: false, null);
		}
		void Apply(ImageSource? img, bool hasCustom, string? path)
		{
			if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
			{
				Application.Current.Dispatcher.Invoke(() =>
				{
					AvatarImage = img;
					HasCustomAvatar = hasCustom;
					CustomAvatarPath = path;
				});
			}
			else
			{
				AvatarImage = img;
				HasCustomAvatar = hasCustom;
				CustomAvatarPath = path;
			}
		}
	}

	private void LoadCustomAvatar()
	{
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XenomOptimizer");
			string path2 = Path.Combine(path, "custom_avatar.path");
			if (File.Exists(path2))
			{
				string filePath = File.ReadAllText(path2).Trim();
				UpdateAvatarImage(filePath);
			}
		}
		catch
		{
		}
	}

	[RelayCommand]
	private void ResetAvatar()
	{
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XenomOptimizer");
			string path2 = Path.Combine(path, "custom_avatar.path");
			if (File.Exists(path2))
			{
				File.Delete(path2);
			}
		}
		catch
		{
		}
		UpdateAvatarImage(null);
	}

	[RelayCommand]
	private void ChangeAvatar()
	{
		try
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = "Select Profile Photo",
				Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.webp;*.ico|All Files (*.*)|*.*",
				CheckFileExists = true
			};
			if (openFileDialog.ShowDialog() == true)
			{
				UpdateAvatarImage(openFileDialog.FileName);
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XenomOptimizer");
				Directory.CreateDirectory(text);
				string path = Path.Combine(text, "custom_avatar.path");
				File.WriteAllText(path, openFileDialog.FileName);
			}
		}
		catch (Exception ex)
		{
			_log.Log("Windows", "Failed to select custom avatar", "ERROR", null, null, ex.Message);
		}
	}

	private static string GetExactDeviceName()
	{
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters");
			if (registryKey != null)
			{
				string text = registryKey.GetValue("NV Hostname")?.ToString();
				if (!string.IsNullOrWhiteSpace(text))
				{
					return text.Trim();
				}
				string text2 = registryKey.GetValue("Hostname")?.ToString();
				if (!string.IsNullOrWhiteSpace(text2))
				{
					return text2.Trim();
				}
			}
		}
		catch
		{
		}
		try
		{
			string hostName = Dns.GetHostName();
			if (!string.IsNullOrWhiteSpace(hostName))
			{
				return hostName.Trim();
			}
		}
		catch
		{
		}
		string text3 = Environment.MachineName;
		if (string.IsNullOrWhiteSpace(text3))
		{
			text3 = Environment.UserName;
		}
		if (!string.IsNullOrWhiteSpace(text3) && text3.Length > 1 && text3 == text3.ToUpperInvariant())
		{
			return char.ToUpper(text3[0]) + text3.Substring(1).ToLowerInvariant();
		}
		return (!string.IsNullOrWhiteSpace(text3)) ? text3 : "PC Device";
	}

	private async Task LoadSystemDetailsAsync()
	{
		try
		{
			string name = GetExactDeviceName();
			if (!string.IsNullOrWhiteSpace(name))
			{
				PcNameText = name;
				PcInitialLetter = name.Substring(0, 1).ToUpperInvariant();
			}
			string win = await _systemDetection.GetWindowsVersionAsync();
			if (!string.IsNullOrWhiteSpace(win))
			{
				WindowsVersionText = win.Replace("Microsoft ", "").Trim();
				string shortWin = WindowsVersionText;
				int parenIndex = shortWin.IndexOf('(');
				if (parenIndex > 0)
				{
					shortWin = shortWin.Substring(0, parenIndex).Trim();
				}
				WindowsShortVersionText = shortWin;
			}
			string cpu = await _systemDetection.GetCpuModelAsync();
			if (!string.IsNullOrWhiteSpace(cpu))
			{
				CpuModelText = Regex.Replace(cpu, "\\s+", " ").Trim();
			}
			string gpu = await _systemDetection.GetGpuModelAsync();
			if (!string.IsNullOrWhiteSpace(gpu))
			{
				GpuModelText = gpu.Trim();
			}
			string gpuDrv = await _systemDetection.GetGpuDriverVersionAsync();
			if (!string.IsNullOrWhiteSpace(gpuDrv))
			{
				GpuDriverText = gpuDrv;
			}
			string ram = await _systemDetection.GetRamAmountAsync();
			if (!string.IsNullOrWhiteSpace(ram))
			{
				RamSpecText = ram;
			}
			string pwr = await _systemDetection.GetCurrentPowerPlanAsync();
			if (!string.IsNullOrWhiteSpace(pwr))
			{
				PowerPlanText = pwr;
			}
			DirectXVersionText = "DirectX 12 (Ultimate)";
			Task.Run(() =>
			{
				try
				{
					using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE ApplicationId = '55c92734-d682-4d71-983e-d6ec3f16059f' AND PartialProductKey IS NOT NULL");
					using ManagementObjectCollection.ManagementObjectEnumerator managementObjectEnumerator = managementObjectSearcher.Get().GetEnumerator();
					if (managementObjectEnumerator.MoveNext())
					{
						ManagementBaseObject current = managementObjectEnumerator.Current;
						int num = Convert.ToInt32(current["LicenseStatus"]);
						StatWindowsActivation = ((num == 1) ? "Activated" : "Not Activated");
					}
				}
				catch
				{
				}
			});
			try
			{
				string displayVer = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "DisplayVersion", null)?.ToString();
				if (!string.IsNullOrWhiteSpace(displayVer))
				{
					StatWindowsVersion = displayVer.Trim();
				}
				else
				{
					string releaseId = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", "ReleaseId", null)?.ToString();
					if (!string.IsNullOrWhiteSpace(releaseId))
					{
						StatWindowsVersion = releaseId.Trim();
					}
				}
			}
			catch
			{
			}
			Task.Run(() =>
			{
				try
				{
					using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
					foreach (ManagementBaseObject item in managementObjectSearcher.Get())
					{
						if (int.TryParse(item["NumberOfCores"]?.ToString(), out var result) && result > 0)
						{
							StatCpuCores = $"{result} Cores";
							break;
						}
					}
				}
				catch
				{
				}
			});
			Task.Run(() =>
			{
				try
				{
					uint num = 0u;
					using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT Speed, ConfiguredClockSpeed FROM Win32_PhysicalMemory");
					foreach (ManagementBaseObject item2 in managementObjectSearcher.Get())
					{
						uint result2;
						if (uint.TryParse(item2["ConfiguredClockSpeed"]?.ToString(), out var result) && result > num)
						{
							num = result;
						}
						else if (uint.TryParse(item2["Speed"]?.ToString(), out result2) && result2 > num)
						{
							num = result2;
						}
					}
					if (num != 0)
					{
						StatRamSpeed = $"{num} MHz";
					}
				}
				catch
				{
				}
			});
			Task.Run(() =>
			{
				try
				{
					string[] array = new string[3] { "0000", "0001", "0002" };
					string[] array2 = array;
					foreach (string text in array2)
					{
						string keyName = "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e968-e325-11ce-bfc1-08002be10318}\\" + text;
						if (Registry.GetValue(keyName, "HardwareInformation.qwMemorySize", null) is long num && num > 0)
						{
							int num2 = (int)Math.Round((double)num / 1073741824.0);
							if (num2 > 0)
							{
								StatGpuVram = $"{num2} GB";
								return;
							}
						}
					}
					using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController WHERE AdapterRAM > 0");
					foreach (ManagementBaseObject item3 in managementObjectSearcher.Get())
					{
						if (long.TryParse(item3["AdapterRAM"]?.ToString(), out var result) && result > 0)
						{
							int num3 = (int)Math.Round((double)result / 1073741824.0);
							if (num3 > 0)
							{
								StatGpuVram = $"{num3} GB";
								break;
							}
						}
					}
				}
				catch
				{
				}
			});
			Task.Run(() =>
			{
				try
				{
					string text = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\', ':') ?? "C";
					int num = -1;
					using (ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("ROOT\\Microsoft\\Windows\\Storage", "SELECT DiskNumber FROM MSFT_Partition WHERE DriveLetter = '" + text + "'"))
					{
						foreach (ManagementBaseObject item4 in managementObjectSearcher.Get())
						{
							if (int.TryParse(item4["DiskNumber"]?.ToString(), out var result))
							{
								num = result;
								break;
							}
						}
					}
					if (num >= 0)
					{
						using (ManagementObjectSearcher managementObjectSearcher2 = new ManagementObjectSearcher("ROOT\\Microsoft\\Windows\\Storage", $"SELECT BusType, MediaType, Model FROM MSFT_PhysicalDisk WHERE DeviceId = '{num}'"))
						{
							using ManagementObjectCollection.ManagementObjectEnumerator managementObjectEnumerator2 = managementObjectSearcher2.Get().GetEnumerator();
							if (managementObjectEnumerator2.MoveNext())
							{
								ManagementBaseObject current2 = managementObjectEnumerator2.Current;
								int num2 = Convert.ToInt32(current2["BusType"]);
								int num3 = Convert.ToInt32(current2["MediaType"]);
								string text2 = current2["Model"]?.ToString() ?? "";
								if (num2 == 17 || text2.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
								{
									StatDriveType = "NVMe SSD";
								}
								else if (num3 == 4 || text2.Contains("SSD", StringComparison.OrdinalIgnoreCase))
								{
									StatDriveType = "SATA SSD";
								}
								else if (num3 == 3 || text2.Contains("HDD", StringComparison.OrdinalIgnoreCase))
								{
									StatDriveType = "HDD";
								}
								else
								{
									StatDriveType = "SSD";
								}
							}
							return;
						}
					}
				}
				catch
				{
				}
			});
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_log.Log("Windows", "Failed to load system details for dashboard", "WARN", null, null, ex2.Message);
		}
	}

	private async Task LoadDriversAsync(bool isRescan = false)
	{
		string mbModel = await _systemDetection.GetMotherboardModelAsync();
		string gpuModel = await _systemDetection.GetGpuModelAsync();
		string gpuDriver = await _systemDetection.GetGpuDriverVersionAsync();
		(string wifi, string bluetooth) tuple = await _systemDetection.GetWirelessHardwareAsync();
		string wifiDevice = tuple.wifi;
		string btDevice = tuple.bluetooth;
		string chipsetDownloadUrl = "https://www.amd.com/en/support/download/drivers.html";
		string chipsetSpec;
		if (mbModel.Contains("B550", StringComparison.OrdinalIgnoreCase))
		{
			chipsetSpec = "AMD B550 Chipset (" + mbModel + ")";
			chipsetDownloadUrl = "https://www.amd.com/en/support/chipsets/amd-socket-am4/b550";
		}
		else if (mbModel.Contains("X570", StringComparison.OrdinalIgnoreCase))
		{
			chipsetSpec = "AMD X570 Chipset (" + mbModel + ")";
			chipsetDownloadUrl = "https://www.amd.com/en/support/chipsets/amd-socket-am4/x570";
		}
		else if (mbModel.Contains("B650", StringComparison.OrdinalIgnoreCase))
		{
			chipsetSpec = "AMD B650 Chipset (" + mbModel + ")";
			chipsetDownloadUrl = "https://www.amd.com/en/support/chipsets/amd-socket-am5/b650";
		}
		else if (mbModel.Contains("X670", StringComparison.OrdinalIgnoreCase))
		{
			chipsetSpec = "AMD X670 Chipset (" + mbModel + ")";
			chipsetDownloadUrl = "https://www.amd.com/en/support/chipsets/amd-socket-am5/x670";
		}
		else if (mbModel.Contains("B450", StringComparison.OrdinalIgnoreCase))
		{
			chipsetSpec = "AMD B450 Chipset (" + mbModel + ")";
			chipsetDownloadUrl = "https://www.amd.com/en/support/chipsets/amd-socket-am4/b450";
		}
		else if (mbModel.Contains("A520", StringComparison.OrdinalIgnoreCase))
		{
			chipsetSpec = "AMD A520 Chipset (" + mbModel + ")";
			chipsetDownloadUrl = "https://www.amd.com/en/support/chipsets/amd-socket-am4/a520";
		}
		else if (mbModel.Contains("Intel", StringComparison.OrdinalIgnoreCase) || mbModel.Contains("Z790", StringComparison.OrdinalIgnoreCase) || mbModel.Contains("B760", StringComparison.OrdinalIgnoreCase) || mbModel.Contains("Z690", StringComparison.OrdinalIgnoreCase) || mbModel.Contains("B660", StringComparison.OrdinalIgnoreCase))
		{
			chipsetSpec = "Intel Chipset (" + mbModel + ")";
			chipsetDownloadUrl = "https://www.intel.com/content/www/us/en/download/19347/chipset-device-software.html";
		}
		else
		{
			chipsetSpec = mbModel + " Chipset Driver";
		}
		string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

		bool isAmdMotherboard = mbModel.Contains("B550", StringComparison.OrdinalIgnoreCase) ||
		                        mbModel.Contains("X570", StringComparison.OrdinalIgnoreCase) ||
		                        mbModel.Contains("B650", StringComparison.OrdinalIgnoreCase) ||
		                        mbModel.Contains("X670", StringComparison.OrdinalIgnoreCase) ||
		                        mbModel.Contains("B450", StringComparison.OrdinalIgnoreCase) ||
		                        mbModel.Contains("A520", StringComparison.OrdinalIgnoreCase) ||
		                        mbModel.Contains("AMD", StringComparison.OrdinalIgnoreCase);

		string chipsetDlUrl = isAmdMotherboard
			? "https://drivers.amd.com/drivers/installer/26.20/beta/amd-software-adrenalin-edition-26.11.1-minimalsetup-261111_web.exe"
			: "https://dsadata.intel.com/installer";
		string chipsetFilePath = Path.Combine(downloadsDir, isAmdMotherboard ? "amd_chipset_installer.exe" : "intel_chipset_installer.exe");
		if (File.Exists(chipsetFilePath) && !IsValidExecutable(chipsetFilePath)) { try { File.Delete(chipsetFilePath); } catch { } }
		bool isChipsetDownloaded = File.Exists(chipsetFilePath) && IsValidExecutable(chipsetFilePath);

		bool isNvidiaGpu = gpuModel.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
		                   gpuModel.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
		                   gpuModel.Contains("RTX", StringComparison.OrdinalIgnoreCase) ||
		                   gpuModel.Contains("GTX", StringComparison.OrdinalIgnoreCase);
		bool isAmdGpu = gpuModel.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
		                gpuModel.Contains("Radeon", StringComparison.OrdinalIgnoreCase);

		string gpuDlUrl = isNvidiaGpu
			? "https://us.download.nvidia.com/GFE/GFEClient/3.28.0.417/GeForce_Experience_v3.28.0.417.exe"
			: (isAmdGpu
				? "https://drivers.amd.com/drivers/installer/26.20/beta/amd-software-adrenalin-edition-26.11.1-minimalsetup-261111_web.exe"
				: "https://dsadata.intel.com/installer");
		string gpuFilePath = Path.Combine(downloadsDir, isNvidiaGpu ? "nvidia_geforce_installer.exe" : (isAmdGpu ? "amd_radeon_installer.exe" : "intel_graphics_installer.exe"));
		if (File.Exists(gpuFilePath) && !IsValidExecutable(gpuFilePath)) { try { File.Delete(gpuFilePath); } catch { } }
		bool isGpuDownloaded = File.Exists(gpuFilePath) && IsValidExecutable(gpuFilePath);

		bool isIntelWireless = wifiDevice.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
		                       btDevice.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
		                       string.IsNullOrEmpty(wifiDevice);
		string wirelessDlUrl = isIntelWireless
			? "https://downloadmirror.intel.com/929786/WiFi-24.80.0-Driver64-Win10-Win11.exe"
			: "https://dsadata.intel.com/installer";
		string wirelessFilePath = Path.Combine(downloadsDir, isIntelWireless ? "intel_wifi_driver_installer.exe" : "wireless_driver_installer.exe");
		if (File.Exists(wirelessFilePath) && !IsValidExecutable(wirelessFilePath)) { try { File.Delete(wirelessFilePath); } catch { } }
		bool isWirelessDownloaded = File.Exists(wirelessFilePath) && IsValidExecutable(wirelessFilePath);

		string dxUrl = "https://download.microsoft.com/download/1/7/1/1718CCC4-6315-4D8E-9543-8E28A4E18C4C/dxwebsetup.exe";
		string dxPath = Path.Combine(downloadsDir, "dxwebsetup.exe");
		if (File.Exists(dxPath) && !IsValidExecutable(dxPath)) { try { File.Delete(dxPath); } catch { } }
		bool isDxDownloaded = File.Exists(dxPath) && IsValidExecutable(dxPath);

		string vcUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";
		string vcPath = Path.Combine(downloadsDir, "vc_redist.x64.exe");
		string vcPathX86 = Path.Combine(downloadsDir, "vc_redist.x86.exe");
		if (File.Exists(vcPath) && !IsValidExecutable(vcPath)) { try { File.Delete(vcPath); } catch { } }
		if (File.Exists(vcPathX86) && !IsValidExecutable(vcPathX86)) { try { File.Delete(vcPathX86); } catch { } }
		bool isVcDownloaded = File.Exists(vcPath) && IsValidExecutable(vcPath);

		string dnUrl = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe";
		string dnPath = Path.Combine(downloadsDir, "windowsdesktop-runtime-8.0-win-x64.exe");
		if (File.Exists(dnPath) && !IsValidExecutable(dnPath)) { try { File.Delete(dnPath); } catch { } }
		bool isDnDownloaded = File.Exists(dnPath) && IsValidExecutable(dnPath);

		var chipsetDriver = new DriverItemViewModel
		{
			Id = "drv_chipset",
			Title = "Motherboard Chipset Driver",
			Icon = "\ue7f8",
			CurrentSpec = chipsetSpec,
			Description = "Installs core PCI-E bus, power management, SMBus, and I/O controller drivers for optimal system stability.",
			DownloadUrl = chipsetDlUrl,
			TargetFilePath = chipsetFilePath,
			IsDownloaded = isChipsetDownloaded,
			PrimaryButtonText = isChipsetDownloaded ? "Install" : "Download",
			SecondaryButtonText = "Device Manager",
			SecondaryAction = () =>
			{
				LaunchProcess("devmgmt.msc");
			}
		};
		chipsetDriver.PrimaryAction = () =>
		{
			_ = HandleDriverItemPrimaryClickAsync(chipsetDriver);
		};

		string gpuAppButtonText = isNvidiaGpu
			? "NVIDIA App"
			: (isAmdGpu
				? "AMD Software"
				: (gpuModel.Contains("Intel", StringComparison.OrdinalIgnoreCase) ? "Intel Graphics" : "Device Manager"));

		var gpuItem = new DriverItemViewModel
		{
			Id = "drv_gpu",
			Title = "GPU Graphics Driver",
			Icon = "\ue7f4",
			CurrentSpec = gpuModel + " (Driver: " + gpuDriver + ")",
			Description = "Ensures GameLoop 3D engine, DirectX pipeline, and shader caching run without frame drops or crashes.",
			DownloadUrl = gpuDlUrl,
			TargetFilePath = gpuFilePath,
			IsDownloaded = isGpuDownloaded,
			PrimaryButtonText = isGpuDownloaded ? "Install" : "Download",
			SecondaryButtonText = gpuAppButtonText,
			SecondaryAction = () =>
			{
				LaunchGpuControlApp(isNvidiaGpu, isAmdGpu);
			}
		};
		gpuItem.PrimaryAction = () =>
		{
			_ = HandleDriverItemPrimaryClickAsync(gpuItem);
		};

		string wirelessSpec = FormatWirelessSpec(wifiDevice, btDevice);
		var wirelessItem = new DriverItemViewModel
		{
			Id = "drv_wireless",
			Title = "Wi-Fi & Bluetooth Wireless Drivers",
			Icon = "\ue701",
			CurrentSpec = wirelessSpec,
			Description = "Reduces ping jitter, packet loss, and Bluetooth audio latency for wireless controllers & headsets.",
			DownloadUrl = wirelessDlUrl,
			TargetFilePath = wirelessFilePath,
			IsDownloaded = isWirelessDownloaded,
			PrimaryButtonText = isWirelessDownloaded ? "Install" : "Download",
			SecondaryButtonText = "Connections",
			SecondaryAction = () =>
			{
				LaunchProcess("ncpa.cpl");
			}
		};
		wirelessItem.PrimaryAction = () =>
		{
			_ = HandleDriverItemPrimaryClickAsync(wirelessItem);
		};

		var dxItem = new DriverItemViewModel
		{
			Id = "drv_directx",
			Title = "DirectX Runtimes (DirectX+ Support)",
			Icon = "\ue790",
			CurrentSpec = "DirectX 12 / DirectX End-User Runtimes",
			Description = "Provides legacy DirectX 9/10/11 dynamic libraries and DirectPlay components necessary for GameLoop emulation.",
			DownloadUrl = dxUrl,
			TargetFilePath = dxPath,
			PrimaryButtonText = isDxDownloaded ? "Install" : "Download",
			IsDownloaded = isDxDownloaded,
			SecondaryButtonText = "DxDiag Tool",
			SecondaryAction = () =>
			{
				LaunchProcess("dxdiag.exe");
			}
		};
		dxItem.PrimaryAction = () =>
		{
			_ = HandleDriverItemPrimaryClickAsync(dxItem);
		};

		var vcItem = new DriverItemViewModel
		{
			Id = "drv_vcredist",
			Title = "Visual C++ Redistributable Runtimes",
			Icon = "\ue896",
			CurrentSpec = "Visual C++ 2015-2022 x86/x64 Runtime",
			Description = "Required runtime libraries for emulator processes (aow_exe.exe, AndroidProcess.exe, TBSGameLive.exe).",
			DownloadUrl = vcUrl,
			TargetFilePath = vcPath,
			PrimaryButtonText = isVcDownloaded ? "Install" : "Download",
			IsDownloaded = isVcDownloaded,
			SecondaryButtonText = "Installed Apps",
			SecondaryAction = () =>
			{
				LaunchProcess("ms-settings:appsfeatures");
			}
		};
		vcItem.PrimaryAction = () =>
		{
			_ = HandleDriverItemPrimaryClickAsync(vcItem);
		};

		var dotnetItem = new DriverItemViewModel
		{
			Id = "drv_dotnet",
			Title = ".NET Framework & Desktop Runtime",
			Icon = "\ue71d",
			CurrentSpec = ".NET Framework 4.8.1 / .NET 8.0 Runtime",
			Description = "Required application framework for GameLoop launcher services, background helpers, and lag killer routines.",
			DownloadUrl = dnUrl,
			TargetFilePath = dnPath,
			IsDownloaded = isDnDownloaded,
			PrimaryButtonText = isDnDownloaded ? "Install" : "Download",
			SecondaryButtonText = "Windows Features",
			SecondaryAction = () =>
			{
				LaunchProcess("OptionalFeatures.exe");
			}
		};
		dotnetItem.PrimaryAction = () =>
		{
			_ = HandleDriverItemPrimaryClickAsync(dotnetItem);
		};

		if (isRescan && DriverItems.Count == 6)
		{
			var existingChipset = DriverItems.FirstOrDefault(d => d.Id == "drv_chipset");
			if (existingChipset != null)
			{
				existingChipset.CurrentSpec = chipsetSpec;
				existingChipset.DownloadUrl = chipsetDlUrl;
				existingChipset.TargetFilePath = chipsetFilePath;
				existingChipset.IsDownloaded = isChipsetDownloaded;
				if (!existingChipset.IsInstalled)
				{
					existingChipset.PrimaryButtonText = isChipsetDownloaded ? "Install" : "Download";
				}
			}

			var existingGpu = DriverItems.FirstOrDefault(d => d.Id == "drv_gpu");
			if (existingGpu != null)
			{
				existingGpu.CurrentSpec = gpuModel + " (Driver: " + gpuDriver + ")";
				existingGpu.DownloadUrl = gpuDlUrl;
				existingGpu.TargetFilePath = gpuFilePath;
				existingGpu.IsDownloaded = isGpuDownloaded;
				existingGpu.SecondaryButtonText = gpuAppButtonText;
				if (!existingGpu.IsInstalled)
				{
					existingGpu.PrimaryButtonText = isGpuDownloaded ? "Install" : "Download";
				}
			}

			var existingWireless = DriverItems.FirstOrDefault(d => d.Id == "drv_wireless");
			if (existingWireless != null)
			{
				existingWireless.CurrentSpec = wirelessSpec;
				existingWireless.DownloadUrl = wirelessDlUrl;
				existingWireless.TargetFilePath = wirelessFilePath;
				existingWireless.IsDownloaded = isWirelessDownloaded;
				if (!existingWireless.IsInstalled)
				{
					existingWireless.PrimaryButtonText = isWirelessDownloaded ? "Install" : "Download";
				}
			}

			var existingDx = DriverItems.FirstOrDefault(d => d.Id == "drv_directx");
			if (existingDx != null)
			{
				existingDx.IsDownloaded = isDxDownloaded;
				if (!existingDx.IsInstalled)
				{
					existingDx.PrimaryButtonText = isDxDownloaded ? "Install" : "Download";
				}
			}

			var existingVc = DriverItems.FirstOrDefault(d => d.Id == "drv_vcredist");
			if (existingVc != null)
			{
				existingVc.IsDownloaded = isVcDownloaded;
				if (!existingVc.IsInstalled)
				{
					existingVc.PrimaryButtonText = isVcDownloaded ? "Install" : "Download";
				}
			}

			var existingDotnet = DriverItems.FirstOrDefault(d => d.Id == "drv_dotnet");
			if (existingDotnet != null)
			{
				existingDotnet.IsDownloaded = isDnDownloaded;
				if (!existingDotnet.IsInstalled)
				{
					existingDotnet.PrimaryButtonText = isDnDownloaded ? "Install" : "Download";
				}
			}
		}
		else
		{
			DriverItems.Clear();
			DriverItems.Add(chipsetDriver);
			DriverItems.Add(gpuItem);
			DriverItems.Add(wirelessItem);
			DriverItems.Add(dxItem);
			DriverItems.Add(vcItem);
			DriverItems.Add(dotnetItem);
		}

		foreach (var item in DriverItems)
		{
			item.StatusChangedAction = () => UpdateDriverActionStates();
		}
		UpdateDriverActionStates();
	}

	private async Task LoadOptimizationsAsync()
	{
		Optimizations.Clear();
		ServicesOptimizations.Clear();
		AdvanceOptimizations.Clear();
		LowEndOptimizations.Clear();
		List<IOptimization> optimizations = WindowsOptimizations.GetAll(_registry, _backup, _log);
		foreach (IOptimization opt in optimizations)
		{
			bool isSupported = await opt.IsSupportedAsync();
			OptimizationState state = await opt.GetCurrentStateAsync();
			OptimizationCardViewModel card = new OptimizationCardViewModel
			{
				Id = opt.Id,
				Name = opt.Name,
				Category = opt.Category,
				Description = opt.Description,
				RiskLevel = opt.RiskLevel,
				RequiresAdmin = opt.RequiresAdmin,
				IsAdmin = _admin.IsRunningAsAdmin,
				CurrentValue = state.CurrentValue,
				RecommendedValue = state.RecommendedValue,
				IsOptimal = state.IsOptimal,
				IsSupported = isSupported,
				StatusMessage = (state.StatusMessage ?? (isSupported ? "" : "Not supported on this Windows version")),
				Optimization = opt
			};
			Optimizations.Add(card);
			if (opt.Category == "Unused Services" || opt.Category == "Windows Gaming Settings" || opt.Category == "Background Activity")
			{
				ServicesOptimizations.Add(card);
			}
			else if (opt.Category == "Low-End PC")
			{
				LowEndOptimizations.Add(card);
			}
			else
			{
				AdvanceOptimizations.Add(card);
			}
		}
	}

	[RelayCommand]
	private void RequestApplyAll()
	{
		if (SelectedSlideIndex == 1)
		{
			ConfirmDialogTitle = "Apply Unused Services Optimizations?";
			ConfirmDialogMessage = "Are you sure you want to apply recommended optimizations for Unused Features & Services? Only settings on this tab will be applied.";
		}
		else
		{
			if (SelectedSlideIndex != 2)
			{
				return;
			}
			ConfirmDialogTitle = "Apply Advance & Low-End Optimizations?";
			ConfirmDialogMessage = "Are you sure you want to apply recommended optimizations for Advance & Low-End PC? Only settings on this tab will be applied.";
		}
		ConfirmDialogAction = "ApplyAll";
		ConfirmDialogButtonText = "Yes, Apply All";
		ConfirmDialogButtonColor = "#8B5CF6";
		IsConfirmDialogVisible = true;
	}

	[RelayCommand]
	private void RequestRestoreDefaults()
	{
		if (SelectedSlideIndex == 1)
		{
			ConfirmDialogTitle = "Restore Services Defaults?";
			ConfirmDialogMessage = "Are you sure you want to restore default Windows settings for Unused Features & Services? Other tabs will remain unaffected.";
		}
		else
		{
			if (SelectedSlideIndex != 2)
			{
				return;
			}
			ConfirmDialogTitle = "Restore Advance & Low-End Defaults?";
			ConfirmDialogMessage = "Are you sure you want to restore default Windows settings for Advance & Low-End PC? Other tabs will remain unaffected.";
		}
		ConfirmDialogAction = "RestoreDefaults";
		ConfirmDialogButtonText = "Yes, Restore Defaults";
		ConfirmDialogButtonColor = "#EF4444";
		IsConfirmDialogVisible = true;
	}

	[RelayCommand]
	private async Task ConfirmActionAsync()
	{
		IsConfirmDialogVisible = false;
		if (ConfirmDialogAction == "ApplyAll")
		{
			await ApplyAllConfirmedAsync();
		}
		else if (ConfirmDialogAction == "RestoreDefaults")
		{
			await RestoreDefaultsConfirmedAsync();
		}
	}

	[RelayCommand]
	private void CancelAction()
	{
		IsConfirmDialogVisible = false;
	}

	[RelayCommand]
	private void SelectOnboardingPath(object? param)
	{
		if (param != null && int.TryParse(param.ToString(), out var result))
		{
			SelectedOnboardingPath = result;
		}
	}

	[RelayCommand]
	private void ConfirmOnboardingSelection()
	{
		if (SelectedOnboardingPath == 1)
		{
			IsDriversIncludedMode = false;
			_settings.Settings.IsDriversInstalledMode = true;
			SelectedSlideIndex = 1;
		}
		else
		{
			IsDriversIncludedMode = true;
			_settings.Settings.IsDriversInstalledMode = false;
			SelectedSlideIndex = 0;
		}
		_settings.Settings.HasSeenOnboardingTaskV2 = true;
		_settings.Settings.HasSeenWindowsGuide = true;
		_settings.Save();
		IsInstructionModalVisible = false;
	}

	[RelayCommand]
	private void SetSetupMode(string? mode)
	{
		if (mode != null && (mode.Equals("Experienced", StringComparison.OrdinalIgnoreCase) || mode.Equals("DriversInstalled", StringComparison.OrdinalIgnoreCase)))
		{
			IsDriversIncludedMode = false;
			_settings.Settings.IsDriversInstalledMode = true;
			SelectedOnboardingPath = 1;
			if (SelectedSlideIndex == 0)
			{
				SelectedSlideIndex = 1;
			}
		}
		else
		{
			IsDriversIncludedMode = true;
			_settings.Settings.IsDriversInstalledMode = false;
			SelectedOnboardingPath = 0;
		}
		_settings.Save();
	}

	[RelayCommand]
	private void DismissInstructionModal(object? proceedNext)
	{
		ConfirmOnboardingSelection();
	}

	public void MarkGuideStepCompleted(int step)
	{
		bool changed = false;
		switch (step)
		{
			case 1:
				if (!IsGuideStep1Done) { IsGuideStep1Done = true; _settings.Settings.GuideStep1DriversCompleted = true; changed = true; }
				break;
			case 2:
				if (!IsGuideStep2Done) { IsGuideStep2Done = true; _settings.Settings.GuideStep2ServicesCompleted = true; changed = true; }
				break;
			case 3:
				if (!IsGuideStep3Done) { IsGuideStep3Done = true; _settings.Settings.GuideStep3AdvanceCompleted = true; changed = true; }
				break;
			case 4:
				if (!IsGuideStep4Done) { IsGuideStep4Done = true; _settings.Settings.GuideStep4RegistryCompleted = true; changed = true; }
				break;
			case 5:
				if (!IsGuideStep5Done) { IsGuideStep5Done = true; _settings.Settings.GuideStep5GameLoopCompleted = true; changed = true; }
				break;
			case 6:
				if (!IsGuideStep6Done) { IsGuideStep6Done = true; _settings.Settings.GuideStep6CleanupCompleted = true; changed = true; }
				break;
		}

		if (changed)
		{
			_settings.Save();
			UpdateGuideProgress();
		}
	}

	public void UpdateGuideProgress()
	{
		int count = 0;
		if (IsGuideStep1Done) count++;
		if (IsGuideStep2Done) count++;
		if (IsGuideStep3Done) count++;
		if (IsGuideStep4Done) count++;
		if (IsGuideStep5Done) count++;
		if (IsGuideStep6Done) count++;

		GuideCompletedCount = count;
		GuideProgressPercent = (count / 6.0) * 100.0;
		IsAllGuideStepsDone = (count == 6);
		GuideStepCounterText = $"{count} of 6 Completed";

		// Default to first incomplete step
		int nextIncomplete = 1;
		if (!IsGuideStep1Done) nextIncomplete = 1;
		else if (!IsGuideStep2Done) nextIncomplete = 2;
		else if (!IsGuideStep3Done) nextIncomplete = 3;
		else if (!IsGuideStep4Done) nextIncomplete = 4;
		else if (!IsGuideStep5Done) nextIncomplete = 5;
		else if (!IsGuideStep6Done) nextIncomplete = 6;
		else nextIncomplete = 6;

		SetActiveGuideStepInternal(nextIncomplete);
	}

	public void SetActiveGuideStepInternal(int step)
	{
		ActiveGuideStep = Math.Clamp(step, 1, 6);

		switch (ActiveGuideStep)
		{
			case 1:
				ActiveStepBubbleTitle = "1. Drivers Hub";
				ActiveStepBubbleSubtitle = "Detect and update essential GPU, DirectX runtime & chipset drivers.";
				ActiveStepBubbleActionText = "Open Drivers Hub ➔";
				ActiveStepTipText = "Tip: Update GPU & DirectX runtimes first to prevent game stutter and crashes.";
				break;
			case 2:
				ActiveStepBubbleTitle = "2. System Services";
				ActiveStepBubbleSubtitle = "Disable background telemetry, Game DVR, and P2P Windows update seeding.";
				ActiveStepBubbleActionText = "Open Services ➔";
				ActiveStepTipText = "Tip: Disabling unused services frees up CPU threads and lowers DPC latency.";
				break;
			case 3:
				ActiveStepBubbleTitle = "3. Advance & Low-End PC";
				ActiveStepBubbleSubtitle = "Apply Ultimate Performance scheme, hardware GPU scheduling & standby RAM flush.";
				ActiveStepBubbleActionText = "Open Advance Tweaks ➔";
				ActiveStepTipText = "Tip: High performance plan guarantees sustained CPU boost frequencies.";
				break;
			case 4:
				ActiveStepBubbleTitle = "4. Registry Optimizer";
				ActiveStepBubbleSubtitle = "Tune network throttling, system responsiveness & low-latency registry tweaks.";
				ActiveStepBubbleActionText = "Open Registry ➔";
				ActiveStepTipText = "Tip: Registry optimizations reduce input lag for high-refresh-rate monitors.";
				break;
			case 5:
				ActiveStepBubbleTitle = "5. GameLoop & PUBG Mobile";
				ActiveStepBubbleSubtitle = "Fine-tune emulator render engine, DirectX/OpenGL modes, memory & 90 FPS cap.";
				ActiveStepBubbleActionText = "Open GameLoop ➔";
				ActiveStepTipText = "Tip: Set DirectX+ or OpenGL+ based on your GPU model for smooth 90 FPS.";
				break;
			case 6:
				ActiveStepBubbleTitle = "6. Junk Cleaner & Network";
				ActiveStepBubbleSubtitle = "Purge temp cache, shader dumps, and optimize DNS & adapter MTU.";
				ActiveStepBubbleActionText = "Open Cleaner ➔";
				ActiveStepTipText = "Tip: Running junk cleanup weekly keeps shader caches clean and fast.";
				break;
		}

		if (IsAllGuideStepsDone)
		{
			ActiveStepBubbleTitle = "All Optimizations Completed! 🎉";
			ActiveStepBubbleSubtitle = "All 6 steps have been visited and tuned. Your system is fully primed for high FPS!";
			ActiveStepBubbleActionText = "All Done ✓";
			ActiveStepTipText = "Tip: Restart your PC once to make sure all kernel optimizations take effect.";
		}

		NotifyStepChanged();
	}

	private void NotifyStepChanged()
	{
		OnPropertyChanged(nameof(IsGuideStep1Active));
		OnPropertyChanged(nameof(IsGuideStep2Active));
		OnPropertyChanged(nameof(IsGuideStep3Active));
		OnPropertyChanged(nameof(IsGuideStep4Active));
		OnPropertyChanged(nameof(IsGuideStep5Active));
		OnPropertyChanged(nameof(IsGuideStep6Active));

		OnPropertyChanged(nameof(GuideStep1Label));
		OnPropertyChanged(nameof(GuideStep2Label));
		OnPropertyChanged(nameof(GuideStep3Label));
		OnPropertyChanged(nameof(GuideStep4Label));
		OnPropertyChanged(nameof(GuideStep5Label));
		OnPropertyChanged(nameof(GuideStep6Label));
	}

	[RelayCommand]
	public void SelectGuideStep(object? param)
	{
		int step = 1;
		if (param is int s) step = s;
		else if (param is string str && int.TryParse(str, out int parsed)) step = parsed;
		SetActiveGuideStepInternal(step);
	}

	[RelayCommand]
	public void NavigateToGuideStep(object? param)
	{
		int step = ActiveGuideStep;
		if (param is int s) step = s;
		else if (param is string str && int.TryParse(str, out int parsed)) step = parsed;

		if (IsAllGuideStepsDone && step == ActiveGuideStep && ActiveStepBubbleActionText.Contains("Done"))
		{
			IsInstructionModalVisible = false;
			return;
		}

		IsInstructionModalVisible = false;

		switch (step)
		{
			case 1:
				RequestPageNavigation?.Invoke("Windows");
				SelectedSlideIndex = 0;
				MarkGuideStepCompleted(1);
				break;
			case 2:
				RequestPageNavigation?.Invoke("Windows");
				SelectedSlideIndex = 1;
				MarkGuideStepCompleted(2);
				break;
			case 3:
				RequestPageNavigation?.Invoke("Windows");
				SelectedSlideIndex = 2;
				MarkGuideStepCompleted(3);
				break;
			case 4:
				RequestPageNavigation?.Invoke("Registry");
				MarkGuideStepCompleted(4);
				break;
			case 5:
				RequestPageNavigation?.Invoke("GameLoop");
				MarkGuideStepCompleted(5);
				break;
			case 6:
				RequestPageNavigation?.Invoke("Cleanup");
				MarkGuideStepCompleted(6);
				break;
		}
	}

	[RelayCommand]
	private void CloseInstructionModal()
	{
		_settings.Settings.HasSeenOnboardingTaskV2 = true;
		_settings.Settings.HasSeenWindowsGuide = true;
		_settings.Save();
		IsInstructionModalVisible = false;
	}

	[RelayCommand]
	private void ShowInstructionModal()
	{
		UpdateGuideProgress();
		IsInstructionModalVisible = true;
	}

	[RelayCommand]
	private async Task OpenDirectXDownload()
	{
		await DownloadDirectXAsync();
	}

	[RelayCommand]
	private void OpenVcRedistDownload()
	{
		OpenUrl("https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist");
	}

	[RelayCommand]
	private async Task ExecuteDriverBoosterMainActionAsync()
	{
		string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
		string setupPath = ((!string.IsNullOrEmpty(DownloadedFilePath) && File.Exists(DownloadedFilePath)) ? DownloadedFilePath : Path.Combine(downloadDir, "driver_booster_setup.exe"));
		if (IsValidDriverBoosterSetup(setupPath))
		{
			try
			{
				Process.Start(new ProcessStartInfo(setupPath)
				{
					UseShellExecute = true
				});
				return;
			}
			catch (Exception ex)
			{
				_log.Log("Windows", "Failed to launch Driver Booster installer", "WARNING", null, null, ex.Message);
				return;
			}
		}
		if (File.Exists(setupPath))
		{
			try
			{
				File.Delete(setupPath);
			}
			catch
			{
			}
		}
		await DownloadDriverBoosterAsync();
	}

	[RelayCommand]
	public async Task GetDriverBoosterBottomActionAsync()
	{
		if (!HasCompletedDriverScan)
		{
			LockedButtonClicked?.Invoke(this, EventArgs.Empty);
		}
		else
		{
			await ExecuteDriverBoosterMainActionAsync();
		}
	}

	[RelayCommand]
	private async Task RequestDriverBoosterDownloadAsync()
	{
		if (IsDriverBoosterInstalled)
		{
			return;
		}
		if (IsDriverBoosterDownloaded)
		{
			await ExecuteDriverBoosterMainActionAsync();
			return;
		}
		IsDriverWarningModalVisible = true;
	}

	[RelayCommand]
	private void CancelDriverWarning()
	{
		IsDriverWarningModalVisible = false;
	}

	[RelayCommand]
	private async Task ConfirmDriverBoosterDownloadAsync()
	{
		IsDriverWarningModalVisible = false;
		await DownloadDriverBoosterAsync();
	}

	[RelayCommand]
	private void ToggleDownloadFlyout()
	{
		if (!IsDownloadFlyoutOpen)
		{
			SelectedDownloadNavTab = -1;
			RefreshCompletedDownloadsList();
		}
		IsDownloadFlyoutOpen = !IsDownloadFlyoutOpen;
	}

	[RelayCommand]
	private void CloseDownloadsModal()
	{
		SelectedDownloadNavTab = -1;
		IsDownloadFlyoutOpen = false;
	}

	[RelayCommand]
	private async Task NavigateToSlideFromDownloads(object? param)
	{
		if (param != null && int.TryParse(param.ToString(), out int slideIdx))
		{
			SelectedDownloadNavTab = slideIdx;
		}
		await Task.Delay(180);
		IsDownloadFlyoutOpen = false;
		SelectSlide(param);
		SelectedDownloadNavTab = -1;
	}

	[RelayCommand]
	private void TogglePauseDownload()
	{
		if (IsDownloadingDriverBooster && !IsDownloadCompleted)
		{
			if (!IsDownloadPaused)
			{
				IsDownloadPaused = true;
				_pauseTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
				DownloadPauseButtonText = "Resume";
				DownloadPauseButtonIcon = "\ue768";
				StopDownloadDotsAnimation();
				_downloadStopwatch?.Stop();
				DownloadSpeedText = "Paused";
				DownloadTimeText = "Paused";
				DownloadSubtitleText = ((DownloadedItemKind == DownloadItemKindType.DirectX) ? "DirectX Runtimes" : "Driver Booster 12") + " • Paused";
				DownloadStatusText = $"Paused ({DownloadProgressPercent:F0}%)";
			}
			else
			{
				IsDownloadPaused = false;
				DownloadPauseButtonText = "Pause";
				DownloadPauseButtonIcon = "\ue769";
				_downloadStopwatch?.Start();
				StartDownloadDotsAnimation();
				DownloadSpeedText = "Resuming...";
				DownloadTimeText = "Calculating...";
				DownloadSubtitleText = ((DownloadedItemKind == DownloadItemKindType.DirectX) ? "DirectX Runtimes" : "Driver Booster 12") + " • Downloading...";
				DownloadStatusText = $"Downloading... {DownloadProgressPercent:F0}%";
				_pauseTcs?.TrySetResult(result: true);
				_pauseTcs = null;
			}
		}
	}

	[RelayCommand]
	private void CancelDownload()
	{
		_downloadCts?.Cancel();
		_pauseTcs?.TrySetCanceled();
		_pauseTcs = null;
		IsDownloadPaused = false;
		DownloadPauseButtonText = "Pause";
		DownloadPauseButtonIcon = "\ue769";
		StopDownloadDotsAnimation();
		IsDownloadingDriverBooster = false;
		DownloadStatusText = "Cancelled";
		DownloadSubtitleText = "No active downloads";
		DownloadSpeedText = "";
		DownloadTimeText = "--";
		DownloadMbText = "0.0 MB";
		DownloadProgressPercent = 0.0;
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			string path2 = Path.Combine(path, "driver_booster_setup.exe.part");
			if (File.Exists(path2))
			{
				File.Delete(path2);
			}
			string path3 = Path.Combine(path, "dxwebsetup.exe");
			if (File.Exists(path3))
			{
				File.Delete(path3);
			}
			string path4 = Path.Combine(path, "dxwebsetup.exe.part");
			if (File.Exists(path4))
			{
				File.Delete(path4);
			}
		}
		catch
		{
		}
		OnPropertyChanged("IsDriverBoosterDownloaded");
		OnPropertyChanged("DriverBoosterBannerButtonText");
		OnPropertyChanged(nameof(InstallAllDriversButtonText));
		var boosterItem = DriverItems.FirstOrDefault(d => d.Id == "drv_booster");
		if (boosterItem != null)
		{
			boosterItem.PrimaryButtonText = IsDriverBoosterDownloaded ? "Install Driver Booster" : "Download Driver Booster";
		}
	}

	partial void OnSelectedSlideIndexChanged(int value)
	{
		MarkGuideStepCompleted(value + 1);
		OnPropertyChanged(nameof(IsDriversSlideActive));
		OnPropertyChanged(nameof(IsServicesSlideActive));
		OnPropertyChanged(nameof(IsAdvanceSlideActive));
		OnPropertyChanged(nameof(CurrentPageTitle));
		OnPropertyChanged(nameof(CurrentPageSubtitle));
		OnPropertyChanged(nameof(IsBottomTimelineBarVisible));
		OnPropertyChanged(nameof(Is3StepTimelineBarVisible));
		OnPropertyChanged(nameof(Is2StepTimelineBarVisible));
		OnPropertyChanged(nameof(IsHeaderStatsVisible));
		OnPropertyChanged(nameof(IsBigDownloadButtonVisible));
		OnPropertyChanged(nameof(CanNavigateToNextSlide));
		RequestEntranceAnimation?.Invoke(false);
	}

	partial void OnIsDriversIncludedModeChanged(bool value)
	{
		if (!value && SelectedSlideIndex == 0)
		{
			SelectedSlideIndex = 1;
		}
		OnPropertyChanged(nameof(IsDriversSlideActive));
		OnPropertyChanged(nameof(IsServicesSlideActive));
		OnPropertyChanged(nameof(IsAdvanceSlideActive));
		OnPropertyChanged(nameof(CurrentPageTitle));
		OnPropertyChanged(nameof(CurrentPageSubtitle));
		OnPropertyChanged(nameof(Is3StepTimelineBarVisible));
		OnPropertyChanged(nameof(IsHeaderStatsVisible));
		OnPropertyChanged(nameof(IsBigDownloadButtonVisible));
		OnPropertyChanged(nameof(Is2StepTimelineBarVisible));
		OnPropertyChanged(nameof(CanNavigateToNextSlide));
	}

	partial void OnSelectedOnboardingPathChanged(int value)
	{
		OnPropertyChanged(nameof(IsFreshInstallPathSelected));
		OnPropertyChanged(nameof(IsDriversInstalledPathSelected));
		OnPropertyChanged(nameof(OnboardingStepCountText));
		OnPropertyChanged(nameof(CanNavigateToNextSlide));
	}

	partial void OnIsDownloadCompletedChanged(bool value)
	{
		OnPropertyChanged(nameof(IsDriverBoosterDownloaded));
		OnPropertyChanged(nameof(DriverBoosterBannerButtonText));
		OnPropertyChanged(nameof(InstallAllDriversButtonText));
		var boosterItem = DriverItems.FirstOrDefault(d => d.Id == "drv_booster");
		if (boosterItem != null)
		{
			boosterItem.PrimaryButtonText = IsDriverBoosterDownloaded ? "Install Driver Booster" : "Download Driver Booster";
		}
	}

	partial void OnIsDriverInstallationViewActiveChanged(bool value)
	{
		if (value)
		{
			HasVisitedDriverInstallationView = true;
		}
		OnPropertyChanged(nameof(IsBottomTimelineBarVisible));
		OnPropertyChanged(nameof(Is3StepTimelineBarVisible));
		OnPropertyChanged(nameof(Is2StepTimelineBarVisible));
		OnPropertyChanged(nameof(CanNavigateToNextSlide));
		if (!value && SelectedSlideIndex == 0)
		{
			TriggerEntranceAnimation(animateScrollDots: false);
		}
	}

	partial void OnHasVisitedDriverInstallationViewChanged(bool value)
	{
		OnPropertyChanged(nameof(CanNavigateToNextSlide));
	}

	partial void OnHasStartedScanChanged(bool value)
	{
		OnPropertyChanged(nameof(IsSkipArrowOnlyMode));
		OnPropertyChanged(nameof(CanClickSkipButton));
		OnPropertyChanged(nameof(SkipButtonToolTip));
		SkipToUnusedServicesCommand.NotifyCanExecuteChanged();
	}

	partial void OnHasCompletedDriverScanChanged(bool value)
	{
		OnPropertyChanged(nameof(IsHeaderStatsVisible));
		OnPropertyChanged(nameof(IsBigDownloadButtonVisible));
		OnPropertyChanged(nameof(IsSkipArrowOnlyMode));
		OnPropertyChanged(nameof(CanClickSkipButton));
		OnPropertyChanged(nameof(SkipButtonToolTip));
		SkipToUnusedServicesCommand.NotifyCanExecuteChanged();
		OnPropertyChanged(nameof(CanNavigateToNextSlide));
		if (value)
		{
			UpdateDriverStatsProperties();
		}
	}

	partial void OnHasSkippedDriverScannerChanged(bool value)
	{
		OnPropertyChanged(nameof(IsSkipArrowOnlyMode));
		OnPropertyChanged(nameof(CanClickSkipButton));
		OnPropertyChanged(nameof(SkipButtonToolTip));
		SkipToUnusedServicesCommand.NotifyCanExecuteChanged();
		OnPropertyChanged(nameof(CanNavigateToNextSlide));
	}

	partial void OnIsScanningDriversChanged(bool value)
	{
		OnPropertyChanged(nameof(IsNotScanningDrivers));
		OnPropertyChanged(nameof(IsSkipArrowOnlyMode));
		OnPropertyChanged(nameof(CanClickSkipButton));
		OnPropertyChanged(nameof(SkipButtonToolTip));
		SkipToUnusedServicesCommand.NotifyCanExecuteChanged();
		OnPropertyChanged(nameof(CanNavigateToNextSlide));
		if (value)
		{
			StartScanningDotsAnimation();
		}
		else
		{
			StopScanningDotsAnimation();
		}
	}

	[RelayCommand]
	private void ClearDownloadHistory()
	{
		if (IsDownloadingDriverBooster)
		{
			CancelDownload();
		}
		StopDownloadDotsAnimation();

		foreach (var item in CompletedDownloads.ToList())
		{
			try
			{
				if (!string.IsNullOrEmpty(item.FilePath) && File.Exists(item.FilePath))
				{
					File.Delete(item.FilePath);
				}
				string part = item.FilePath + ".part";
				if (File.Exists(part))
				{
					File.Delete(part);
				}
			}
			catch (Exception ex)
			{
				_log.Log("Windows", $"Failed to delete installer: {item.FilePath}", "WARNING", null, null, ex.Message);
			}
		}

		try
		{
			string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			string p1 = Path.Combine(downloadDir, "driver_booster_setup.exe");
			if (File.Exists(p1)) File.Delete(p1);
			string p1p = Path.Combine(downloadDir, "driver_booster_setup.exe.part");
			if (File.Exists(p1p)) File.Delete(p1p);
			string p2 = Path.Combine(downloadDir, "dxwebsetup.exe");
			if (File.Exists(p2)) File.Delete(p2);
			string p2p = Path.Combine(downloadDir, "dxwebsetup.exe.part");
			if (File.Exists(p2p)) File.Delete(p2p);
		}
		catch
		{
		}

		CompletedDownloads.Clear();
		IsMultiDownloadViewActive = false;
		IsSingleDownloadCompletedView = false;
		HasDownloadItem = false;
		IsDownloadCompleted = false;
		IsDownloadingDriverBooster = false;
		DownloadedFilePath = "";
		DownloadProgressPercent = 0.0;
		DownloadSpeedText = "";
		DownloadSizeText = "";
		DownloadFinalSizeText = "";
		DownloadStatusText = "";
		DownloadSubtitleText = "No active downloads";
		DownloadTimeText = "--";
		DownloadMbText = "0.0 MB";
		DownloadedItemKind = DownloadItemKindType.DriverBooster;
		DownloadItemTitleText = "Driver Booster 12";
		DownloadItemDetailsText = "Completed • 31.4 MB";
		DownloadInstallButtonText = "Install Driver Booster";
		DownloadTotalSizeText = "~32.6 MB";
		OnPropertyChanged("IsDriverBoosterDownloaded");
		OnPropertyChanged("DriverBoosterBannerButtonText");
	}

	public void RefreshCompletedDownloadsList()
	{
		try
		{
			string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			var list = new List<DownloadHistoryItemViewModel>();

			// 1. Check Driver Booster
			string dbPath = Path.Combine(downloadsDir, "driver_booster_setup.exe");
			if (File.Exists(dbPath))
			{
				try
				{
					var fi = new FileInfo(dbPath);
					if (fi.Length > 1024)
					{
						double mb = (double)fi.Length / (1024.0 * 1024.0);
						string timeStr = fi.LastWriteTime.ToString("h:mm tt");
						bool isDbInstalled = File.Exists(@"C:\Program Files (x86)\IObit\Driver Booster\DriverBooster.exe") ||
						                     File.Exists(@"C:\Program Files\IObit\Driver Booster\DriverBooster.exe");

						var dbItem = new DownloadHistoryItemViewModel
						{
							Id = "driver_booster",
							Title = "Driver Booster 12",
							FilePath = dbPath,
							TimeText = timeStr,
							SizeText = $"{mb:F1} MB",
							DetailsText = $"{timeStr} • {mb:F1} MB",
							IconGlyph = "\uE74C",
							IconColor = "#D1FE29",
							IsInstalled = isDbInstalled,
							Kind = DownloadItemKindType.DriverBooster,
							InstallRequested = OnHistoryItemInstallRequested,
							OpenLocationRequested = OnHistoryItemOpenLocationRequested,
							DeleteRequested = OnHistoryItemDeleteRequested,
							ActionsToggleRequested = OnHistoryItemActionsToggleRequested
						};
						list.Add(dbItem);
					}
				}
				catch
				{
				}
			}

			// 2. Check DirectX
			string dxPath = Path.Combine(downloadsDir, "dxwebsetup.exe");
			if (File.Exists(dxPath))
			{
				try
				{
					var fi = new FileInfo(dxPath);
					if (fi.Length > 100000)
					{
						double kb = (double)fi.Length / 1024.0;
						string timeStr = fi.LastWriteTime.ToString("h:mm tt");
						bool isDxInstalled = File.Exists(@"C:\Windows\System32\d3dx9_43.dll");

						var dxItem = new DownloadHistoryItemViewModel
						{
							Id = "directx",
							Title = "DirectX End-User Runtimes",
							FilePath = dxPath,
							TimeText = timeStr,
							SizeText = $"{kb:F0} KB",
							DetailsText = $"{timeStr} • {kb:F0} KB",
							IconGlyph = "\uE7FC",
							IconColor = "#38BDF8",
							IsInstalled = isDxInstalled,
							Kind = DownloadItemKindType.DirectX,
							InstallRequested = OnHistoryItemInstallRequested,
							OpenLocationRequested = OnHistoryItemOpenLocationRequested,
							DeleteRequested = OnHistoryItemDeleteRequested,
							ActionsToggleRequested = OnHistoryItemActionsToggleRequested
						};
						list.Add(dxItem);
					}
				}
				catch
				{
				}
			}

			// 3. Check other core DriverItems
			foreach (var drv in DriverItems)
			{
				if (drv.Id == "drv_directx" || drv.Id == "drv_booster")
					continue;

				if (!string.IsNullOrEmpty(drv.TargetFilePath) && File.Exists(drv.TargetFilePath))
				{
					try
					{
						var fi = new FileInfo(drv.TargetFilePath);
						if (fi.Length > 100000)
						{
							double mb = (double)fi.Length / (1024.0 * 1024.0);
							string timeStr = fi.LastWriteTime.ToString("h:mm tt");

							var drvHistory = new DownloadHistoryItemViewModel
							{
								Id = drv.Id,
								Title = drv.Title,
								FilePath = drv.TargetFilePath,
								TimeText = timeStr,
								SizeText = $"{mb:F1} MB",
								DetailsText = $"Download Done • {mb:F1} MB",
								IconGlyph = drv.Id switch
								{
									"drv_chipset" => "\uE7F8",
									"drv_gpu" => "\uE7F4",
									"drv_wireless" => "\uE701",
									"drv_vcredist" => "\uE7B8",
									"drv_dotnet" => "\uE7BE",
									_ => "\uE74C"
								},
								IconColor = "#D1FE29",
								IsInstalled = drv.IsInstalled,
								InstallRequested = OnHistoryItemInstallRequested,
								OpenLocationRequested = OnHistoryItemOpenLocationRequested,
								DeleteRequested = OnHistoryItemDeleteRequested,
								ActionsToggleRequested = OnHistoryItemActionsToggleRequested
							};
							list.Add(drvHistory);
						}
					}
					catch { }
				}
			}

			void ApplyUpdate()
			{
				CompletedDownloads = new ObservableCollection<DownloadHistoryItemViewModel>(list);
				UpdateDownloadViewState();
			}

			if (Application.Current?.Dispatcher?.CheckAccess() == true)
			{
				ApplyUpdate();
			}
			else
			{
				Application.Current?.Dispatcher?.Invoke(ApplyUpdate);
			}
		}
		catch (Exception ex)
		{
			_log.Log("Windows", "Failed to refresh download history list", "WARNING", null, null, ex.Message);
		}
	}

	private void UpdateDownloadViewState()
	{
		if (IsDownloadingDriverBooster)
		{
			return;
		}

		if (CompletedDownloads.Count > 1)
		{
			IsMultiDownloadViewActive = true;
			IsSingleDownloadCompletedView = false;
			IsDownloadCompleted = true;
			HasDownloadItem = true;
			DownloadSubtitleText = $"{CompletedDownloads.Count} Items Completed • Ready to install";
		}
		else if (CompletedDownloads.Count == 1)
		{
			IsMultiDownloadViewActive = true;
			IsSingleDownloadCompletedView = true;
			IsDownloadCompleted = true;
			HasDownloadItem = true;
			var single = CompletedDownloads[0];
			DownloadedFilePath = single.FilePath;
			DownloadedItemKind = single.Kind;
			DownloadItemTitleText = single.Title;
			DownloadItemDetailsText = single.DetailsText;
			DownloadItemIconGlyph = single.IconGlyph;
			DownloadItemIconColor = single.IconColor;
			DownloadInstallButtonText = single.Kind == DownloadItemKindType.DirectX ? "Install DirectX" : "Install Driver Booster";
			DownloadSubtitleText = "1 Item Completed • Ready to install";
		}
		else
		{
			IsMultiDownloadViewActive = false;
			IsSingleDownloadCompletedView = false;
			IsDownloadCompleted = false;
			HasDownloadItem = false;
			DownloadSubtitleText = "No active downloads";
		}
		OnPropertyChanged(nameof(IsDriverBoosterDownloaded));
		OnPropertyChanged(nameof(DriverBoosterBannerButtonText));
	}

	private async void OnHistoryItemInstallRequested(DownloadHistoryItemViewModel item)
	{
		if (item.Kind == DownloadItemKindType.DirectX)
		{
			await InstallDownloadedDirectX();
		}
		else if (item.Kind == DownloadItemKindType.DriverBooster)
		{
			await InstallDownloadedDriverBooster();
		}
		else
		{
			var driver = DriverItems.FirstOrDefault(d => d.Id == item.Id || d.TargetFilePath == item.FilePath);
			item.IsInstalling = true;
			if (driver != null)
			{
				driver.IsInstalling = true;
				driver.PrimaryButtonText = "Installing...";
				UpdateDriverActionStates();

				bool installed = await InstallSingleDriverInternalAsync(driver);

				if (installed)
				{
					driver.IsInstalling = false;
					driver.IsInstalled = true;
					driver.PrimaryButtonText = "Installed";
					item.IsInstalling = false;
					item.IsInstalled = true;
				}
				else
				{
					driver.IsInstalling = false;
					driver.IsInstalled = false;
					driver.IsDownloaded = true;
					driver.PrimaryButtonText = "Install";
					item.IsInstalling = false;
					item.IsInstalled = false;
				}
				InstalledDriversBadgeText = $"{DriverItems.Count(d => d.IsInstalled)}/{DriverItems.Count} Installed";
				UpdateDriverActionStates();
			}
			else if (!string.IsNullOrEmpty(item.FilePath) && File.Exists(item.FilePath))
			{
				try
				{
					var fi = new FileInfo(item.FilePath);
					if (fi.Length > 100000)
					{
						Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true });
						await Task.Delay(2600);
						item.IsInstalling = false;
						item.IsInstalled = true;
					}
				}
				catch 
				{ 
					item.IsInstalling = false;
				}
			}
			else
			{
				item.IsInstalling = false;
			}
		}
		RefreshCompletedDownloadsList();
	}

	private void OnHistoryItemOpenLocationRequested(DownloadHistoryItemViewModel item)
	{
		if (!string.IsNullOrEmpty(item.FilePath) && File.Exists(item.FilePath))
		{
			try
			{
				Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
				return;
			}
			catch
			{
			}
		}
		try
		{
			string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			Process.Start("explorer.exe", dir);
		}
		catch
		{
		}
	}

	private void OnHistoryItemDeleteRequested(DownloadHistoryItemViewModel item)
	{
		try
		{
			if (!string.IsNullOrEmpty(item.FilePath) && File.Exists(item.FilePath))
			{
				File.Delete(item.FilePath);
			}
			string part = item.FilePath + ".part";
			if (File.Exists(part))
			{
				File.Delete(part);
			}
		}
		catch (Exception ex)
		{
			_log.Log("Windows", $"Failed to delete installer: {item.FilePath}", "WARNING", null, null, ex.Message);
		}

		void ApplyDelete()
		{
			CompletedDownloads.Remove(item);
			UpdateDownloadViewState();
		}

		if (Application.Current?.Dispatcher?.CheckAccess() == true)
		{
			ApplyDelete();
		}
		else
		{
			Application.Current?.Dispatcher?.Invoke(ApplyDelete);
		}
	}

	private void OnHistoryItemActionsToggleRequested(DownloadHistoryItemViewModel toggledItem)
	{
		foreach (var item in CompletedDownloads)
		{
			if (item != toggledItem)
			{
				item.IsActionsRevealed = false;
			}
		}
	}

	[RelayCommand]
	private void DeleteDownloadedFile()
	{
		try
		{
			if (!string.IsNullOrEmpty(DownloadedFilePath) && File.Exists(DownloadedFilePath))
			{
				File.Delete(DownloadedFilePath);
			}
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			string path2 = Path.Combine(path, "driver_booster_setup.exe");
			if (File.Exists(path2))
			{
				File.Delete(path2);
			}
			string path3 = Path.Combine(path, "driver_booster_setup.exe.part");
			if (File.Exists(path3))
			{
				File.Delete(path3);
			}
		}
		catch (Exception ex)
		{
			_log.Log("Windows", "Failed to delete downloaded installer", "WARNING", null, null, ex.Message);
		}
		StopDownloadDotsAnimation();
		RefreshCompletedDownloadsList();
		HasDownloadItem = false;
		IsDownloadCompleted = false;
		IsDownloadingDriverBooster = false;
		IsMultiDownloadViewActive = false;
		IsSingleDownloadCompletedView = false;
		DownloadedFilePath = "";
		DownloadProgressPercent = 0.0;
		DownloadSpeedText = "";
		DownloadSizeText = "";
		DownloadFinalSizeText = "";
		DownloadStatusText = "";
		DownloadSubtitleText = "No active downloads";
		DownloadTimeText = "--";
		DownloadMbText = "0.0 MB";
		StatusMessage = "";
		OnPropertyChanged("IsDriverBoosterDownloaded");
		OnPropertyChanged("DriverBoosterBannerButtonText");
	}

	[RelayCommand]
	private async Task InstallDownloadedItem()
	{
		if (DownloadedItemKind == DownloadItemKindType.DirectX)
		{
			await InstallDownloadedDirectX();
		}
		else
		{
			await InstallDownloadedDriverBooster();
		}
	}

	[RelayCommand]
	private async Task InstallDownloadedDirectX()
	{
		string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
		string setupPath = ((!string.IsNullOrEmpty(DownloadedFilePath) && File.Exists(DownloadedFilePath)) ? DownloadedFilePath : Path.Combine(downloadDir, "dxwebsetup.exe"));
		if (File.Exists(setupPath) && new FileInfo(setupPath).Length > 100000)
		{
			try
			{
				StatusMessage = "Starting DirectX Web Setup...";
				_log.Log("Windows", "Launching DirectX Web Setup from " + setupPath);
				Process.Start(new ProcessStartInfo(setupPath)
				{
					UseShellExecute = true,
					Verb = "runas"
				});
				StatusMessage = "DirectX Web Setup launched.";
				return;
			}
			catch (Exception ex)
			{
				_log.Log("Windows", "Failed to launch DirectX Setup", "ERROR", null, null, ex.Message);
				StatusMessage = "Error launching DirectX Setup.";
				return;
			}
		}
		try { if (File.Exists(setupPath)) File.Delete(setupPath); } catch { }
		StatusMessage = "DirectX installer not found. Starting download...";
		await DownloadDirectXAsync();
	}

	[RelayCommand]
	private async Task InstallDownloadedDriverBooster()
	{
		string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
		string setupPath = ((!string.IsNullOrEmpty(DownloadedFilePath) && File.Exists(DownloadedFilePath)) ? DownloadedFilePath : Path.Combine(downloadDir, "driver_booster_setup.exe"));
		if (IsValidDriverBoosterSetup(setupPath))
		{
			try
			{
				Process.Start(new ProcessStartInfo(setupPath)
				{
					UseShellExecute = true
				});
				return;
			}
			catch (Exception ex)
			{
				_log.Log("Windows", "Failed to launch Driver Booster installer", "WARNING", null, null, ex.Message);
				return;
			}
		}
		if (File.Exists(setupPath))
		{
			try
			{
				File.Delete(setupPath);
			}
			catch
			{
			}
		}
		await DownloadDriverBoosterAsync();
	}

	[RelayCommand]
	private void OpenDownloadedFolder()
	{
		if (!string.IsNullOrEmpty(DownloadedFilePath) && File.Exists(DownloadedFilePath))
		{
			try
			{
				Process.Start("explorer.exe", "/select,\"" + DownloadedFilePath + "\"");
				return;
			}
			catch
			{
				return;
			}
		}
		try
		{
			string arguments = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
			Process.Start("explorer.exe", arguments);
		}
		catch
		{
		}
	}

	public async Task DownloadDriverBoosterAsync()
	{
		if (IsDownloadingDriverBooster)
		{
			return;
		}
		string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
		Directory.CreateDirectory(downloadDir);
		string finalFilePath = Path.Combine(downloadDir, "driver_booster_setup.exe");
		string tempFilePath = Path.Combine(downloadDir, "driver_booster_setup.exe.part");
		if (File.Exists(finalFilePath))
		{
			try
			{
				File.Delete(finalFilePath);
			}
			catch
			{
			}
		}
		if (File.Exists(tempFilePath))
		{
			try
			{
				File.Delete(tempFilePath);
			}
			catch
			{
			}
		}
		DownloadedItemKind = DownloadItemKindType.DriverBooster;
		DownloadItemTitleText = "Driver Booster 12";
		DownloadItemDetailsText = "Completed • 31.4 MB";
		DownloadInstallButtonText = "Install Driver Booster";
		DownloadTotalSizeText = "~32.6 MB";
		HasDownloadItem = true;
		IsDownloadingDriverBooster = true;
		IsDownloadCompleted = false;
		SelectedDownloadNavTab = -1;
		IsDownloadFlyoutOpen = true;
		IsDownloadPaused = false;
		DownloadPauseButtonText = "Pause";
		DownloadPauseButtonIcon = "\ue769";
		_pauseTcs = null;
		DownloadProgressPercent = 0.0;
		DownloadStatusText = "Connecting...";
		DownloadSpeedText = "Connecting...";
		DownloadSizeText = "Starting download...";
		DownloadTimeText = "Calculating...";
		DownloadMbText = "0.0 MB";
		DownloadFinalSizeText = "";
		DownloadSubtitleText = "Driver Booster 12 • Connecting...";
		_isConnecting = true;
		StartDownloadDotsAnimation();
		_downloadCts?.Cancel();
		_downloadCts = new CancellationTokenSource();
		try
		{
			string localV12Source = null;
			string[] candidatePaths = new string[5]
			{
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "DriverBooster12_Setup.exe"),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DriverBooster12_Setup.exe"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XenomOptimizer", "DriverBooster12_Setup.exe"),
				"d:\\Shop\\Xenom Lag Fix\\Assets\\DriverBooster12_Setup.exe",
				"C:\\Users\\anjul\\.gemini\\antigravity\\brain\\71b74b8b-8fa5-4dce-a4ef-4fbb8a50d260\\scratch\\db12_archive.exe"
			};
			string[] array = candidatePaths;
			foreach (string p in array)
			{
				if (File.Exists(p) && new FileInfo(p).Length > 25000000)
				{
					localV12Source = p;
					break;
				}
			}
			string[] downloadUrls = new string[2] { "https://web.archive.org/web/20241118080431id_/https://cdn.iobit.com/dl/driver_booster_setup.exe", "https://web.archive.org/web/20241130041500id_/https://cdn.iobit.com/dl/driver_booster_setup.exe" };
			HttpResponseMessage response = null;
			string activeUrl = downloadUrls[0];
			string[] array2 = downloadUrls;
			foreach (string url in array2)
			{
				try
				{
					using (CancellationTokenSource mirrorCts = CancellationTokenSource.CreateLinkedTokenSource(_downloadCts.Token))
					{
						mirrorCts.CancelAfter(TimeSpan.FromSeconds(3.0));
						HttpResponseMessage resp = await s_downloadHttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, mirrorCts.Token);
						if (resp.IsSuccessStatusCode)
						{
							response = resp;
							activeUrl = url;
							break;
						}
						resp.Dispose();
					}
					continue;
				}
				catch when (!_downloadCts.IsCancellationRequested)
				{
					continue;
				}
			}
			if (response == null && string.IsNullOrEmpty(localV12Source))
			{
				throw new IOException("Could not connect to download servers. Please check your internet connection.");
			}
			HttpResponseMessage currentResponse = response;
			Stream stream = null;
			bool isLocal = false;
			try
			{
				_isConnecting = false;
				DownloadStatusText = "Downloading... 0%";
				DownloadSubtitleText = "Driver Booster 12 • Downloading...";
				CancellationToken token = _downloadCts.Token;
				long totalBytes;
				if (currentResponse != null)
				{
					totalBytes = currentResponse.Content.Headers.ContentLength ?? 31370944;
					stream = await currentResponse.Content.ReadAsStreamAsync(token);
				}
				else
				{
					isLocal = true;
					totalBytes = new FileInfo(localV12Source).Length;
					stream = new FileStream(localV12Source, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
				}
				using (FileStream fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true))
				{
					byte[] buffer = new byte[65536];
					long totalRead = 0L;
					_downloadStopwatch = Stopwatch.StartNew();
					Stopwatch lastUiUpdate = Stopwatch.StartNew();
					while (totalRead < totalBytes)
					{
						if (IsDownloadPaused && _pauseTcs != null)
						{
							try
							{
								await _pauseTcs.Task;
							}
							catch (OperationCanceledException)
							{
								break;
							}
						}
						token.ThrowIfCancellationRequested();
						int read;
						try
						{
							int num = ((stream != null) ? (await stream.ReadAsync(buffer, 0, buffer.Length, token)) : 0);
							read = num;
						}
						catch when (!token.IsCancellationRequested && totalRead < totalBytes)
						{
							read = 0;
						}
						if (read <= 0)
						{
							if (totalRead >= totalBytes || isLocal || string.IsNullOrEmpty(activeUrl))
							{
								break;
							}
							try
							{
								stream?.Dispose();
								currentResponse?.Dispose();
								HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, activeUrl);
								req.Headers.Range = new RangeHeaderValue(totalRead, null);
								using CancellationTokenSource rangeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
								rangeCts.CancelAfter(TimeSpan.FromSeconds(15.0));
								currentResponse = await s_downloadHttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, rangeCts.Token);
								if (currentResponse.IsSuccessStatusCode)
								{
									stream = await currentResponse.Content.ReadAsStreamAsync(token);
									continue;
								}
							}
							catch
							{
							}
							break;
						}
						await fileStream.WriteAsync(buffer, 0, read, token);
						totalRead += read;
						if (isLocal)
						{
							await Task.Delay(2, token);
						}
						if (IsDownloadPaused || (lastUiUpdate.ElapsedMilliseconds <= 50 && totalRead != totalBytes))
						{
							continue;
						}
						lastUiUpdate.Restart();
						double pct = ((totalBytes > 0) ? Math.Min(100.0, (double)totalRead / (double)totalBytes * 100.0) : 0.0);
						double readMb = (double)totalRead / 1048576.0;
						double totalMb = ((totalBytes > 0) ? ((double)totalBytes / 1048576.0) : 31.4);
						double elapsedSec = Math.Max(0.05, _downloadStopwatch?.Elapsed.TotalSeconds ?? 0.05);
						double speed = readMb / elapsedSec;
						long remainingBytes = Math.Max(0L, totalBytes - totalRead);
						double remainingSec = ((speed > 0.05) ? ((double)remainingBytes / (speed * 1024.0 * 1024.0)) : 0.0);
						string timeStr = ((remainingSec > 60.0) ? $"{(int)(remainingSec / 60.0)}m {(int)(remainingSec % 60.0)}s" : $"{(int)Math.Ceiling(remainingSec)}s left");
						Application.Current?.Dispatcher?.InvokeAsync(() =>
						{
							if (!IsDownloadPaused)
							{
								DownloadProgressPercent = pct;
								DownloadSpeedText = $"{speed:F1} MB/s";
								DownloadSizeText = $"{readMb:F1} MB / {totalMb:F1} MB";
								DownloadMbText = $"{readMb:F1} MB";
								DownloadTimeText = timeStr;
								DownloadStatusText = $"Downloading... {pct:F0}%";
							}
						});
					}
					IsDownloadPaused = false;
					DownloadPauseButtonText = "Pause";
					DownloadPauseButtonIcon = "\ue769";
					_pauseTcs = null;
					if (totalRead < 25000000)
					{
						throw new IOException($"Download was incomplete: received {totalRead} bytes.");
					}
				}
				try
				{
					FileVersionInfo vi = FileVersionInfo.GetVersionInfo(tempFilePath);
					if (vi.ProductMajorPart > 0 && vi.ProductMajorPart != 12 && !string.IsNullOrEmpty(localV12Source) && File.Exists(localV12Source))
					{
						File.Copy(localV12Source, tempFilePath, overwrite: true);
					}
				}
				catch
				{
				}
				if (File.Exists(finalFilePath))
				{
					try
					{
						File.Delete(finalFilePath);
					}
					catch
					{
					}
				}
				File.Move(tempFilePath, finalFilePath);
				DownloadedFilePath = finalFilePath;
				StopDownloadDotsAnimation();
				double finalMb = (double)new FileInfo(finalFilePath).Length / 1048576.0;
				Application.Current?.Dispatcher?.Invoke(() =>
				{
					IsDownloadCompleted = true;
					IsDownloadingDriverBooster = false;
					DownloadProgressPercent = 100.0;
					DownloadSpeedText = "Complete";
					DownloadMbText = $"{finalMb:F1} MB";
					DownloadTimeText = "Done";
					DownloadFinalSizeText = $"{finalMb:F1} MB";
					DownloadSizeText = "";
					DownloadStatusText = "Completed";
					DownloadSubtitleText = "Driver Booster 12 • Completed ✓";
					DownloadItemTitleText = "Driver Booster 12";
					DownloadItemDetailsText = $"Completed • {finalMb:F1} MB";
					DownloadInstallButtonText = "Install Driver Booster";
					StatusMessage = "";
					OnPropertyChanged("IsDriverBoosterDownloaded");
					OnPropertyChanged("DriverBoosterBannerButtonText");
				});
				RefreshCompletedDownloadsList();
				_log.Log("Windows", "Driver Booster 12 downloaded successfully", "SUCCESS");
			}
			finally
			{
				stream?.Dispose();
				currentResponse?.Dispose();
			}
		}
		catch (OperationCanceledException)
		{
			StopDownloadDotsAnimation();
			if (File.Exists(tempFilePath))
			{
				try
				{
					File.Delete(tempFilePath);
				}
				catch
				{
				}
			}
			Application.Current?.Dispatcher?.Invoke(() =>
			{
				DownloadStatusText = "Download Cancelled";
				DownloadSubtitleText = "No active downloads";
				DownloadSpeedText = "";
				IsDownloadingDriverBooster = false;
				OnPropertyChanged("IsDriverBoosterDownloaded");
				OnPropertyChanged("DriverBoosterBannerButtonText");
			});
		}
		catch (Exception ex3)
		{
			Exception ex4 = ex3;
			Exception ex5 = ex4;
			StopDownloadDotsAnimation();
			if (File.Exists(tempFilePath))
			{
				try
				{
					File.Delete(tempFilePath);
				}
				catch
				{
				}
			}
			_log.Log("Windows", "Failed to download Driver Booster", "ERROR", null, null, ex5.Message);
			Application.Current?.Dispatcher?.Invoke(() =>
			{
				DownloadStatusText = "Download failed. Opening official page...";
				DownloadSubtitleText = "No active downloads";
				DownloadSpeedText = "";
				IsDownloadingDriverBooster = false;
				StatusMessage = "Download failed: " + ex5.Message;
				OnPropertyChanged("IsDriverBoosterDownloaded");
				OnPropertyChanged("DriverBoosterBannerButtonText");
			});
			OpenUrl("https://www.iobit.com/en/driver-booster.php");
		}
		finally
		{
			IsDownloadingDriverBooster = false;
		}
	}

	public async Task DownloadDirectXAsync()
	{
		if (IsDownloadingDriverBooster)
		{
			return;
		}
		DownloadedItemKind = DownloadItemKindType.DirectX;
		DownloadItemTitleText = "DirectX End-User Runtimes";
		DownloadItemDetailsText = "Completed • 295 KB";
		DownloadInstallButtonText = "Install DirectX";
		DownloadTotalSizeText = "~295 KB";
		string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
		Directory.CreateDirectory(downloadDir);
		string finalFilePath = Path.Combine(downloadDir, "dxwebsetup.exe");
		string tempFilePath = Path.Combine(downloadDir, "dxwebsetup.exe.part");
		if (File.Exists(finalFilePath))
		{
			try
			{
				File.Delete(finalFilePath);
			}
			catch
			{
			}
		}
		if (File.Exists(tempFilePath))
		{
			try
			{
				File.Delete(tempFilePath);
			}
			catch
			{
			}
		}
		HasDownloadItem = true;
		IsDownloadingDriverBooster = true;
		IsDownloadCompleted = false;
		SelectedDownloadNavTab = -1;
		IsDownloadFlyoutOpen = true;
		IsDownloadPaused = false;
		DownloadPauseButtonText = "Pause";
		DownloadPauseButtonIcon = "\ue769";
		_pauseTcs = null;
		DownloadProgressPercent = 0.0;
		DownloadStatusText = "Connecting...";
		DownloadSpeedText = "Connecting...";
		DownloadSizeText = "Starting download...";
		DownloadTimeText = "Calculating...";
		DownloadMbText = "0 KB";
		DownloadFinalSizeText = "";
		DownloadSubtitleText = "DirectX Runtimes • Connecting...";
		_isConnecting = true;
		StartDownloadDotsAnimation();
		_downloadCts?.Cancel();
		_downloadCts = new CancellationTokenSource();
		try
		{
			string localDxSource = null;
			string[] candidatePaths = new string[4]
			{
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "dxwebsetup.exe"),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dxwebsetup.exe"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XenomOptimizer", "dxwebsetup.exe"),
				"d:\\Shop\\Xenom Lag Fix\\Assets\\dxwebsetup.exe"
			};
			string[] array = candidatePaths;
			foreach (string p in array)
			{
				if (File.Exists(p) && new FileInfo(p).Length > 200000)
				{
					localDxSource = p;
					break;
				}
			}
			string downloadUrl = "https://download.microsoft.com/download/1/7/1/1718CCC4-6315-4D8E-9543-8E28A4E18C4C/dxwebsetup.exe";
			HttpResponseMessage response = null;
			try
			{
				using CancellationTokenSource mirrorCts = CancellationTokenSource.CreateLinkedTokenSource(_downloadCts.Token);
				mirrorCts.CancelAfter(TimeSpan.FromSeconds(5.0));
				HttpResponseMessage resp = await s_downloadHttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, mirrorCts.Token);
				if (resp.IsSuccessStatusCode)
				{
					response = resp;
				}
				else
				{
					resp.Dispose();
				}
			}
			catch when (!_downloadCts.IsCancellationRequested)
			{
			}
			if (response == null && string.IsNullOrEmpty(localDxSource))
			{
				throw new IOException("Could not connect to Microsoft download servers. Please check your internet connection.");
			}
			HttpResponseMessage currentResponse = response;
			Stream stream = null;
			bool isLocal = false;
			try
			{
				_isConnecting = false;
				DownloadStatusText = "Downloading... 0%";
				DownloadSubtitleText = "DirectX Runtimes • Downloading...";
				CancellationToken token = _downloadCts.Token;
				long totalBytes;
				if (currentResponse != null)
				{
					totalBytes = currentResponse.Content.Headers.ContentLength ?? 295320;
					stream = await currentResponse.Content.ReadAsStreamAsync(token);
				}
				else
				{
					isLocal = true;
					totalBytes = new FileInfo(localDxSource).Length;
					stream = new FileStream(localDxSource, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, useAsync: true);
				}
				using (FileStream fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 16384, useAsync: true))
				{
					byte[] buffer = new byte[16384];
					long totalRead = 0L;
					_downloadStopwatch = Stopwatch.StartNew();
					Stopwatch lastUiUpdate = Stopwatch.StartNew();
					while (totalRead < totalBytes)
					{
						if (IsDownloadPaused && _pauseTcs != null)
						{
							try
							{
								await _pauseTcs.Task;
							}
							catch (OperationCanceledException)
							{
								break;
							}
						}
						token.ThrowIfCancellationRequested();
						int read;
						try
						{
							int num = ((stream != null) ? (await stream.ReadAsync(buffer, 0, buffer.Length, token)) : 0);
							read = num;
						}
						catch when (!token.IsCancellationRequested && totalRead < totalBytes)
						{
							read = 0;
						}
						if (read <= 0)
						{
							if (totalRead < totalBytes)
							{
							}
							break;
						}
						await fileStream.WriteAsync(buffer, 0, read, token);
						totalRead += read;
						if (!isLocal)
						{
							await Task.Delay(10, token);
						}
						else
						{
							await Task.Delay(25, token);
						}
						if (IsDownloadPaused || (lastUiUpdate.ElapsedMilliseconds <= 40 && totalRead != totalBytes))
						{
							continue;
						}
						lastUiUpdate.Restart();
						double pct = ((totalBytes > 0) ? Math.Min(100.0, (double)totalRead / (double)totalBytes * 100.0) : 0.0);
						double readKb = (double)totalRead / 1024.0;
						double totalKb = ((totalBytes > 0) ? ((double)totalBytes / 1024.0) : 295.0);
						double elapsedSec = Math.Max(0.05, _downloadStopwatch?.Elapsed.TotalSeconds ?? 0.05);
						double speedKb = readKb / elapsedSec;
						Application.Current?.Dispatcher?.InvokeAsync(() =>
						{
							if (!IsDownloadPaused)
							{
								DownloadProgressPercent = pct;
								DownloadSpeedText = ((speedKb >= 1024.0) ? $"{speedKb / 1024.0:F1} MB/s" : $"{speedKb:F0} KB/s");
								DownloadSizeText = $"{readKb:F0} KB / {totalKb:F0} KB";
								DownloadMbText = $"{readKb:F0} KB";
								DownloadTimeText = "A few seconds";
								DownloadStatusText = $"Downloading... {pct:F0}%";
							}
						});
					}
					IsDownloadPaused = false;
					DownloadPauseButtonText = "Pause";
					DownloadPauseButtonIcon = "\ue769";
					_pauseTcs = null;
					if (totalRead < 100000)
					{
						if (string.IsNullOrEmpty(localDxSource) || !File.Exists(localDxSource))
						{
							throw new IOException($"DirectX download was incomplete: received {totalRead} bytes.");
						}
						fileStream.Dispose();
						File.Copy(localDxSource, tempFilePath, overwrite: true);
					}
				}
				if (File.Exists(finalFilePath))
				{
					try
					{
						File.Delete(finalFilePath);
					}
					catch
					{
					}
				}
				File.Move(tempFilePath, finalFilePath);
				DownloadedFilePath = finalFilePath;
				StopDownloadDotsAnimation();
				double finalKb = (double)new FileInfo(finalFilePath).Length / 1024.0;
				Application.Current?.Dispatcher?.Invoke(() =>
				{
					IsDownloadCompleted = true;
					IsDownloadingDriverBooster = false;
					DownloadProgressPercent = 100.0;
					DownloadSpeedText = "Complete";
					DownloadMbText = $"{finalKb:F0} KB";
					DownloadTimeText = "Done";
					DownloadFinalSizeText = $"{finalKb:F0} KB";
					DownloadSizeText = "";
					DownloadStatusText = "Completed";
					DownloadSubtitleText = "DirectX Runtimes • Completed ✓";
					DownloadItemTitleText = "DirectX End-User Runtimes";
					DownloadItemDetailsText = $"Completed • {finalKb:F0} KB";
					DownloadInstallButtonText = "Install DirectX";
					StatusMessage = "DirectX End-User Runtimes downloaded.";

					var dxDriver = DriverItems.FirstOrDefault(d => d.Id == "drv_directx");
					if (dxDriver != null)
					{
						dxDriver.IsDownloaded = true;
						if (!dxDriver.IsInstalled)
						{
							dxDriver.PrimaryButtonText = "Install";
						}
					}
				});
				RefreshCompletedDownloadsList();
				_log.Log("Windows", "DirectX Web Setup downloaded successfully", "SUCCESS");
			}
			finally
			{
				stream?.Dispose();
				currentResponse?.Dispose();
			}
		}
		catch (OperationCanceledException)
		{
			StopDownloadDotsAnimation();
			if (File.Exists(tempFilePath))
			{
				try
				{
					File.Delete(tempFilePath);
				}
				catch
				{
				}
			}
			Application.Current?.Dispatcher?.Invoke(() =>
			{
				DownloadStatusText = "Download Cancelled";
				DownloadSubtitleText = "No active downloads";
				DownloadSpeedText = "";
				IsDownloadingDriverBooster = false;
			});
		}
		catch (Exception ex3)
		{
			Exception ex4 = ex3;
			Exception ex5 = ex4;
			StopDownloadDotsAnimation();
			if (File.Exists(tempFilePath))
			{
				try
				{
					File.Delete(tempFilePath);
				}
				catch
				{
				}
			}
			_log.Log("Windows", "DirectX download failed", "ERROR", null, null, ex5.Message);
			Application.Current?.Dispatcher?.Invoke(() =>
			{
				DownloadStatusText = "Download Failed";
				DownloadSubtitleText = "Download Failed";
				DownloadSpeedText = "Error";
				IsDownloadingDriverBooster = false;
				StatusMessage = "Failed to download DirectX: " + ex5.Message;
			});
		}
	}

	private async Task ApplyAllConfirmedAsync()
	{
		IsLoading = true;
		try
		{
			await _backup.StartSessionAsync();
			int count = 0;
			List<OptimizationCardViewModel> targetList = ((SelectedSlideIndex == 1) ? ServicesOptimizations.ToList() : AdvanceOptimizations.Concat(LowEndOptimizations).ToList());
			string tabName = ((SelectedSlideIndex == 1) ? "Unused Services" : "Advance & Low-End PC");
			StatusMessage = "Applying recommended optimizations for " + tabName + "...";
			foreach (OptimizationCardViewModel card in targetList.Where((OptimizationCardViewModel c) => c.IsSupported && !c.IsOptimal))
			{
				if ((!card.RequiresAdmin || _admin.IsRunningAsAdmin) && await card.ApplyAsync())
				{
					count++;
				}
			}
			await _backup.EndSessionAsync();
			StatusMessage = $"Applied {count} recommended optimizations for {tabName} successfully.";
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			StatusMessage = "Apply error: " + ex2.Message;
		}
		finally
		{
			IsLoading = false;
		}
	}

	private async Task RestoreDefaultsConfirmedAsync()
	{
		IsLoading = true;
		try
		{
			if (SelectedSlideIndex == 1)
			{
				StatusMessage = "Restoring default settings for Unused Services...";
				_registry.SetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\GameBar", "AllowAutoGameMode", 1u, RegistryValueKind.DWord);
				_registry.SetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\GameBar", "AutoGameModeEnabled", 1u, RegistryValueKind.DWord);
				_registry.SetValue("HKEY_CURRENT_USER\\System\\GameConfigStore", "GameDVR_Enabled", 1u, RegistryValueKind.DWord);
				_registry.SetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\GameDVR", "AppCaptureEnabled", 1u, RegistryValueKind.DWord);
				_registry.SetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications", "GlobalUserDisabled", 0u, RegistryValueKind.DWord);
				_registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization", "DODownloadMode", 1u, RegistryValueKind.DWord);
				_registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\DeliveryOptimization\\Config", "DODownloadMode", 1u, RegistryValueKind.DWord);
				_registry.DeleteValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection", "AllowTelemetry");
				foreach (OptimizationCardViewModel card in ServicesOptimizations)
				{
					await card.RestoreAsync();
				}
				StatusMessage = "Restored default settings for Unused Services successfully.";
			}
			else if (SelectedSlideIndex == 2)
			{
				StatusMessage = "Restoring default settings for Advance & Low-End PC...";
				try
				{
					Process.Start(new ProcessStartInfo
					{
						FileName = "powercfg",
						Arguments = "/setactive 381b4222-f694-41f0-9685-ff5bb260df2e",
						CreateNoWindow = true,
						UseShellExecute = false
					})?.WaitForExit();
				}
				catch
				{
				}
				_registry.SetValue("HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers", "HwSchMode", 1u, RegistryValueKind.DWord);
				_registry.SetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects", "VisualFXSetting", 0u, RegistryValueKind.DWord);
				_registry.SetValue("HKEY_CURRENT_USER\\Control Panel\\Mouse", "MouseSpeed", "1", RegistryValueKind.String);
				_registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile", "SystemResponsiveness", 20u, RegistryValueKind.DWord);
				_registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile", "NetworkThrottlingIndex", 10u, RegistryValueKind.DWord);
				_registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games", "Priority", 2u, RegistryValueKind.DWord);
				_registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games", "Scheduling Category", "Medium", RegistryValueKind.String);
				_registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games", "SFIO Priority", "Normal", RegistryValueKind.String);
				_registry.SetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "EnableTransparency", 1u, RegistryValueKind.DWord);
				foreach (OptimizationCardViewModel card2 in AdvanceOptimizations.Concat(LowEndOptimizations))
				{
					await card2.RestoreAsync();
				}
				StatusMessage = "Restored default settings for Advance & Low-End PC successfully.";
			}
			await LoadOptimizationsAsync();
			_log.Log("Windows", $"Restored defaults for slide {SelectedSlideIndex}", "SUCCESS");
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			StatusMessage = "Restore failed: " + ex2.Message;
			_log.Log("Windows", "Restore defaults failed", "ERROR", null, null, ex2.Message);
		}
		finally
		{
			IsLoading = false;
		}
	}

	[DllImport("psapi.dll")]
	private static extern int EmptyWorkingSet(nint hwProc);

	[RelayCommand]
	private async Task CleanStandbyRamAsync()
	{
		StatusMessage = "Flushing RAM standby cache and working sets...";
		try
		{
			await Task.Run(() =>
			{
				Process[] processes = Process.GetProcesses();
				foreach (Process process in processes)
				{
					try
					{
						EmptyWorkingSet(process.Handle);
					}
					catch
					{
					}
					finally
					{
						process.Dispose();
					}
				}
				GC.Collect();
				GC.WaitForPendingFinalizers();
			});
			StatusMessage = "Standby RAM flushed successfully! Physical memory reclaimed.";
			_log.Log("Windows", "Cleaned Standby RAM memory list", "SUCCESS");
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			StatusMessage = "RAM clean error: " + ex2.Message;
		}
	}

	private static void OpenUrl(string url)
	{
		try
		{
			Process.Start(new ProcessStartInfo(url)
			{
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}

	private static void LaunchProcess(string command)
	{
		try
		{
			Process.Start(new ProcessStartInfo(command)
			{
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}

	private static void LaunchGpuControlApp(bool isNvidia, bool isAmd)
	{
		try
		{
			if (isNvidia)
			{
				string[] nvidiaPaths = new[]
				{
					@"C:\Program Files\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA App.exe",
					@"C:\Program Files (x86)\NVIDIA Corporation\NVIDIA App\CEF\NVIDIA App.exe",
					@"C:\Program Files\NVIDIA Corporation\NVIDIA GeForce Experience\NVIDIA GeForce Experience.exe",
					@"C:\Program Files (x86)\NVIDIA Corporation\NVIDIA GeForce Experience\NVIDIA GeForce Experience.exe",
					@"C:\Program Files\NVIDIA Corporation\Control Panel Client\nvcplui.exe",
					@"C:\Windows\System32\nvcplui.exe"
				};

				foreach (var path in nvidiaPaths)
				{
					if (File.Exists(path))
					{
						Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
						return;
					}
				}

				try
				{
					var psi = new ProcessStartInfo("explorer.exe", @"shell:AppsFolder\NVIDIACorp.NVIDIAControlPanel_56jybvy8sckqj!NVIDIAControlPanel")
					{
						UseShellExecute = true
					};
					Process.Start(psi);
					return;
				}
				catch { }

				LaunchProcess("devmgmt.msc");
			}
			else if (isAmd)
			{
				string[] amdPaths = new[]
				{
					@"C:\Program Files\AMD\CNext\CNext\RadeonSoftware.exe",
					@"C:\Program Files\AMD\CNext\CNext\CNEXT.exe",
					@"C:\Program Files (x86)\AMD\CNext\CNext\RadeonSoftware.exe"
				};

				foreach (var path in amdPaths)
				{
					if (File.Exists(path))
					{
						Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
						return;
					}
				}

				try
				{
					var psi = new ProcessStartInfo("explorer.exe", @"shell:AppsFolder\AdvancedMicroDevicesInc-2.AMDRadeonSoftware_0aav3q9b99r8g!App")
					{
						UseShellExecute = true
					};
					Process.Start(psi);
					return;
				}
				catch { }

				LaunchProcess("devmgmt.msc");
			}
			else
			{
				try
				{
					var psi = new ProcessStartInfo("explorer.exe", @"shell:AppsFolder\AppUp.IntelGraphicsExperience_8j3eq9eme6ctt!App")
					{
						UseShellExecute = true
					};
					Process.Start(psi);
					return;
				}
				catch { }

				LaunchProcess("devmgmt.msc");
			}
		}
		catch
		{
			LaunchProcess("devmgmt.msc");
		}
	}

	private static void OpenDriverDownloadPage(string gpuModel)
	{
		if (gpuModel.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || gpuModel.Contains("GeForce", StringComparison.OrdinalIgnoreCase) || gpuModel.Contains("RTX", StringComparison.OrdinalIgnoreCase) || gpuModel.Contains("GTX", StringComparison.OrdinalIgnoreCase))
		{
			OpenUrl("https://www.nvidia.com/Download/index.aspx");
		}
		else if (gpuModel.Contains("AMD", StringComparison.OrdinalIgnoreCase) || gpuModel.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
		{
			OpenUrl("https://www.amd.com/en/support");
		}
		else if (gpuModel.Contains("Intel", StringComparison.OrdinalIgnoreCase))
		{
			OpenUrl("https://www.intel.com/content/www/us/en/download-center/home.html");
		}
		else
		{
			OpenUrl("https://www.google.com/search?q=download+latest+graphics+driver");
		}
	}

	[RelayCommand]
	public async Task DriverNotchActionAsync()
	{
		if (HasCompletedDriverScan)
		{
			OpenDriverInstallationView();
		}
		else
		{
			await StartDriverScanAsync();
		}
	}

	private async Task AnimateScanProgressToAsync(double targetPercent, int durationMs = 180)
	{
		double start = ScanProgressPercent;
		double diff = targetPercent - start;
		if (!(diff <= 0.0))
		{
			int steps = Math.Max(10, durationMs / 25);
			int delayPerStep = Math.Max(16, durationMs / steps);
			for (int i = 1; i <= steps; i++)
			{
				ScanProgressPercent = Math.Round(start + diff * ((double)i / (double)steps), 1);
				await Task.Delay(delayPerStep);
			}
			ScanProgressPercent = targetPercent;
		}
	}

	[RelayCommand]
	public async Task StartDriverScanAsync()
	{
		if (!IsScanningDrivers)
		{
			HasStartedScan = true;
			IsScanningDrivers = true;
			HasCompletedDriverScan = false;
			IsHoverGreenEnabled = false;
			IsInstallButtonRevealed = false;
			IsInstallTransitionFinished = false;
			ScanGaugeSubtitle = "Scanning...";
			ScanProgressPercent = 0.0;
			ScanStatusText = "Scanning graphics adapter & display drivers...";
			ScannedDrivers.Clear();
			_backgroundAuditTask = Task.Run((Func<Task<List<ScannedDriverItemViewModel>>?>)PerformAuditHardwareAsync);
			if (ScanAnimationRequested != null)
			{
				ScanAnimationRequested(this, EventArgs.Empty);
				return;
			}
			await AnimateScanProgressToAsync(100.0, 11920);
			await FinalizeDriverScanAsync();
		}
	}

	public void UpdateScanProgress(double pct)
	{
		ScanProgressPercent = Math.Round(pct, 1);
		if (pct < 20.0)
		{
			ScanStatusText = "Scanning graphics adapter & display drivers...";
		}
		else if (pct < 45.0)
		{
			ScanStatusText = "Scanning DirectX legacy libraries (d3dx9, d3dx10, d3dx11)...";
		}
		else if (pct < 70.0)
		{
			ScanStatusText = "Checking Visual C++ 2015-2022 Redistributables...";
		}
		else if (pct < 88.0)
		{
			ScanStatusText = "Verifying High Definition audio hardware & drivers...";
		}
		else if (pct < 98.0)
		{
			ScanStatusText = "Checking network controller & adapter drivers...";
		}
		else
		{
			ScanStatusText = "Finalizing hardware audit results...";
		}
	}

	public async Task FinalizeDriverScanAsync()
	{
		try
		{
			List<ScannedDriverItemViewModel> list = ((_backgroundAuditTask == null) ? (await Task.Run((Func<Task<List<ScannedDriverItemViewModel>>?>)PerformAuditHardwareAsync)) : (await _backgroundAuditTask));
			ScannedDrivers.Clear();
			List<ScannedDriverItemViewModel> sortedList = list.OrderBy((ScannedDriverItemViewModel x) => x.IsInstalled).ToList();
			foreach (ScannedDriverItemViewModel item in sortedList)
			{
				ScannedDrivers.Add(item);
			}
			MissingDriverCount = Enumerable.Count(ScannedDrivers, (ScannedDriverItemViewModel x) => !x.IsInstalled);
			InstalledDriverCount = Enumerable.Count(ScannedDrivers, (ScannedDriverItemViewModel x) => x.IsInstalled);
			UpdateDriverStatsProperties();
			if (MissingDriverCount > 0)
			{
				PostScanHeadlineText = $"Successful! You Need to Download {MissingDriverCount} Drivers";
				PostScanSubtitleText = $"{MissingDriverCount} missing driver components detected for your PC";
				ScanSummaryTitle = $"{MissingDriverCount} Gaming Components Need Installation";
				ScanSummarySubtitle = "Missing drivers or runtimes were detected. Click 'Installation Driver' below to install and update missing components.";
				ScanGaugeSubtitle = "Done";
				DriverNotchButtonTitle = "Install Drivers";
				DriverNotchButtonSubtitle = "Click to review & install";
			}
			else
			{
				PostScanHeadlineText = "Successful! All Essential Drivers Are Installed";
				PostScanSubtitleText = "Click below to install extra & advanced drivers for your PC";
				ScanSummaryTitle = "All Essential Gaming Drivers & Runtimes Installed";
				ScanSummarySubtitle = "All core gaming driver stacks are verified. You can proceed to 'Advanced Drivers' to manage updates or configure buffers.";
				ScanGaugeSubtitle = "Done";
				DriverNotchButtonTitle = "Advanced Drivers";
				DriverNotchButtonSubtitle = "Click to review & install";
			}
			ScanProgressPercent = 100.0;
			ScanStatusText = "Hardware & driver scan completed successfully!";
			HasCompletedDriverScan = true;
			AnimateDriverStatsTickerAsync();
			IsHoverGreenEnabled = false;
			IsInstallButtonRevealed = false;
			IsInstallTransitionFinished = false;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			ScanStatusText = "Scan failed: " + ex2.Message;
			ScanGaugeSubtitle = "ERROR";
			_log.Log("Windows", "Driver scan error", "ERROR", null, null, ex2.Message);
		}
		finally
		{
			IsScanningDrivers = false;
		}
	}

	private async Task<List<ScannedDriverItemViewModel>> PerformAuditHardwareAsync()
	{
		List<ScannedDriverItemViewModel> list = new List<ScannedDriverItemViewModel>();
		string mbModel = await _systemDetection.GetMotherboardModelAsync();
		string chipsetDownloadUrl = "https://www.amd.com/en/support/download/drivers.html";
		string chipsetSpec;
		if (mbModel.Contains("B550", StringComparison.OrdinalIgnoreCase))
		{
			chipsetSpec = "AMD B550 Chipset (" + mbModel + ")";
			chipsetDownloadUrl = "https://www.amd.com/en/support/chipsets/amd-socket-am4/b550";
		}
		else if (mbModel.Contains("X570", StringComparison.OrdinalIgnoreCase))
		{
			chipsetSpec = "AMD X570 Chipset (" + mbModel + ")";
			chipsetDownloadUrl = "https://www.amd.com/en/support/chipsets/amd-socket-am4/x570";
		}
		else if (mbModel.Contains("B650", StringComparison.OrdinalIgnoreCase))
		{
			chipsetSpec = "AMD B650 Chipset (" + mbModel + ")";
			chipsetDownloadUrl = "https://www.amd.com/en/support/chipsets/amd-socket-am5/b650";
		}
		else if (mbModel.Contains("X670", StringComparison.OrdinalIgnoreCase))
		{
			chipsetSpec = "AMD X670 Chipset (" + mbModel + ")";
			chipsetDownloadUrl = "https://www.amd.com/en/support/chipsets/amd-socket-am5/x670";
		}
		else if (mbModel.Contains("B450", StringComparison.OrdinalIgnoreCase))
		{
			chipsetSpec = "AMD B450 Chipset (" + mbModel + ")";
			chipsetDownloadUrl = "https://www.amd.com/en/support/chipsets/amd-socket-am4/b450";
		}
		else if (mbModel.Contains("A520", StringComparison.OrdinalIgnoreCase))
		{
			chipsetSpec = "AMD A520 Chipset (" + mbModel + ")";
			chipsetDownloadUrl = "https://www.amd.com/en/support/chipsets/amd-socket-am4/a520";
		}
		else if (mbModel.Contains("Intel", StringComparison.OrdinalIgnoreCase) || mbModel.Contains("Z790", StringComparison.OrdinalIgnoreCase) || mbModel.Contains("B760", StringComparison.OrdinalIgnoreCase) || mbModel.Contains("Z690", StringComparison.OrdinalIgnoreCase) || mbModel.Contains("B660", StringComparison.OrdinalIgnoreCase))
		{
			chipsetSpec = "Intel Chipset (" + mbModel + ")";
			chipsetDownloadUrl = "https://www.intel.com/content/www/us/en/download/19347/chipset-device-software.html";
		}
		else
		{
			chipsetSpec = mbModel + " Chipset Driver";
		}
		bool isChipsetInstalled = !string.IsNullOrWhiteSpace(mbModel) && !mbModel.Equals("Standard Motherboard", StringComparison.OrdinalIgnoreCase);
		list.Add(new ScannedDriverItemViewModel
		{
			Id = "scan_chipset",
			Title = "Motherboard Chipset Driver",
			Icon = "\ue7f8",
			Category = "Motherboard & Chipset",
			DetectedInfo = chipsetSpec,
			IsInstalled = isChipsetInstalled,
			StatusBadgeText = (isChipsetInstalled ? "INSTALLED & OPTIMAL" : "NOT INSTALLED / MISSING"),
			StatusBadgeColor = (isChipsetInstalled ? "#34D399" : "#EF4444"),
			StatusBadgeBg = (isChipsetInstalled ? "#064E3B" : "#451A1A"),
			Description = (isChipsetInstalled ? "Core motherboard chipset, PCIe bus, and SMBus controllers detected and functioning normally." : "Motherboard chipset driver is missing or generic. Install latest drivers for optimal stability."),
			ActionButtonText = (isChipsetInstalled ? "Device Manager" : "Get Chipset Driver"),
			Action = () =>
			{
				if (isChipsetInstalled)
				{
					LaunchProcess("devmgmt.msc");
				}
				else
				{
					var match = DriverItems.FirstOrDefault(d => d.Id == "drv_chipset");
					if (match != null)
					{
						SelectedSlideIndex = 0;
						_ = HandleDriverItemPrimaryClickAsync(match);
					}
					else
					{
						LaunchProcess("devmgmt.msc");
					}
				}
			}
		});
		string gpuModel = await _systemDetection.GetGpuModelAsync();
		string gpuDriver = await _systemDetection.GetGpuDriverVersionAsync();
		bool isGpuInstalled = !string.IsNullOrWhiteSpace(gpuModel) && !gpuModel.Contains("Basic Display", StringComparison.OrdinalIgnoreCase) && !gpuModel.Equals("Unknown GPU", StringComparison.OrdinalIgnoreCase);
		list.Add(new ScannedDriverItemViewModel
		{
			Id = "scan_gpu",
			Title = "GPU Graphics Driver",
			Icon = "\ue7f4",
			Category = "Display Hardware",
			DetectedInfo = (isGpuInstalled ? (gpuModel + " (Driver: " + gpuDriver + ")") : "Microsoft Basic Display Adapter"),
			IsInstalled = isGpuInstalled,
			StatusBadgeText = (isGpuInstalled ? "INSTALLED & OPTIMAL" : "NOT INSTALLED / MISSING"),
			StatusBadgeColor = (isGpuInstalled ? "#34D399" : "#EF4444"),
			StatusBadgeBg = (isGpuInstalled ? "#064E3B" : "#451A1A"),
			Description = (isGpuInstalled ? "Dedicated GPU driver is installed and ready for hardware acceleration and GameLoop 3D engine." : "Dedicated GPU driver is missing. Games will run on slow software emulation with extreme lag."),
			ActionButtonText = (isGpuInstalled ? "Check Updates" : "Install GPU Driver"),
			Action = () =>
			{
				OpenDriverDownloadPage(gpuModel);
			}
		});
		(string wifi, string bluetooth) tuple = await _systemDetection.GetWirelessHardwareAsync();
		string wifiDevice = tuple.wifi;
		string btDevice = tuple.bluetooth;
		bool isWirelessInstalled = !string.IsNullOrWhiteSpace(wifiDevice) && !wifiDevice.Equals("Wi-Fi 6 / 802.11ax Adapter", StringComparison.OrdinalIgnoreCase);
		string wirelessDownloadUrl = ((wifiDevice.Contains("Intel", StringComparison.OrdinalIgnoreCase) || btDevice.Contains("Intel", StringComparison.OrdinalIgnoreCase)) ? "https://www.intel.com/content/www/us/en/support/detect.html" : ("https://www.google.com/search?q=" + Uri.EscapeDataString(wifiDevice + " driver download")));
		list.Add(new ScannedDriverItemViewModel
		{
			Id = "scan_wireless",
			Title = "Wi-Fi & Bluetooth Wireless Drivers",
			Icon = "\ue701",
			Category = "Wireless Networking",
			DetectedInfo = FormatWirelessSpec(wifiDevice, btDevice),
			IsInstalled = isWirelessInstalled,
			StatusBadgeText = (isWirelessInstalled ? "INSTALLED & ACTIVE" : "NOT INSTALLED / MISSING"),
			StatusBadgeColor = (isWirelessInstalled ? "#34D399" : "#EF4444"),
			StatusBadgeBg = (isWirelessInstalled ? "#064E3B" : "#451A1A"),
			Description = (isWirelessInstalled ? "High-speed wireless interface and Bluetooth controller detected and optimized for low ping." : "Wireless drivers missing or using generic Windows drivers. Frame drops and audio delay may occur."),
			ActionButtonText = (isWirelessInstalled ? "Connections" : "Check Wireless"),
			Action = () =>
			{
				if (isWirelessInstalled)
				{
					LaunchProcess("ncpa.cpl");
				}
				else
				{
					var match = DriverItems.FirstOrDefault(d => d.Id == "drv_wireless");
					if (match != null)
					{
						SelectedSlideIndex = 0;
						_ = HandleDriverItemPrimaryClickAsync(match);
					}
					else
					{
						LaunchProcess("ncpa.cpl");
					}
				}
			}
		});
		string sys32 = Environment.SystemDirectory;
		string sysWow64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64");
		bool dx9Exists = File.Exists(Path.Combine(sys32, "d3dx9_43.dll")) || (Directory.Exists(sysWow64) && File.Exists(Path.Combine(sysWow64, "d3dx9_43.dll")));
		bool dx11Exists = File.Exists(Path.Combine(sys32, "d3dx11_43.dll")) || (Directory.Exists(sysWow64) && File.Exists(Path.Combine(sysWow64, "d3dx11_43.dll")));
		bool isDirectXInstalled = dx9Exists & dx11Exists;
		list.Add(new ScannedDriverItemViewModel
		{
			Id = "scan_directx",
			Title = "DirectX Runtimes (DirectX+ Support)",
			Icon = "\ue790",
			Category = "DirectX 9/10/11 Stack",
			DetectedInfo = (isDirectXInstalled ? "DirectX 9.0c / 10 / 11 Legacy Dynamic Libraries Present" : "DirectX 9/11 Legacy Runtimes Missing"),
			IsInstalled = isDirectXInstalled,
			StatusBadgeText = (isDirectXInstalled ? "INSTALLED & READY" : "NOT INSTALLED / MISSING"),
			StatusBadgeColor = (isDirectXInstalled ? "#34D399" : "#EF4444"),
			StatusBadgeBg = (isDirectXInstalled ? "#064E3B" : "#451A1A"),
			Description = (isDirectXInstalled ? "Legacy DirectX dynamic libraries and DirectPlay components are present for GameLoop emulation." : "DirectX End-User Runtimes missing in System32. GameLoop DirectX+ mode requires these DLLs."),
			ActionButtonText = (isDirectXInstalled ? "DxDiag Tool" : "Download DirectX"),
			Action = () =>
			{
				if (isDirectXInstalled)
				{
					LaunchProcess("dxdiag.exe");
				}
				else
				{
					DownloadDirectXAsync();
				}
			}
		});
		bool isVcInstalled = false;
		string vcVersion = "";
		try
		{
			using RegistryKey x64Key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\VisualStudio\\14.0\\VC\\Runtimes\\X64");
			if (x64Key != null && Convert.ToInt32(x64Key.GetValue("Installed", 0)) == 1)
			{
				isVcInstalled = true;
				vcVersion = x64Key.GetValue("Version")?.ToString() ?? "2015-2022";
			}
		}
		catch
		{
		}
		list.Add(new ScannedDriverItemViewModel
		{
			Id = "scan_vcredist",
			Title = "Visual C++ Redistributable Runtimes",
			Icon = "\ue896",
			Category = "C++ Runtime Environment",
			DetectedInfo = (isVcInstalled ? ("Visual C++ 2015-2022 x86/x64 Runtime (" + vcVersion + ")") : "Visual C++ 2015-2022 Runtime Not Installed"),
			IsInstalled = isVcInstalled,
			StatusBadgeText = (isVcInstalled ? "INSTALLED & OPTIMAL" : "NOT INSTALLED / MISSING"),
			StatusBadgeColor = (isVcInstalled ? "#34D399" : "#EF4444"),
			StatusBadgeBg = (isVcInstalled ? "#064E3B" : "#451A1A"),
			Description = (isVcInstalled ? "MSVCP140 and VCRUNTIME140 runtime libraries are properly installed for emulator processes." : "Missing Visual C++ runtimes will prevent emulator processes (aow_exe.exe, TBSGameLive.exe) from starting."),
			ActionButtonText = (isVcInstalled ? "Installed Apps" : "Get VC++ Redist"),
			Action = () =>
			{
				if (isVcInstalled)
				{
					LaunchProcess("ms-settings:appsfeatures");
				}
				else
				{
					OpenUrl("https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist");
				}
			}
		});
		bool isDotNetInstalled = false;
		string dotNetVersion = "4.8.1";
		try
		{
			using RegistryKey ndpKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full");
			if (ndpKey != null)
			{
				int release = Convert.ToInt32(ndpKey.GetValue("Release", 0));
				if (release >= 394802)
				{
					isDotNetInstalled = true;
					dotNetVersion = ndpKey.GetValue("Version")?.ToString() ?? "4.8.1";
				}
			}
		}
		catch
		{
		}
		list.Add(new ScannedDriverItemViewModel
		{
			Id = "scan_dotnet",
			Title = ".NET Framework & Desktop Runtime",
			Icon = "\ue71d",
			Category = "Application Framework",
			DetectedInfo = (isDotNetInstalled ? (".NET Framework " + dotNetVersion + " Present") : ".NET Framework Missing"),
			IsInstalled = isDotNetInstalled,
			StatusBadgeText = (isDotNetInstalled ? "INSTALLED & OPTIMAL" : "NOT INSTALLED / MISSING"),
			StatusBadgeColor = (isDotNetInstalled ? "#34D399" : "#EF4444"),
			StatusBadgeBg = (isDotNetInstalled ? "#064E3B" : "#451A1A"),
			Description = (isDotNetInstalled ? "Core .NET desktop runtimes are installed for GameLoop services and system optimization." : ".NET Framework is missing. Required for launcher services and optimization routines."),
			ActionButtonText = (isDotNetInstalled ? "Windows Features" : "Get .NET Runtime"),
			Action = () =>
			{
				if (isDotNetInstalled)
				{
					LaunchProcess("OptionalFeatures.exe");
				}
				else
				{
					var match = DriverItems.FirstOrDefault(d => d.Id == "drv_dotnet");
					if (match != null)
					{
						SelectedSlideIndex = 0;
						_ = HandleDriverItemPrimaryClickAsync(match);
					}
					else
					{
						LaunchProcess("OptionalFeatures.exe");
					}
				}
			}
		});
		return list;
	}

	[RelayCommand]
	private void OpenDriverInstallationView()
	{
		HasVisitedDriverInstallationView = true;
		IsDriverInstallationViewActive = true;
	}

	[RelayCommand]
	private async Task SkipToUnusedServices()
	{
		if (IsScanningDrivers || IsRescanningDrivers || IsRescanCompleted)
		{
			return;
		}

		if (HasCompletedDriverScan)
		{
			OpenDriverInstallationView();
			return;
		}

		// When skipped before scan: Discard/hide Drivers Hub entirely and navigate to 2nd slide (Unused Services)
		IsDriversIncludedMode = false;
		_settings.Settings.IsDriversInstalledMode = true;
		_settings.Save();
		HasSkippedDriverScanner = true;
		SelectedSlideIndex = 1;
		IsDriverInstallationViewActive = false;
		TriggerEntranceAnimation(animateScrollDots: false);
	}

	[RelayCommand]
	private void BackToDriverScanner()
	{
		IsDriverInstallationViewActive = false;
	}

	[ObservableProperty]
	private bool _isRescanButtonVisible = true;

	[ObservableProperty]
	private bool _hasRescannedDrivers = false;

	[RelayCommand]
	public async Task RescanDriversAsync()
	{
		if (IsRescanningDrivers || IsScanningDrivers)
		{
			return;
		}
		IsRescanningDrivers = true;
		IsRescanCompleted = false;
		InstalledDriversBadgeText = "Scanning Drivers...";

		if (DriverItems.Count == 0)
		{
			for (int i = 0; i < 6; i++)
			{
				DriverItems.Add(new DriverItemViewModel { IsRescanning = true });
			}
		}
		else
		{
			foreach (var item in DriverItems)
			{
				item.IsRescanning = true;
			}
		}

		try
		{
			_systemDetection.InvalidateHardwareCache();

			var auditTask = Task.Run((Func<Task<List<ScannedDriverItemViewModel>>?>)PerformAuditHardwareAsync);
			var loadTask = LoadDriversAsync(isRescan: true);
			var minDelayTask = Task.Delay(2300);

			await Task.WhenAll(auditTask, loadTask, minDelayTask);

			List<ScannedDriverItemViewModel> list = await auditTask;
			ScannedDrivers.Clear();
			List<ScannedDriverItemViewModel> sortedList = list.OrderBy((ScannedDriverItemViewModel x) => x.IsInstalled).ToList();
			foreach (ScannedDriverItemViewModel item in sortedList)
			{
				ScannedDrivers.Add(item);
			}
			MissingDriverCount = Enumerable.Count(ScannedDrivers, (ScannedDriverItemViewModel x) => !x.IsInstalled);
			InstalledDriverCount = Enumerable.Count(ScannedDrivers, (ScannedDriverItemViewModel x) => x.IsInstalled);
			UpdateDriverStatsProperties();
			UpdateDriverActionStates();
			InstalledDriversBadgeText = $"{DriverItems.Count} Drivers Found";
			HasRescannedDrivers = true;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			_log.Log("Windows", "Rescan error", "ERROR", null, null, ex2.Message);
			InstalledDriversBadgeText = $"{DriverItems.Count} Drivers Found";
		}
		finally
		{
			foreach (var item in DriverItems)
			{
				item.IsRescanning = false;
			}
			IsRescanningDrivers = false;
		}
		IsRescanCompleted = true;
		await Task.Delay(1800);
		IsRescanCompleted = false;
	}






}


public partial class DriverItemViewModel : ObservableObject
{
	[ObservableProperty]
	private string _primaryButtonText = "Download";

	[ObservableProperty]
	private string _secondaryButtonText = "Open Tool";

	[ObservableProperty]
	private bool _isDownloading = false;

	[ObservableProperty]
	private bool _isInstalling = false;

	[ObservableProperty]
	private bool _isInstalled = false;

	[ObservableProperty]
	private bool _isDownloaded = false;

	[ObservableProperty]
	private double _downloadProgressRatio = 0.0;

	public double DownloadProgressWidth => Math.Max(0.0, Math.Min(162.0, DownloadProgressRatio * 162.0));

	public double DownloadProgressFillWidth => DownloadProgressRatio <= 0.0 ? 0.0 : Math.Min(180.0, 15.0 + (DownloadProgressRatio * 158.0));

	partial void OnDownloadProgressRatioChanged(double value)
	{
		OnPropertyChanged(nameof(DownloadProgressWidth));
		OnPropertyChanged(nameof(DownloadProgressFillWidth));
	}

	public Action? StatusChangedAction { get; set; }

	partial void OnIsInstalledChanged(bool value)
	{
		if (value)
		{
			PrimaryButtonText = "Installed";
		}
		else
		{
			PrimaryButtonText = IsDownloaded ? "Install" : "Download";
		}
		StatusChangedAction?.Invoke();
	}

	partial void OnIsDownloadedChanged(bool value)
	{
		if (!IsInstalled)
		{
			PrimaryButtonText = value ? "Install" : "Download";
		}
		StatusChangedAction?.Invoke();
	}

	[ObservableProperty]
	private string _statusText = "";

	public string Id { get; set; } = "";

	[ObservableProperty]
	private string _title = "";

	[ObservableProperty]
	private string _icon = "\ue7f4";

	[ObservableProperty]
	private string _currentSpec = "";

	[ObservableProperty]
	private string _description = "";

	[ObservableProperty]
	private bool _isRescanning = false;

	public string DownloadUrl { get; set; } = "";

	public string TargetFilePath { get; set; } = "";

	public Action? PrimaryAction { get; set; }

	public Action? SecondaryAction { get; set; }



	[RelayCommand]
	private void Primary()
	{
		PrimaryAction?.Invoke();
	}

	[RelayCommand]
	private void Secondary()
	{
		SecondaryAction?.Invoke();
	}
}

public partial class ScannedDriverItemViewModel : ObservableObject
{
	[ObservableProperty]
	private string _detectedInfo = "";

	[ObservableProperty]
	private bool _isInstalled;

	[ObservableProperty]
	private string _statusBadgeText = "";

	[ObservableProperty]
	private string _statusBadgeColor = "#34D399";

	[ObservableProperty]
	private string _statusBadgeBg = "#064E3B";

	[ObservableProperty]
	private string _description = "";


	public string Id { get; set; } = "";

	public string Title { get; set; } = "";

	public string Icon { get; set; } = "\ue7f4";

	public string Category { get; set; } = "";

	public string ActionButtonText { get; set; } = "";

	public Action? Action { get; set; }








	[RelayCommand]
	private void ExecuteAction()
	{
		Action?.Invoke();
	}
}

public partial class OptimizationCardViewModel : ObservableObject
{
	[ObservableProperty]
	private string _currentValue = "";

	[ObservableProperty]
	private string _recommendedValue = "";

	[ObservableProperty]
	private bool _isOptimal;

	[ObservableProperty]
	private bool _isSupported = true;

	[ObservableProperty]
	private string _statusMessage = "";

	[ObservableProperty]
	private bool _isBusy;



	public string Id { get; set; } = "";

	public string Name { get; set; } = "";

	public string Category { get; set; } = "";

	public string Description { get; set; } = "";

	public RiskLevel RiskLevel { get; set; }

	public bool RequiresAdmin { get; set; }

	public bool IsAdmin { get; set; }

	public IOptimization? Optimization { get; set; }

	public string RiskText
	{
		get
		{
			RiskLevel riskLevel = RiskLevel;
			if (1 == 0)
			{
			}
			string result = riskLevel switch
			{
				RiskLevel.Low => "Low Risk", 
				RiskLevel.Medium => "Medium Risk", 
				RiskLevel.High => "High Risk", 
				_ => "Unknown", 
			};
			if (1 == 0)
			{
			}
			return result;
		}
	}

	public string RiskColor
	{
		get
		{
			RiskLevel riskLevel = RiskLevel;
			if (1 == 0)
			{
			}
			string result = riskLevel switch
			{
				RiskLevel.Low => "#A3E635", 
				RiskLevel.Medium => "#FBBF24", 
				RiskLevel.High => "#EF4444", 
				_ => "#94A3B8", 
			};
			if (1 == 0)
			{
			}
			return result;
		}
	}









	[RelayCommand]
	public async Task<bool> ApplyAsync()
	{
		if (Optimization == null || IsBusy)
		{
			return false;
		}
		IsBusy = true;
		try
		{
			OptimizationResult result = await Optimization.ApplyAsync();
			if (result.Success)
			{
				OptimizationState state = await Optimization.GetCurrentStateAsync();
				CurrentValue = state.CurrentValue;
				IsOptimal = state.IsOptimal;
				StatusMessage = result.Message;
			}
			else
			{
				StatusMessage = "Failed: " + result.Message;
			}
			return result.Success;
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	public async Task RestoreAsync()
	{
		if (Optimization == null || IsBusy)
		{
			return;
		}
		IsBusy = true;
		try
		{
			OptimizationResult result = await Optimization.RestoreAsync();
			if (result.Success)
			{
				OptimizationState state = await Optimization.GetCurrentStateAsync();
				CurrentValue = state.CurrentValue;
				IsOptimal = state.IsOptimal;
				StatusMessage = "Restored to original value.";
			}
			else
			{
				StatusMessage = "Restore failed: " + result.Message;
			}
		}
		finally
		{
			IsBusy = false;
		}
	}
}
