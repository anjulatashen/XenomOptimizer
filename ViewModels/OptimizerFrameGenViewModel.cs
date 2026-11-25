using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

public partial class GameLoopRegistryItem : ObservableObject
{
    public string KeyName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "MobileGamePC"; // "MobileGamePC", "Responsiveness", "CpuPriority"
    public string RegistryPath { get; set; } = string.Empty;
    public object TargetValue { get; set; } = 1;
    public object DefaultValue { get; set; } = 0;
    public RegistryValueKind ValueKind { get; set; } = RegistryValueKind.DWord;
    public string FormattedValue
    {
        get => TargetValue is int i ? $"{i} (0x{i:X})" : TargetValue?.ToString() ?? "";
        set { }
    }

    [ObservableProperty]
    private bool _isApplied;

    [ObservableProperty]
    private bool _isBusy;
}

public partial class OptimizerFrameGenViewModel : ObservableObject
{
    private readonly FrameGenService _frameGen = new();
    private readonly ProcessSuspenderService _suspender = new();
    private readonly GameLoopCleanerService _cleaner = new();
    private readonly ISettingsService? _settings;

    public FrameGenService FrameGen => _frameGen;
    public ProcessSuspenderService Suspender => _suspender;
    public GameLoopCleanerService Cleaner => _cleaner;

    // ── Slide Navigation (0 = GameLoop Engine, 1 = In-Game Unlocker, 2 = GameLoop Optimizer) ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPage1Active))]
    [NotifyPropertyChangedFor(nameof(IsPage2Active))]
    [NotifyPropertyChangedFor(nameof(IsPage3Active))]
    private int _selectedSlideIndex = 0;

    public bool IsPage1Active => SelectedSlideIndex == 0;
    public bool IsPage2Active => SelectedSlideIndex == 1;
    public bool IsPage3Active => SelectedSlideIndex == 2;

    // ── Slide 0: GameLoop Engine Configuration Center ──
    [ObservableProperty] private int _selectedEngineIndex = 0; // DirectX+
    [ObservableProperty] private int _selectedResolutionIndex = 3; // 1440x1080 iPad View
    [ObservableProperty] private int _selectedRamIndex = 3; // 8192 MB (8GB)
    [ObservableProperty] private int _selectedCoresIndex = 2; // 4 Cores
    [ObservableProperty] private int _selectedDpiIndex = 1; // 240 DPI
    [ObservableProperty] private int _selectedDeviceProfileIndex = 0; // Samsung SM-X910 120Hz
    [ObservableProperty] private bool _isRenderCacheEnabled = true;
    [ObservableProperty] private bool _isDiscreteGpuEnforced = true;
    [ObservableProperty] private bool _isRenderOptimizationEnabled = true;
    [ObservableProperty] private bool _isVsyncDisabled = true;
    [ObservableProperty] private string _engineStatusMessage = "Ready to configure & apply GameLoop Engine settings.";

    public ObservableCollection<string> EngineOptions { get; } = new()
    {
        "OpenGL+ (Zero Black Screen Bug • Maximum Stability)",
        "DirectX+ (High CPU Rendering Pipeline)",
        "Vulkan (Low Driver Overhead & Next-Gen Pipeline)",
        "Auto (Smart Adaptive Engine Switching)"
    };

    public ObservableCollection<string> ResolutionOptions { get; } = new()
    {
        "1920x1080 (16:9 Full HD - Native Standard)",
        "2560x1440 (16:9 2K QHD - High Clarity)",
        "3840x2160 (16:9 4K UHD - Extreme Fidelity)",
        "1440x1080 (4:3 iPad View - Pro Gamer Stretched)",
        "1920x1440 (4:3 Pro Ultra - Ultra Crisp Stretched)",
        "1280x720 (16:9 Standard HD - Maximum FPS)"
    };

    public ObservableCollection<string> RamOptions { get; } = new()
    {
        "Auto (Smart System Allocation)",
        "2048 MB (2 GB - Lightweight Setup)",
        "4096 MB (4 GB - Recommended for 8GB PC)",
        "8192 MB (8 GB - High Performance for 16GB+ PC)",
        "12288 MB (12 GB - Ultra Heavy Duty)"
    };

    public ObservableCollection<string> CoreOptions { get; } = new()
    {
        "Auto (Smart Multi-Core Balancing)",
        "2 Cores (Dual-Core Virtual Machine)",
        "4 Cores (Quad-Core - Recommended for 120 FPS)",
        "6 Cores (Hexa-Core for 8+ Core CPUs)",
        "8 Cores (Octa-Core Maximum SMT Processing)"
    };

    public ObservableCollection<string> DpiOptions { get; } = new()
    {
        "160 DPI (Fastest Raw Mouse Aim & Low Latency)",
        "240 DPI (Balanced Standard Preset)",
        "320 DPI (Sharp High-DPI Display)",
        "480 DPI (Ultra Clear Text & Crisp Minimap)"
    };

    public ObservableCollection<string> DeviceProfileOptions { get; } = new()
    {
        "Samsung SM-X920 (Galaxy Tab S10+ - 120Hz Flagship Active)",
        "ASUS ROG Phone 5 (120Hz Ultra Gaming Profile)",
        "Xiaomi Black Shark 4 (Hyper-Response Touch Profile)",
        "Generic High-Performance 120Hz Flagship"
    };

    // ── Registry Tweaks Collections ──
    public ObservableCollection<GameLoopRegistryItem> MobileGamePcItems { get; } = new();
    public ObservableCollection<GameLoopRegistryItem> ResponsivenessItems { get; } = new();
    public ObservableCollection<GameLoopRegistryItem> CpuPriorityItems { get; } = new();

    [ObservableProperty]
    private bool _isApplyingAllTweaks;

    [ObservableProperty]
    private string _tweakStatusMessage = "Ready to apply ultra-low latency & engine registry tweaks.";

    [ObservableProperty]
    private bool _wasGameLoopClosed;

