using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

public partial class OptimizerFrameGenViewModel : ObservableObject
{
    private readonly FrameGenService _frameGen = new();
    private readonly ProcessSuspenderService _suspender = new();
    private readonly GameLoopCleanerService _cleaner = new();

    public FrameGenService FrameGen => _frameGen;
    public ProcessSuspenderService Suspender => _suspender;
    public GameLoopCleanerService Cleaner => _cleaner;

    [ObservableProperty]
    private double _targetFps = 90;

    [ObservableProperty]
    private bool _isFrameGenActive;

    [ObservableProperty]
    private string _frameGenToggleText = "Enable Frame Generation";

    [ObservableProperty]
    private string _cleanerStatusText = "Ready to clean telemetry and cache files.";

    [ObservableProperty]
    private int _selectedResolutionIndex = 3; // 1440x1080 4:3 iPad View by default

    [ObservableProperty]
    private int _selectedEngineIndex = 0; // DirectX+ by default

    [ObservableProperty]
    private bool _isProcessSuspendActive;

    [ObservableProperty]
    private string _suspendedStatusText = "0 background apps frozen.";

    [ObservableProperty]
    private string _gpuStatusText = "Dedicated High-Performance GPU Active";

    public ObservableCollection<string> ResolutionOptions { get; } = new()
    {
        "1920x1080 (16:9 Full HD)",
        "2560x1440 (16:9 2K QHD)",
        "3840x2160 (16:9 4K UHD)",
        "1440x1080 (4:3 iPad View)",
        "1920x1440 (4:3 Pro Ultra)"
    };

    public ObservableCollection<string> EngineOptions { get; } = new()
    {
        "DirectX+",
        "OpenGL+",
        "Vulkan"
    };

    public OptimizerFrameGenViewModel()
    {
        // Apply GPU route on init silently
        _cleaner.ForceDedicatedGpu();
    }

    partial void OnTargetFpsChanged(double value)
    {
        if (IsFrameGenActive)
        {
            _frameGen.StartFrameGeneration((int)value);
            FrameGenToggleText = $"Active ({(int)value} FPS)";
        }
    }

    [RelayCommand]
    public void ToggleFrameGen()
    {
        if (!IsFrameGenActive)
        {
            int fps = (int)TargetFps;
            _frameGen.StartFrameGeneration(fps);
            IsFrameGenActive = true;
            FrameGenToggleText = $"Active ({fps} FPS)";
        }
        else
        {
            _frameGen.StopFrameGeneration();
            IsFrameGenActive = false;
            FrameGenToggleText = "Enable Frame Generation";
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
            0 => 0, // DirectX+
            1 => 1, // OpenGL+
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
            await Task.Run(() => _suspender.ResumeAllProcesses());
            IsProcessSuspendActive = false;
            SuspendedStatusText = "All background processes resumed.";
        }
    }

    [RelayCommand]
    public void ForceDedicatedGpu()
    {
        bool ok = _cleaner.ForceDedicatedGpu();
        GpuStatusText = ok
            ? "Dedicated High-Performance GPU Assigned"
            : "Registry write error.";
    }

    public void CleanExit()
    {
        // Safe exit: resume all frozen apps & stop frame gen
        _suspender.ResumeAllProcesses();
        _frameGen.StopFrameGeneration();
    }
}