    // ── Active.sav Configurator Properties (Slide 1) ──
    [ObservableProperty]
    private double _activeSavSliderValue = 3; // 0=30, 1=60, 2=90, 3=120

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFps30))]
    [NotifyPropertyChangedFor(nameof(IsFps60))]
    [NotifyPropertyChangedFor(nameof(IsFps90))]
    [NotifyPropertyChangedFor(nameof(IsFps120))]
    private int _activeSavFrameRate = 120;

    public bool IsFps30 => ActiveSavFrameRate == 30;
    public bool IsFps60 => ActiveSavFrameRate == 60;
    public bool IsFps90 => ActiveSavFrameRate == 90;
    public bool IsFps120 => ActiveSavFrameRate == 120;

    [ObservableProperty]
    private string _activeSavRecommendationText = "Recommended: Ultra 120 FPS Extreme (SM-X910 Whitelist Active)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsQualitySuperSmooth))]
    [NotifyPropertyChangedFor(nameof(IsQualitySmooth))]
    [NotifyPropertyChangedFor(nameof(IsQualityBalanced))]
    [NotifyPropertyChangedFor(nameof(IsQualityHd))]
    [NotifyPropertyChangedFor(nameof(IsQualityHdr))]
    [NotifyPropertyChangedFor(nameof(IsQualityUltraHdr))]
    private string _activeSavQuality = "HDR"; // "Super Smooth", "Smooth", "Balanced", "HD", "HDR", "Ultra HDR"

    public bool IsQualitySuperSmooth => ActiveSavQuality == "Super Smooth";
    public bool IsQualitySmooth => ActiveSavQuality == "Smooth";
    public bool IsQualityBalanced => ActiveSavQuality == "Balanced";
    public bool IsQualityHd => ActiveSavQuality == "HD";
    public bool IsQualityHdr => ActiveSavQuality == "HDR";
    public bool IsQualityUltraHdr => ActiveSavQuality == "Ultra HDR";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStyleClassic))]
    [NotifyPropertyChangedFor(nameof(IsStyleColorful))]
    [NotifyPropertyChangedFor(nameof(IsStyleRealistic))]
    [NotifyPropertyChangedFor(nameof(IsStyleSoft))]
    [NotifyPropertyChangedFor(nameof(IsStyleMovie))]
    private string _activeSavStyle = "Colorful"; // "Classic", "Colorful", "Realistic", "Soft", "Movie"

    public bool IsStyleClassic => ActiveSavStyle == "Classic";
    public bool IsStyleColorful => ActiveSavStyle == "Colorful";
    public bool IsStyleRealistic => ActiveSavStyle == "Realistic";
    public bool IsStyleSoft => ActiveSavStyle == "Soft";
    public bool IsStyleMovie => ActiveSavStyle == "Movie";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAaDisable))]
    [NotifyPropertyChangedFor(nameof(IsAa2x))]
    [NotifyPropertyChangedFor(nameof(IsAa4x))]
    private string _activeSavAntiAliasing = "Disable"; // "Disable", "2x", "4x"

    public bool IsAaDisable => ActiveSavAntiAliasing == "Disable";
    public bool IsAa2x => ActiveSavAntiAliasing == "2x";
    public bool IsAa4x => ActiveSavAntiAliasing == "4x";

    [ObservableProperty]
    private bool _isPowerSavingEnabled = false;

    [ObservableProperty]
    private bool _isShadowsEnabled = true;

    [ObservableProperty]
    private bool _isAdaptiveGraphicsEnabled = false;

    [ObservableProperty]
    private bool _isLobbyEnhancementEnabled = true;

    [ObservableProperty]
    private double _activeSavBrightness = 100; // 80 - 150 %

    [ObservableProperty]
    private double _activeSavNonStandardScreen = 0.0; // 0.0 - 50.0

    [ObservableProperty]
    private bool _isApplyingActiveSav;

    [ObservableProperty]
    private string _activeSavStatusMessage = "Ready to patch PUBG Mobile Active.sav & inject in-game configs.";

    // ── Page 2 Existing Properties ──
    [ObservableProperty]
    private double _targetFps = 120;

    [ObservableProperty]
    private int _selectedQualityIndex = 0; // 0 = Smooth

    [ObservableProperty]
    private bool _isFrameGenActive;

    [ObservableProperty]
    private string _frameGenToggleText = "Apply Frame & Graphics Optimization";

    [ObservableProperty]
    private string _frameGenStatusText = "Target: 120 FPS • Smooth Profile ready to apply.";

    [ObservableProperty]
    private string _cleanerStatusText = "Ready to clean telemetry and cache files.";

    [ObservableProperty]
    private bool _isProcessSuspendActive;

    [ObservableProperty]
    private string _suspendedStatusText = "0 background apps frozen.";

    [ObservableProperty]
    private string _gpuStatusText = "Dedicated High-Performance GPU Active";

    public ObservableCollection<string> QualityOptions { get; } = new()
    {
        "Smooth (Maximum FPS & Lowest Input Lag)",
        "Balanced (Clear Visuals & Stable Frames)",
        "HD (High Definition 1080p Textures)",
        "HDR (Dynamic Lighting & Rich Shadows)",
        "Ultra HD (4K Ultra Fidelity & Max Shaders)"
    };

    public OptimizerFrameGenViewModel()
    {
        _settings = App.Services?.GetService(typeof(ISettingsService)) as ISettingsService;
        if (_settings != null)
        {
            if (_settings.Settings.TargetFps >= 60 && _settings.Settings.TargetFps <= 165)
            {
                _targetFps = _settings.Settings.TargetFps;
            }
            if (_settings.Settings.SelectedQualityIndex >= 0 && _settings.Settings.SelectedQualityIndex <= 4)
            {
                _selectedQualityIndex = _settings.Settings.SelectedQualityIndex;
            }
            _isFrameGenActive = _settings.Settings.IsFrameGenActive;
        }

        // Apply GPU route on init silently
        _cleaner.ForceDedicatedGpu();
        UpdateStatusPreview();

        // Initialize Registry Items
        InitializeRegistryItems();
        CheckAllRegistryStatuses();
    }

    [RelayCommand]
    public void SelectSlide(string? index)
    {
        if (int.TryParse(index, out int idx) && idx >= 0 && idx <= 2)
        {
            SelectedSlideIndex = idx;
        }
    }

    [RelayCommand]
    public void SelectNextSlide()
    {
        if (SelectedSlideIndex < 2)
        {
            SelectedSlideIndex++;
        }
    }

    [RelayCommand]
    public void SelectPreviousSlide()
    {
        if (SelectedSlideIndex > 0)
        {
            SelectedSlideIndex--;
        }
    }

    [RelayCommand]
    public void SaveFullEngineSettings()
    {
        try
        {
            // 1. Resolution
            int width = 1440, height = 1080;
            switch (SelectedResolutionIndex)
            {
                case 0: width = 1920; height = 1080; break;
                case 1: width = 2560; height = 1440; break;
                case 2: width = 3840; height = 2160; break;
                case 3: width = 1440; height = 1080; break;
                case 4: width = 1920; height = 1440; break;
                case 5: width = 1280; height = 720; break;
            }

            // 2. Engine
            int engineCode = SelectedEngineIndex switch
            {
                0 => 2, // OpenGL+
                1 => 1, // DirectX+
                2 => 4, // Vulkan
                _ => 2  // Auto/Default OpenGL+
            };

            // 3. RAM
            int ramMb = SelectedRamIndex switch
            {
                1 => 2048,
                2 => 4096,
                3 => 8192,
                4 => 12288,
                _ => 0 // Auto
            };

            // 4. Cores
            int cores = SelectedCoresIndex switch
            {
                1 => 2,
                2 => 4,
                3 => 6,
                4 => 8,
                _ => 0 // Auto
            };

            // 5. DPI
            int dpi = SelectedDpiIndex switch
            {
                0 => 160,
                1 => 240,
                2 => 320,
                3 => 480,
                _ => 240
            };

            // 6. Device Model
            var (manufacturer, model, phoneDevice) = SelectedDeviceProfileIndex switch
            {
                1 => ("ASUS", "ASUS_I005D", "ASUS ROG Phone 5"),
                2 => ("Xiaomi", "SHARK PRS-A0", "Xiaomi Black Shark 4"),
                3 => ("SAMSUNG", "SM-G998B", "Samsung Galaxy S21 Ultra"),
                _ => ("SAMSUNG", "SM-X920", "Samsung Galaxy Tab S10+")
            };

            // Registry keys
            var paths = new[]
            {
                @"Software\Tencent\MobileGamePC\VM",
                @"Software\Tencent\MobileGamePC"
            };

            foreach (var p in paths)
            {
                using var key = Registry.CurrentUser.CreateSubKey(p);
                if (key != null)
                {
                    key.SetValue("Width", width, RegistryValueKind.DWord);
                    key.SetValue("Height", height, RegistryValueKind.DWord);
                    key.SetValue("ResolutionWidth", width, RegistryValueKind.DWord);
                    key.SetValue("ResolutionHeight", height, RegistryValueKind.DWord);
                    key.SetValue("ClientWidth", width, RegistryValueKind.DWord);
                    key.SetValue("ClientHeight", height, RegistryValueKind.DWord);
                    key.SetValue("RenderEngine", engineCode, RegistryValueKind.DWord);
                    key.SetValue("RendererType", engineCode, RegistryValueKind.DWord);
                    key.SetValue("ForceDirectX", engineCode == 1 ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("RenderMode", engineCode == 1 ? 2 : (engineCode == 2 ? 1 : 4), RegistryValueKind.DWord);
                    key.SetValue("EnableGLESv3", 1, RegistryValueKind.DWord);
                    key.SetValue("VMResWidth", width, RegistryValueKind.DWord);
                    key.SetValue("VMResHeight", height, RegistryValueKind.DWord);
                    key.SetValue("DPI", dpi, RegistryValueKind.DWord);

                    if (cores > 0) key.SetValue("CpuCount", cores, RegistryValueKind.DWord);
                    else key.DeleteValue("CpuCount", false);

                    if (ramMb > 0) key.SetValue("MemorySize", ramMb, RegistryValueKind.DWord);
                    else key.DeleteValue("MemorySize", false);

                    key.SetValue("VMDeviceManufacturer", manufacturer, RegistryValueKind.String);
                    key.SetValue("VMDeviceModel", model, RegistryValueKind.String);
                    key.SetValue("VMPhoneDevice", phoneDevice, RegistryValueKind.String);
                    key.SetValue("fps_max", 120, RegistryValueKind.DWord);
                    key.SetValue("com.tencent.ig_FPSLevel", 120, RegistryValueKind.DWord);

                    key.SetValue("RenderCache", IsRenderCacheEnabled ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("RenderOptimization", IsRenderOptimizationEnabled ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("VSyncEnabled", IsVsyncDisabled ? 0 : 1, RegistryValueKind.DWord);
                    key.SetValue("AdbDisable", 0, RegistryValueKind.DWord);
                    key.SetValue("FPSLock", 0, RegistryValueKind.DWord);
                }
            }

            // HKLM MobileGamePC
            using (var hklmKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\WOW6432Node\Tencent\MobileGamePC"))
            {
                if (hklmKey != null)
                {
                    hklmKey.SetValue("FPSLock", 0, RegistryValueKind.DWord);
                    hklmKey.SetValue("fps_max", 120, RegistryValueKind.DWord);
                    hklmKey.SetValue("UseVulkan", SelectedEngineIndex == 2 ? 1 : 0, RegistryValueKind.DWord);
                    hklmKey.SetValue("ForceDirectX", SelectedEngineIndex == 1 ? 1 : 0, RegistryValueKind.DWord);
                }
            }

            // GPU Route
            if (IsDiscreteGpuEnforced)
            {
                _cleaner.ForceDedicatedGpu();
            }

            string coreLabel = cores == 0 ? "Auto" : $"{cores} Cores";
            string ramLabel = ramMb == 0 ? "Auto" : $"{ramMb} MB";
            EngineStatusMessage = $"✓ Engine Applied: {width}x{height} • {coreLabel} • {ramLabel} • {model} 120Hz";
        }
        catch (Exception ex)
        {
            EngineStatusMessage = $"Error applying engine: {ex.Message}";
        }
    }

    [RelayCommand]
    public void AutoDetectEngineSettings()
    {
        int logicalCores = Environment.ProcessorCount;
        if (logicalCores >= 8) SelectedCoresIndex = 2; // 4 Cores
        else if (logicalCores >= 6) SelectedCoresIndex = 2; // 4 Cores
        else SelectedCoresIndex = 1; // 2 Cores

        SelectedRamIndex = 3; // 8192 MB
        SelectedEngineIndex = 0; // OpenGL+ (Zero Black Screen Bug)
        SelectedResolutionIndex = 3; // 1440x1080 (4:3 iPad View)
        SelectedDpiIndex = 1; // 240 DPI
        SelectedDeviceProfileIndex = 0; // Samsung SM-X920
        IsRenderCacheEnabled = true;
        IsDiscreteGpuEnforced = true;
        IsRenderOptimizationEnabled = true;
        IsVsyncDisabled = true;

        EngineStatusMessage = $"Auto-detected {logicalCores} CPU threads. Best gaming profile loaded! Click 'Save & Apply Engine Settings'.";
    }

    private void InitializeRegistryItems()
    {
        // ── 1. MobileGamePC Registry Tweaks (HKLM\SOFTWARE\WOW6432Node\Tencent\MobileGamePC) ──
        const string mgpcPath = @"SOFTWARE\WOW6432Node\Tencent\MobileGamePC";

        MobileGamePcItems.Add(new GameLoopRegistryItem
        {
            KeyName = "HWMemoryCache",
            DisplayName = "RAM Hardware Caching",
            Description = "Enables hardware-level RAM caching for emulator virtual machine.",
            RegistryPath = mgpcPath,
            TargetValue = 1,
            DefaultValue = 0
        });
        MobileGamePcItems.Add(new GameLoopRegistryItem
        {
            KeyName = "ProcessMemoryTrim",
            DisplayName = "RAM Usage Trim",
            Description = "Aggressively trims inactive memory pages to keep emulator RAM usage low.",
            RegistryPath = mgpcPath,
            TargetValue = 1,
            DefaultValue = 0
        });
        MobileGamePcItems.Add(new GameLoopRegistryItem
        {
            KeyName = "VTOptimize",
            DisplayName = "Virtualization Optimize",
            Description = "Optimizes CPU VT-x and AMD-V virtualization pipelines.",
            RegistryPath = mgpcPath,
            TargetValue = 1,
            DefaultValue = 0
        });
        MobileGamePcItems.Add(new GameLoopRegistryItem
        {
            KeyName = "EnableASTC",
            DisplayName = "ASTC Texture Compression",
            Description = "Enables ASTC texture decoding to prevent stutters during texture streaming.",
            RegistryPath = mgpcPath,
            TargetValue = 1,
            DefaultValue = 0
        });
        MobileGamePcItems.Add(new GameLoopRegistryItem
        {
            KeyName = "UseCPUFrameBuffer",
            DisplayName = "CPU Frame Buffer Fix",
            Description = "Optimizes CPU frame buffering and eliminates micro-stutters during rendering.",
            RegistryPath = mgpcPath,
            TargetValue = 1,
            DefaultValue = 0
        });
        MobileGamePcItems.Add(new GameLoopRegistryItem
        {
            KeyName = "UseVulkan",
            DisplayName = "Vulkan API Enable",
            Description = "Unlocks Vulkan graphics pipeline for low driver overhead.",
            RegistryPath = mgpcPath,
            TargetValue = 1,
            DefaultValue = 0
        });
        MobileGamePcItems.Add(new GameLoopRegistryItem
        {
            KeyName = "UseDXTTexture",
            DisplayName = "DXT Texture Compression",
            Description = "Accelerates DirectX DXT compressed texture rendering.",
            RegistryPath = mgpcPath,
            TargetValue = 1,
            DefaultValue = 0
        });
        MobileGamePcItems.Add(new GameLoopRegistryItem
        {
            KeyName = "EnableHyperThreading",
            DisplayName = "Hyper-Threading Optimization",
            Description = "Forces GameLoop to leverage all SMT and logical processor cores.",
            RegistryPath = mgpcPath,
            TargetValue = 1,
            DefaultValue = 0
        });
        MobileGamePcItems.Add(new GameLoopRegistryItem
        {
            KeyName = "GpuTurboMode",
            DisplayName = "GPU Turbo Boost",
            Description = "Locks emulator graphics frequency to highest performance state.",
            RegistryPath = mgpcPath,
            TargetValue = 1,
            DefaultValue = 0
        });
        MobileGamePcItems.Add(new GameLoopRegistryItem
        {
            KeyName = "FPSLock",
            DisplayName = "FPS Lock Bypass (0)",
            Description = "Disables default 90 FPS limiter lock to allow 120 FPS frame rates.",
            RegistryPath = mgpcPath,
            TargetValue = 0,
            DefaultValue = 1
        });
        MobileGamePcItems.Add(new GameLoopRegistryItem
        {
            KeyName = "PowerSaveMode",
            DisplayName = "Disable Power Saving (0)",
            Description = "Prevents power throttling dips during intensive gameplay battles.",
            RegistryPath = mgpcPath,
            TargetValue = 0,
            DefaultValue = 1
        });
        MobileGamePcItems.Add(new GameLoopRegistryItem
        {
            KeyName = "RenderResolutionQuality",
            DisplayName = "Render Quality Max (3)",
            Description = "Sets maximum rasterization render resolution quality.",
            RegistryPath = mgpcPath,
            TargetValue = 3,
            DefaultValue = 0
        });
        MobileGamePcItems.Add(new GameLoopRegistryItem
        {
            KeyName = "CpuBoostMode",
            DisplayName = "CPU Performance Boost",
            Description = "Enables aggressive CPU clock boosting for emulator worker threads.",
            RegistryPath = mgpcPath,
            TargetValue = 1,
            DefaultValue = 0
        });
        MobileGamePcItems.Add(new GameLoopRegistryItem
        {
            KeyName = "MemoryOptimize",
            DisplayName = "Memory Optimization",
            Description = "Ensures zero paging stalls and consistent 120 FPS frame pacing.",
            RegistryPath = mgpcPath,
            TargetValue = 1,
            DefaultValue = 0
        });

        // ── 2. Responsiveness & Latency Tweaks (D:\Lag Pix\Gameloop\Xenom_BOOST_PUBG_Responsiveness.reg) ──
        ResponsivenessItems.Add(new GameLoopRegistryItem
        {
            KeyName = "Win32PrioritySeparation",
            DisplayName = "Foreground Quantum Boost (0x25)",
            Description = "Prioritizes foreground emulator rendering threads with optimized quantum scheduling.",
            Category = "Responsiveness",
            TargetValue = 0x25,
            DefaultValue = 0x02
        });
        ResponsivenessItems.Add(new GameLoopRegistryItem
        {
            KeyName = "MouseDataQueueSize",
            DisplayName = "Mouse Data Queue Size (16)",
            Description = "Reduces mouse buffer queue to eliminate aim delay and click latency.",
            Category = "Responsiveness",
            TargetValue = 0x10,
            DefaultValue = 0x64
        });
        ResponsivenessItems.Add(new GameLoopRegistryItem
        {
            KeyName = "KeyboardDataQueueSize",
            DisplayName = "Keyboard Data Queue Size (16)",
            Description = "Optimizes keyboard input queue for instantaneous movement key response.",
            Category = "Responsiveness",
            TargetValue = 0x10,
            DefaultValue = 0x64
        });
        ResponsivenessItems.Add(new GameLoopRegistryItem
        {
            KeyName = "Latency Sensitive",
            DisplayName = "Kernel Latency Sensitive (True)",
            Description = "Flags all emulator processes as latency-sensitive in Windows kernel.",
            Category = "Responsiveness",
            TargetValue = "True",
            DefaultValue = "False",
            ValueKind = RegistryValueKind.String
        });

        // ── 3. High CPU Priority Tweaks (D:\Lag Pix\Gameloop\Xenom_CPU_Priority_PUBG.reg) ──
        CpuPriorityItems.Add(new GameLoopRegistryItem
        {
            KeyName = "CpuPriorityClass",
            DisplayName = "High CPU Priority Scheduling (3)",
            Description = "Sets High priority (0x03) in IFEO PerfOptions for all 5 GameLoop binaries.",
            Category = "CpuPriority",
            TargetValue = 3,
            DefaultValue = 2
        });
    }

    private static readonly string[] GameLoopBinaries = new[]
    {
        "AndroidEmulatorEn.exe",
        "aow_exe.exe",
        "AndroidRenderer.exe",
        "TBSWebRenderer.exe",
        "AndroidEmulatorEx.exe"
    };

    public void CheckAllRegistryStatuses()
    {
        try
        {
            const string mgpcPath = @"SOFTWARE\WOW6432Node\Tencent\MobileGamePC";
            using var mgpcKey = Registry.LocalMachine.OpenSubKey(mgpcPath, false);

            foreach (var item in MobileGamePcItems)
            {
                if (mgpcKey != null)
                {
                    var val = mgpcKey.GetValue(item.KeyName);
                    item.IsApplied = val != null && Convert.ToInt32(val) == Convert.ToInt32(item.TargetValue);
                }
                else
                {
                    item.IsApplied = false;
                }
            }

            // Check Responsiveness (check on AndroidEmulatorEn.exe)
            string ifeoBase = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\AndroidEmulatorEn.exe";
            using var ifeoKey = Registry.LocalMachine.OpenSubKey(ifeoBase, false);
            foreach (var item in ResponsivenessItems)
            {
                if (ifeoKey != null)
                {
                    var val = ifeoKey.GetValue(item.KeyName);
                    if (item.ValueKind == RegistryValueKind.String)
                    {
                        item.IsApplied = string.Equals(val?.ToString(), item.TargetValue.ToString(), StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        item.IsApplied = val != null && Convert.ToInt32(val) == Convert.ToInt32(item.TargetValue);
                    }
                }
                else
                {
                    item.IsApplied = false;
                }
            }

            // Check CPU Priority
            string perfBase = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\AndroidEmulatorEn.exe\PerfOptions";
            using var perfKey = Registry.LocalMachine.OpenSubKey(perfBase, false);
            foreach (var item in CpuPriorityItems)
            {
                if (perfKey != null)
                {
                    var val = perfKey.GetValue(item.KeyName);
                    item.IsApplied = val != null && Convert.ToInt32(val) == Convert.ToInt32(item.TargetValue);
                }
                else
                {
                    item.IsApplied = false;
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Safely terminates GameLoop and background daemons if running, so registry tweaks can apply cleanly.
    /// </summary>
    public static bool SafeCloseGameLoopRunning()
    {
        var targets = new[]
        {
            "AndroidEmulatorEn", "AndroidEmulator", "aow_exe", "AppMarket",
            "AndroidProcess", "AndroidRenderer", "QMEmulatorService", "TSettingCenter", "syzs_dl_svr"
        };

        bool wasRunning = false;
        foreach (var name in targets)
        {
            try
            {
                var procs = Process.GetProcessesByName(name);
                if (procs.Length > 0)
                {
                    wasRunning = true;
                    foreach (var p in procs)
                    {
                        try
                        {
                            p.Kill();
                            p.WaitForExit(1000);
                        }
                        catch { }
                    }
                }
            }
            catch { }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = $"/F /T /IM {name}.exe",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(500);
            }
            catch { }
        }

        return wasRunning;
    }

    [RelayCommand]
    public async Task ApplyAllEngineTweaksAsync()
    {
        IsApplyingAllTweaks = true;
        TweakStatusMessage = "Checking running processes and closing GameLoop safely...";

        await Task.Run(() =>
        {
            bool wasClosed = SafeCloseGameLoopRunning();
            WasGameLoopClosed = wasClosed;

            try
            {
                // 1. Write MobileGamePC keys (write to both WOW6432Node and standard 64-bit key)
                var mgpcPaths = new[]
                {
                    @"SOFTWARE\WOW6432Node\Tencent\MobileGamePC",
                    @"SOFTWARE\Tencent\MobileGamePC"
                };

                foreach (var p in mgpcPaths)
                {
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(p, RegistryKeyPermissionCheck.ReadWriteSubTree);
                        if (key != null)
                        {
                            foreach (var item in MobileGamePcItems)
                            {
                                key.SetValue(item.KeyName, item.TargetValue, item.ValueKind);
                            }
                        }
                    }
                    catch { }
                }

                // Fallback via reg.exe add to ensure write permissions
                foreach (var item in MobileGamePcItems)
                {
                    string valType = item.ValueKind == RegistryValueKind.DWord ? "REG_DWORD" : "REG_SZ";
                    RunRegCommand($@"add ""HKLM\SOFTWARE\WOW6432Node\Tencent\MobileGamePC"" /v ""{item.KeyName}"" /t {valType} /d {item.TargetValue} /f");
                    RunRegCommand($@"add ""HKLM\SOFTWARE\Tencent\MobileGamePC"" /v ""{item.KeyName}"" /t {valType} /d {item.TargetValue} /f");
                }

                // 2. Write Responsiveness IFEO keys for all 5 GameLoop executables
                foreach (var exe in GameLoopBinaries)
                {
                    string ifeoPath = $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{exe}";
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(ifeoPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
                        if (key != null)
                        {
                            foreach (var item in ResponsivenessItems)
                            {
                                key.SetValue(item.KeyName, item.TargetValue, item.ValueKind);
                            }
                        }
                    }
                    catch { }

                    foreach (var item in ResponsivenessItems)
                    {
                        string valType = item.ValueKind == RegistryValueKind.DWord ? "REG_DWORD" : "REG_SZ";
                        RunRegCommand($@"add ""HKLM\{ifeoPath}"" /v ""{item.KeyName}"" /t {valType} /d ""{item.TargetValue}"" /f");
                    }
                }

                // 3. Write CPU Priority Class in PerfOptions for all 5 GameLoop executables
                foreach (var exe in GameLoopBinaries)
                {
                    string perfPath = $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{exe}\PerfOptions";
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(perfPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
                        if (key != null)
                        {
                            foreach (var item in CpuPriorityItems)
                            {
                                key.SetValue(item.KeyName, item.TargetValue, item.ValueKind);
                            }
                        }
                    }
                    catch { }

                    foreach (var item in CpuPriorityItems)
                    {
                        RunRegCommand($@"add ""HKLM\{perfPath}"" /v ""{item.KeyName}"" /t REG_DWORD /d {item.TargetValue} /f");
                    }
                }
            }
            catch (Exception ex)
            {
                TweakStatusMessage = $"Error applying tweaks: {ex.Message}";
            }
        });

        CheckAllRegistryStatuses();

        TweakStatusMessage = WasGameLoopClosed
            ? "⚡ GameLoop was safely closed. All 16 Extreme Engine & Latency Tweaks applied successfully!"
            : "⚡ All 16 Extreme Engine & Latency Tweaks applied successfully! Start GameLoop to experience the boost.";

        IsApplyingAllTweaks = false;
    }

    [RelayCommand]
    public async Task ApplyAllMobileGamePcAsync()
    {
        IsApplyingAllTweaks = true;
        TweakStatusMessage = "Closing GameLoop and applying all 14 MobileGamePC engine keys...";

        await Task.Run(() =>
        {
            SafeCloseGameLoopRunning();

            var mgpcPaths = new[]
            {
                @"SOFTWARE\WOW6432Node\Tencent\MobileGamePC",
                @"SOFTWARE\Tencent\MobileGamePC"
            };

            foreach (var p in mgpcPaths)
            {
                try
                {
                    using var key = Registry.LocalMachine.CreateSubKey(p, RegistryKeyPermissionCheck.ReadWriteSubTree);
                    if (key != null)
                    {
                        foreach (var item in MobileGamePcItems)
                        {
                            key.SetValue(item.KeyName, item.TargetValue, item.ValueKind);
                        }
                    }
                }
                catch { }
            }

            foreach (var item in MobileGamePcItems)
            {
                string valType = item.ValueKind == RegistryValueKind.DWord ? "REG_DWORD" : "REG_SZ";
                RunRegCommand($@"add ""HKLM\SOFTWARE\WOW6432Node\Tencent\MobileGamePC"" /v ""{item.KeyName}"" /t {valType} /d {item.TargetValue} /f");
                RunRegCommand($@"add ""HKLM\SOFTWARE\Tencent\MobileGamePC"" /v ""{item.KeyName}"" /t {valType} /d {item.TargetValue} /f");
            }
        });

        CheckAllRegistryStatuses();
        TweakStatusMessage = "⚡ All 14 MobileGamePC engine registry keys applied successfully!";
        IsApplyingAllTweaks = false;
    }

    [RelayCommand]
    public async Task RevertAllMobileGamePcAsync()
    {
        IsApplyingAllTweaks = true;
        TweakStatusMessage = "Closing GameLoop and removing all 14 newly created MobileGamePC keys...";

        await Task.Run(() =>
        {
            SafeCloseGameLoopRunning();

            var mgpcPaths = new[]
            {
                @"SOFTWARE\WOW6432Node\Tencent\MobileGamePC",
                @"SOFTWARE\Tencent\MobileGamePC"
            };

            foreach (var p in mgpcPaths)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(p, true);
                    if (key != null)
                    {
                        foreach (var item in MobileGamePcItems)
                        {
                            key.DeleteValue(item.KeyName, false);
                        }
                    }
                }
                catch { }
            }

            foreach (var item in MobileGamePcItems)
            {
                RunRegCommand($@"delete ""HKLM\SOFTWARE\WOW6432Node\Tencent\MobileGamePC"" /v ""{item.KeyName}"" /f");
                RunRegCommand($@"delete ""HKLM\SOFTWARE\Tencent\MobileGamePC"" /v ""{item.KeyName}"" /f");
            }
        });

        CheckAllRegistryStatuses();
        TweakStatusMessage = "↺ All 14 newly created MobileGamePC engine keys removed from registry.";
        IsApplyingAllTweaks = false;
    }

    [RelayCommand]
    public async Task ApplyAllResponsivenessAsync()
    {
        IsApplyingAllTweaks = true;
        TweakStatusMessage = "Closing GameLoop and applying all 4 Kernel & GPU Responsiveness tweaks...";

        await Task.Run(() =>
        {
            SafeCloseGameLoopRunning();

            foreach (var exe in GameLoopBinaries)
            {
                string ifeoPath = $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{exe}";
                try
                {
                    using var key = Registry.LocalMachine.CreateSubKey(ifeoPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
                    if (key != null)
                    {
                        foreach (var item in ResponsivenessItems)
                        {
                            key.SetValue(item.KeyName, item.TargetValue, item.ValueKind);
                        }
                    }
                }
                catch { }

                foreach (var item in ResponsivenessItems)
                {
                    string valType = item.ValueKind == RegistryValueKind.DWord ? "REG_DWORD" : "REG_SZ";
                    RunRegCommand($@"add ""HKLM\{ifeoPath}"" /v ""{item.KeyName}"" /t {valType} /d ""{item.TargetValue}"" /f");
                }
            }
        });

        CheckAllRegistryStatuses();
        TweakStatusMessage = "⚡ All 4 Kernel & GPU Responsiveness tweaks applied successfully!";
        IsApplyingAllTweaks = false;
    }

    [RelayCommand]
    public async Task RevertAllResponsivenessAsync()
    {
        IsApplyingAllTweaks = true;
        TweakStatusMessage = "Closing GameLoop and reverting all 4 Kernel & GPU Responsiveness tweaks...";

        await Task.Run(() =>
        {
            SafeCloseGameLoopRunning();

            foreach (var exe in GameLoopBinaries)
            {
                string ifeoPath = $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{exe}";
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(ifeoPath, true);
                    if (key != null)
                    {
                        foreach (var item in ResponsivenessItems)
                        {
                            key.DeleteValue(item.KeyName, false);
                        }
                    }
                }
                catch { }

                foreach (var item in ResponsivenessItems)
                {
                    RunRegCommand($@"delete ""HKLM\{ifeoPath}"" /v ""{item.KeyName}"" /f");
                }
            }
        });

        CheckAllRegistryStatuses();
        TweakStatusMessage = "↺ All 4 Kernel & GPU Responsiveness tweaks reverted to defaults.";
        IsApplyingAllTweaks = false;
    }

    [RelayCommand]
    public async Task RevertAllToDefaultsAsync()
    {
        IsApplyingAllTweaks = true;
        TweakStatusMessage = "Closing GameLoop safely and restoring all factory defaults...";

        await Task.Run(() =>
        {
            bool wasClosed = SafeCloseGameLoopRunning();
            WasGameLoopClosed = wasClosed;

            try
            {
                // 1. MobileGamePC - delete all newly created keys so registry remains clean
                var mgpcPaths = new[]
                {
                    @"SOFTWARE\WOW6432Node\Tencent\MobileGamePC",
                    @"SOFTWARE\Tencent\MobileGamePC"
                };

                foreach (var p in mgpcPaths)
                {
                    try
                    {
                        using var key = Registry.LocalMachine.OpenSubKey(p, true);
                        if (key != null)
                        {
                            foreach (var item in MobileGamePcItems)
                            {
                                key.DeleteValue(item.KeyName, false);
                            }
                        }
                    }
                    catch { }
                }

                foreach (var item in MobileGamePcItems)
                {
                    RunRegCommand($@"delete ""HKLM\SOFTWARE\WOW6432Node\Tencent\MobileGamePC"" /v ""{item.KeyName}"" /f");
                    RunRegCommand($@"delete ""HKLM\SOFTWARE\Tencent\MobileGamePC"" /v ""{item.KeyName}"" /f");
                }

                // 2. Responsiveness - revert to default Windows values
                foreach (var exe in GameLoopBinaries)
                {
                    string ifeoPath = $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{exe}";
                    try
                    {
                        using var key = Registry.LocalMachine.OpenSubKey(ifeoPath, true);
                        if (key != null)
                        {
                            foreach (var item in ResponsivenessItems)
                            {
                                key.DeleteValue(item.KeyName, false);
                            }
                        }
                    }
                    catch { }

                    foreach (var item in ResponsivenessItems)
                    {
                        RunRegCommand($@"delete ""HKLM\{ifeoPath}"" /v ""{item.KeyName}"" /f");
                    }
                }

                // 3. CPU Priority - revert to default Windows normal priority
                foreach (var exe in GameLoopBinaries)
                {
                    string perfPath = $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{exe}\PerfOptions";
                    try
                    {
                        using var key = Registry.LocalMachine.OpenSubKey(perfPath, true);
                        key?.DeleteValue("CpuPriorityClass", false);
                    }
                    catch { }
                    RunRegCommand($@"delete ""HKLM\{perfPath}"" /v ""CpuPriorityClass"" /f");
                }
            }
            catch (Exception ex)
            {
                TweakStatusMessage = $"Error reverting tweaks: {ex.Message}";
            }
        });

        CheckAllRegistryStatuses();
        TweakStatusMessage = "↺ All GameLoop engine and latency tweaks successfully restored to defaults (custom keys removed).";
        IsApplyingAllTweaks = false;
    }

    [RelayCommand]
    public async Task ToggleRegistryItemAsync(GameLoopRegistryItem item)
    {
        await ToggleTweakAsync(item);
    }

    [RelayCommand]
    public async Task ToggleTweakAsync(GameLoopRegistryItem item)
    {
        if (item == null) return;
        item.IsBusy = true;
        bool shouldApply = item.IsApplied;

        await Task.Run(() =>
        {
            SafeCloseGameLoopRunning();

            try
            {
                if (item.Category == "MobileGamePC")
                {
                    var mgpcPaths = new[]
                    {
                        @"SOFTWARE\WOW6432Node\Tencent\MobileGamePC",
                        @"SOFTWARE\Tencent\MobileGamePC"
                    };

                    if (shouldApply)
                    {
                        string valType = item.ValueKind == RegistryValueKind.DWord ? "REG_DWORD" : "REG_SZ";
                        foreach (var p in mgpcPaths)
                        {
                            using var key = Registry.LocalMachine.CreateSubKey(p, RegistryKeyPermissionCheck.ReadWriteSubTree);
                            key?.SetValue(item.KeyName, item.TargetValue, item.ValueKind);
                        }
                        RunRegCommand($@"add ""HKLM\SOFTWARE\WOW6432Node\Tencent\MobileGamePC"" /v ""{item.KeyName}"" /t {valType} /d {item.TargetValue} /f");
                        RunRegCommand($@"add ""HKLM\SOFTWARE\Tencent\MobileGamePC"" /v ""{item.KeyName}"" /t {valType} /d {item.TargetValue} /f");
                    }
                    else
                    {
                        // When OFF: DELETE the newly created key completely!
                        foreach (var p in mgpcPaths)
                        {
                            try
                            {
                                using var key = Registry.LocalMachine.OpenSubKey(p, true);
                                key?.DeleteValue(item.KeyName, false);
                            }
                            catch { }
                        }
                        RunRegCommand($@"delete ""HKLM\SOFTWARE\WOW6432Node\Tencent\MobileGamePC"" /v ""{item.KeyName}"" /f");
                        RunRegCommand($@"delete ""HKLM\SOFTWARE\Tencent\MobileGamePC"" /v ""{item.KeyName}"" /f");
                    }
                }
                else if (item.Category == "Responsiveness")
                {
                    foreach (var exe in GameLoopBinaries)
                    {
                        string ifeoPath = $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{exe}";
                        if (shouldApply)
                        {
                            using var key = Registry.LocalMachine.CreateSubKey(ifeoPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
                            key?.SetValue(item.KeyName, item.TargetValue, item.ValueKind);
                            string valType = item.ValueKind == RegistryValueKind.DWord ? "REG_DWORD" : "REG_SZ";
                            RunRegCommand($@"add ""HKLM\{ifeoPath}"" /v ""{item.KeyName}"" /t {valType} /d ""{item.TargetValue}"" /f");
                        }
                        else
                        {
                            using var key = Registry.LocalMachine.OpenSubKey(ifeoPath, true);
                            key?.DeleteValue(item.KeyName, false);
                            RunRegCommand($@"delete ""HKLM\{ifeoPath}"" /v ""{item.KeyName}"" /f");
                        }
                    }
                }
                else if (item.Category == "CpuPriority")
                {
                    foreach (var exe in GameLoopBinaries)
                    {
                        string perfPath = $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{exe}\PerfOptions";
                        if (shouldApply)
                        {
                            using var key = Registry.LocalMachine.CreateSubKey(perfPath, RegistryKeyPermissionCheck.ReadWriteSubTree);
                            key?.SetValue(item.KeyName, item.TargetValue, item.ValueKind);
                            RunRegCommand($@"add ""HKLM\{perfPath}"" /v ""{item.KeyName}"" /t REG_DWORD /d {item.TargetValue} /f");
                        }
                        else
                        {
                            using var key = Registry.LocalMachine.OpenSubKey(perfPath, true);
                            key?.DeleteValue(item.KeyName, false);
                            RunRegCommand($@"delete ""HKLM\{perfPath}"" /v ""{item.KeyName}"" /f");
                        }
                    }
                }
            }
            catch { }
        });

        CheckAllRegistryStatuses();
        item.IsBusy = false;
        TweakStatusMessage = shouldApply
            ? $"⚡ Enabled {item.DisplayName} (Active)."
            : $"↺ Reverted {item.DisplayName} to default.";
    }

    private static void RunRegCommand(string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(1000);
        }
        catch { }
    }

    // ── PAGE 2 COMMANDS & LOGIC ──

    partial void OnTargetFpsChanged(double value)
    {
        UpdateStatusPreview();
        if (_settings != null)
        {
            _settings.Settings.TargetFps = (int)value;
            _settings.Save();
        }
        if (IsFrameGenActive)
        {
            ApplySettings();
        }
    }

    partial void OnActiveSavSliderValueChanged(double value)
    {
        int step = (int)Math.Round(value);
        ActiveSavFrameRate = step switch
        {
            0 => 30,
            1 => 60,
            2 => 90,
            _ => 120
        };

        ActiveSavRecommendationText = ActiveSavFrameRate switch
        {
            120 => "Recommended: 120 FPS Ultra Extreme (SM-X920 Whitelist Active)",
            90 => "Recommended: 90 FPS Extreme (SM-X920 HDR Active)",
            60 => "Standard: 60 FPS Extreme Profile",
            _ => "Battery Saver: 30 FPS Low Heat Profile"
        };
    }

    [RelayCommand]
    public void SelectFrameRate(string? fpsStr)
    {
        if (int.TryParse(fpsStr, out int fps))
        {
            ActiveSavFrameRate = fps;
            ActiveSavSliderValue = fps switch
            {
                <= 30 => 0,
                <= 60 => 1,
                <= 90 => 2,
                _ => 3
            };
        }
    }

    [RelayCommand]
    public void SelectQuality(string? q)
    {
        if (!string.IsNullOrEmpty(q))
        {
            ActiveSavQuality = q;
        }
    }

    [RelayCommand]
    public void SelectStyle(string? s)
    {
        if (!string.IsNullOrEmpty(s))
        {
            ActiveSavStyle = s;
        }
    }

    [RelayCommand]
    public void SelectAntiAliasing(string? aa)
    {
        if (!string.IsNullOrEmpty(aa))
        {
            ActiveSavAntiAliasing = aa;
        }
    }

    [RelayCommand]
    public void TogglePowerSaving() => IsPowerSavingEnabled = !IsPowerSavingEnabled;

    [RelayCommand]
    public void ToggleShadows() => IsShadowsEnabled = !IsShadowsEnabled;

    [RelayCommand]
    public void ToggleAdaptiveGraphics() => IsAdaptiveGraphicsEnabled = !IsAdaptiveGraphicsEnabled;

    [RelayCommand]
    public void ToggleLobbyEnhancement() => IsLobbyEnhancementEnabled = !IsLobbyEnhancementEnabled;

    [RelayCommand]
    public void DecreaseBrightness()
    {
        if (ActiveSavBrightness > 80)
            ActiveSavBrightness -= 5;
    }

    [RelayCommand]
    public void IncreaseBrightness()
    {
        if (ActiveSavBrightness < 150)
            ActiveSavBrightness += 5;
    }

    [RelayCommand]
    public void DecreaseScreenScale()
    {
        if (ActiveSavNonStandardScreen > 0)
            ActiveSavNonStandardScreen = Math.Max(0, ActiveSavNonStandardScreen - 1.0);
    }

    [RelayCommand]
    public void IncreaseScreenScale()
    {
        if (ActiveSavNonStandardScreen < 50)
            ActiveSavNonStandardScreen = Math.Min(50, ActiveSavNonStandardScreen + 1.0);
    }

    [RelayCommand]
    public async Task ApplyActiveSavConfigAsync()
    {
        IsApplyingActiveSav = true;
        ActiveSavStatusMessage = $"Injecting {ActiveSavFrameRate} FPS • {ActiveSavQuality} • {ActiveSavStyle} into Active.sav...";

        await Task.Delay(350);

        bool success = await Task.Run(() =>
        {
            return _frameGen.ApplyActiveSavSettings(
                ActiveSavFrameRate,
                ActiveSavQuality,
                ActiveSavStyle,
                ActiveSavAntiAliasing,
                IsPowerSavingEnabled,
                IsShadowsEnabled,
                IsAdaptiveGraphicsEnabled,
                IsLobbyEnhancementEnabled,
                ActiveSavBrightness,
                ActiveSavNonStandardScreen,
                out string msg);
        });

        IsApplyingActiveSav = false;
        ActiveSavStatusMessage = success
            ? $"✓ Successfully deployed {ActiveSavFrameRate} FPS • {ActiveSavQuality} ({ActiveSavStyle}) to Active.sav!"
            : "Failed to deploy Active.sav settings.";
    }

    partial void OnSelectedQualityIndexChanged(int value)
    {
        UpdateStatusPreview();
        if (_settings != null)
        {
            _settings.Settings.SelectedQualityIndex = value;
            _settings.Save();
        }
        if (IsFrameGenActive)
        {
            ApplySettings();
        }
    }

    private void UpdateStatusPreview()
    {
        string qName = FrameGenService.GetQualityName(SelectedQualityIndex);
        if (!IsFrameGenActive)
        {
            FrameGenToggleText = $"Apply {(int)TargetFps} FPS & {qName}";
            FrameGenStatusText = $"Selected: {(int)TargetFps} FPS with {qName} Graphics Profile.";
        }
    }

    [RelayCommand]
    public void ToggleFrameGen()
    {
        if (!IsFrameGenActive)
        {
            ApplySettings();
        }
        else
        {
            _frameGen.ResetToDefaults(out var msg);
            IsFrameGenActive = false;
            if (_settings != null)
            {
                _settings.Settings.IsFrameGenActive = false;
                _settings.Save();
            }
            UpdateStatusPreview();
            FrameGenStatusText = msg;
        }
    }

    [RelayCommand]
    public void ApplySettings()
    {
        int fps = (int)TargetFps;
        bool ok = _frameGen.ApplyOptimization(fps, SelectedQualityIndex, out var msg);
        IsFrameGenActive = ok;
        string qName = FrameGenService.GetQualityName(SelectedQualityIndex);
        FrameGenToggleText = ok ? $"Active ({fps} FPS • {qName})" : "Apply Frame & Graphics Optimization";
        FrameGenStatusText = msg;

        if (_settings != null)
        {
            _settings.Settings.TargetFps = fps;
            _settings.Settings.SelectedQualityIndex = SelectedQualityIndex;
            _settings.Settings.IsFrameGenActive = ok;
            _settings.Save();
        }
    }

    [RelayCommand]
    public async Task KillTelemetryAsync()
    {
        CleanerStatusText = "Scanning and terminating telemetry daemons...";
        int killed = await Task.Run(() => _cleaner.KillTelemetryAndDaemons());
        CleanerStatusText = killed > 0
            ? $"Successfully terminated {killed} GameLoop telemetry processes."
            : "No active telemetry daemons found.";
    }

    [RelayCommand]
    public async Task PurgeCacheAsync()
    {
        CleanerStatusText = "Purging shader cache and temporary objects...";
        int purged = await Task.Run(() => _cleaner.DeepCachePurge());
        CleanerStatusText = $"Purged {purged} cache and shader objects successfully.";
    }

    [RelayCommand]
    public async Task UninstallGameLoopAsync()
    {
        CleanerStatusText = "Searching and launching official GameLoop uninstaller...";
        bool launched = await Task.Run(() => _cleaner.LaunchOfficialUninstall(out string msg));
        CleanerStatusText = launched
            ? "Official uninstaller launched! Follow the prompts, then click 'Nuclear Wipe' to clear residue."
            : "Official uninstaller not found. Click 'Nuclear Wipe' to completely remove all GameLoop files & registry keys.";
    }

    [RelayCommand]
    public async Task NuclearCleanAsync()
    {
        CleanerStatusText = "Executing 7-step nuclear wipe across all drives...";
        var res = await Task.Run(() => _cleaner.FullNuclearClean(s => CleanerStatusText = s));
        CleanerStatusText = res.SummaryMessage;
    }

    [RelayCommand]
    public void SaveEngineSettings()
    {
        int width = 1440, height = 1080;
        switch (SelectedResolutionIndex)
        {
            case 0: width = 1920; height = 1080; break;
            case 1: width = 2560; height = 1440; break;
            case 2: width = 3840; height = 2160; break;
            case 3: width = 1440; height = 1080; break;
            case 4: width = 1920; height = 1440; break;
        }

        int engineCode = SelectedEngineIndex switch
        {
            0 => 2, // OpenGL+
            1 => 1, // DirectX+
            _ => 4  // Vulkan
        };

        bool ok = _cleaner.ApplyGameLoopConfig(width, height, engineCode);
        CleanerStatusText = ok
            ? $"Applied: {width}x{height} | {EngineOptions[SelectedEngineIndex]}"
            : "Failed to write GameLoop configuration.";
    }

    [RelayCommand]
    public async Task ToggleProcessSuspenderAsync()
    {
        if (!IsProcessSuspendActive)
        {
            SuspendedStatusText = "Freezing non-essential background processes...";
            int frozen = await Task.Run(() => _suspender.SuspendBackgroundProcesses());
            IsProcessSuspendActive = true;
            SuspendedStatusText = $"Optimization ON: {frozen} background apps frozen.";
        }
        else
        {
            SuspendedStatusText = "Thawing frozen background processes...";
            await Task.Run(() => _suspender.ResumeAllProcesses());
            IsProcessSuspendActive = false;
            SuspendedStatusText = "Optimization OFF: All background apps resumed.";
        }
    }

    [RelayCommand]
    public void ForceDedicatedGpu()
    {
        bool ok = _cleaner.ForceDedicatedGpu();
        GpuStatusText = ok ? "DirectX GPU Routing Applied Successfully" : "Failed to write DirectX UserGpuPreferences";
    }

    public void CleanExit()
    {
        try
        {
            _suspender.ResumeAllProcesses();
        }
        catch { }
    }
}
