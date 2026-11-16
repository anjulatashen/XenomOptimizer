using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using XenomOptimizer.Converters;
using XenomOptimizer.ViewModels;

namespace XenomOptimizer.Views;

public partial class DashboardView : UserControl
{
    private static readonly ArchPathDashConverter _archConverter = new();
    private static readonly UsageToBrushConverter _usageBrushConverter = new();

    public static readonly DependencyProperty AnimationProgressProperty =
        DependencyProperty.Register(
            nameof(AnimationProgress),
            typeof(double),
            typeof(DashboardView),
            new PropertyMetadata(0.0, OnAnimationProgressChanged));

    public double AnimationProgress
    {
        get => (double)GetValue(AnimationProgressProperty);
        set => SetValue(AnimationProgressProperty, value);
    }

    private static void OnAnimationProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DashboardView view && e.NewValue is double progress)
        {
            view.ApplyProgress(progress);
        }
    }

    private double _targetCpu;
    private double _targetRam;
    private double _targetGpu;
    private double _targetScore;
    private double _targetLagFix;

    private bool _isAnimating;
    private DateTime _lastAnimationStartTime = DateTime.MinValue;
    private DashboardViewModel? _subscribedVm;

    public DashboardView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HookViewModel(DataContext as DashboardViewModel);
        Dispatcher.InvokeAsync(TriggerEntranceAnimation, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_isAnimating)
        {
            BeginAnimation(AnimationProgressProperty, null);
            _isAnimating = false;
            RestoreLiveBindings();
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        HookViewModel(e.NewValue as DashboardViewModel);
        if (IsLoaded)
        {
            Dispatcher.InvokeAsync(TriggerEntranceAnimation, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void HookViewModel(DashboardViewModel? vm)
    {
        if (_subscribedVm == vm) return;

        if (_subscribedVm != null)
        {
            _subscribedVm.RequestEntranceAnimation -= OnRequestEntranceAnimation;
        }

        _subscribedVm = vm;

        if (_subscribedVm != null)
        {
            _subscribedVm.RequestEntranceAnimation += OnRequestEntranceAnimation;
        }
    }

    private void OnRequestEntranceAnimation()
    {
        Dispatcher.InvokeAsync(TriggerEntranceAnimation, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void TriggerEntranceAnimation()
    {
        if (DataContext is not DashboardViewModel vm) return;
        if (CpuUsageText == null || CpuProgressBar == null) return;

        // If telemetry stats haven't arrived yet on initial launch, wait for data to load
        if (vm.CpuUsage <= 0 && vm.RamUsage <= 0 && vm.StorageDrives.Count == 0 && vm.GameLoopScore <= 0)
        {
            return;
        }

        // If currently animating, do NOT abort the running animation!
        // Seamlessly update targets so motion finishes cleanly.
        if (_isAnimating)
        {
            _targetCpu = vm.CpuUsage;
            _targetRam = vm.RamUsage;
            _targetGpu = vm.GpuUsage;
            _targetScore = vm.GameLoopScore;
            _targetLagFix = vm.LagFixProgressPercent;

            foreach (var drive in vm.StorageDrives)
            {
                if (drive.TargetUsagePercent <= 0 && drive.UsagePercent > 0)
                {
                    drive.TargetUsagePercent = drive.UsagePercent;
                }
            }
            return;
        }

        // Debounce if multiple triggers occur in quick succession
        if ((DateTime.UtcNow - _lastAnimationStartTime).TotalMilliseconds < 300)
            return;

        _lastAnimationStartTime = DateTime.UtcNow;

        _targetCpu = vm.CpuUsage;
        _targetRam = vm.RamUsage;
        _targetGpu = vm.GpuUsage;
        _targetScore = vm.GameLoopScore;
        _targetLagFix = vm.LagFixProgressPercent;

        // Fallback for LagFix in case it was 0 while applied count is positive
        if (_targetLagFix <= 0 && vm.LagFixTotalCount > 0 && vm.LagFixAppliedCount > 0)
        {
            _targetLagFix = Math.Round((vm.LagFixAppliedCount / (double)vm.LagFixTotalCount) * 100);
            vm.LagFixProgressPercent = _targetLagFix;
        }

        // Ensure target percentage is retained for all storage drives
        foreach (var drive in vm.StorageDrives)
        {
            if (drive.TargetUsagePercent <= 0 && drive.UsagePercent > 0)
            {
                drive.TargetUsagePercent = drive.UsagePercent;
            }
        }

        // Cancel any active animation
        BeginAnimation(AnimationProgressProperty, null);
        SetValue(AnimationProgressProperty, 0.0);
        _isAnimating = true;

        // Reset to initial 0 state
        ApplyProgress(0.0);

        var anim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(850))
        {
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };

        anim.Completed += (s, e) =>
        {
            _isAnimating = false;
            RestoreLiveBindings();
        };

        BeginAnimation(AnimationProgressProperty, anim);
    }

    private void ApplyProgress(double p)
    {
        if (DataContext is not DashboardViewModel vm) return;

        // 1. CPU
        double cpuVal = _targetCpu * p;
        if (CpuUsageText != null)
            CpuUsageText.Text = $"{(int)Math.Round(cpuVal)}%";
        if (CpuProgressBar != null)
        {
            CpuProgressBar.Value = cpuVal;
            CpuProgressBar.Foreground = (Brush)_usageBrushConverter.Convert(cpuVal, typeof(Brush), null!, CultureInfo.InvariantCulture);
        }

        // 2. RAM
        double ramVal = _targetRam * p;
        if (RamUsageText != null)
            RamUsageText.Text = $"{(int)Math.Round(ramVal)}%";
        if (RamProgressBar != null)
        {
            RamProgressBar.Value = ramVal;
            RamProgressBar.Foreground = (Brush)_usageBrushConverter.Convert(ramVal, typeof(Brush), null!, CultureInfo.InvariantCulture);
        }

        // 3. GPU
        double gpuVal = _targetGpu * p;
        if (GpuUsageText != null)
            GpuUsageText.Text = $"{(int)Math.Round(gpuVal)}%";
        if (GpuProgressBar != null)
        {
            GpuProgressBar.Value = gpuVal;
            GpuProgressBar.Foreground = (Brush)_usageBrushConverter.Convert(gpuVal, typeof(Brush), null!, CultureInfo.InvariantCulture);
        }

        // 4. Storage drives mini bars
        foreach (var drive in vm.StorageDrives)
        {
            drive.UsagePercent = drive.TargetUsagePercent * p;
        }

        // 5. Gaming Requirements Arc & Score Text
        double scoreVal = _targetScore * p;
        if (ArchScoreText != null)
            ArchScoreText.Text = $"{(int)Math.Round(scoreVal)}%";
        if (ArchProgressPath != null)
            ArchProgressPath.StrokeDashArray = (DoubleCollection)_archConverter.Convert(scoreVal, typeof(DoubleCollection), null!, CultureInfo.InvariantCulture);

        // 6. Lag Fix Plan Bar & Text
        double lagFixVal = _targetLagFix * p;
        if (LagFixProgressTextBlock != null)
            LagFixProgressTextBlock.Text = $"{(int)Math.Round(lagFixVal)}% completed";
        if (LagFixProgressBar != null)
            LagFixProgressBar.Value = lagFixVal;
    }

    private void RestoreLiveBindings()
    {
        if (DataContext is not DashboardViewModel vm) return;

        // Restore CPU
        if (CpuUsageText != null)
            CpuUsageText.SetBinding(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.CpuUsage))
            {
                Source = vm,
                StringFormat = "{0:F0}%",
                Mode = BindingMode.OneWay
            });
        if (CpuProgressBar != null)
        {
            CpuProgressBar.SetBinding(ProgressBar.ValueProperty, new Binding(nameof(DashboardViewModel.CpuUsage))
            {
                Source = vm,
                Mode = BindingMode.OneWay
            });
            CpuProgressBar.SetBinding(ProgressBar.ForegroundProperty, new Binding(nameof(DashboardViewModel.CpuUsage))
            {
                Source = vm,
                Converter = _usageBrushConverter,
                Mode = BindingMode.OneWay
            });
        }

        // Restore RAM
        if (RamUsageText != null)
            RamUsageText.SetBinding(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.RamUsage))
            {
                Source = vm,
                StringFormat = "{0:F0}%",
                Mode = BindingMode.OneWay
            });
        if (RamProgressBar != null)
        {
            RamProgressBar.SetBinding(ProgressBar.ValueProperty, new Binding(nameof(DashboardViewModel.RamUsage))
            {
                Source = vm,
                Mode = BindingMode.OneWay
            });
            RamProgressBar.SetBinding(ProgressBar.ForegroundProperty, new Binding(nameof(DashboardViewModel.RamUsage))
            {
                Source = vm,
                Converter = _usageBrushConverter,
                Mode = BindingMode.OneWay
            });
        }

        // Restore GPU
        if (GpuUsageText != null)
            GpuUsageText.SetBinding(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.GpuUsage))
            {
                Source = vm,
                StringFormat = "{0:F0}%",
                Mode = BindingMode.OneWay
            });
        if (GpuProgressBar != null)
        {
            GpuProgressBar.SetBinding(ProgressBar.ValueProperty, new Binding(nameof(DashboardViewModel.GpuUsage))
            {
                Source = vm,
                Mode = BindingMode.OneWay
            });
            GpuProgressBar.SetBinding(ProgressBar.ForegroundProperty, new Binding(nameof(DashboardViewModel.GpuUsage))
            {
                Source = vm,
                Converter = _usageBrushConverter,
                Mode = BindingMode.OneWay
            });
        }

        // Restore Storage drives
        foreach (var drive in vm.StorageDrives)
        {
            drive.UsagePercent = drive.TargetUsagePercent;
        }

        // Restore Gaming Requirements
        if (ArchScoreText != null)
            ArchScoreText.SetBinding(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.GameLoopScore))
            {
                Source = vm,
                StringFormat = "{0:F0}%",
                Mode = BindingMode.OneWay
            });
        if (ArchProgressPath != null)
            ArchProgressPath.SetBinding(Shape.StrokeDashArrayProperty, new Binding(nameof(DashboardViewModel.GameLoopScore))
            {
                Source = vm,
                Converter = _archConverter,
                Mode = BindingMode.OneWay
            });

        // Restore Lag Fix Plan
        if (LagFixProgressTextBlock != null)
            LagFixProgressTextBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.LagFixProgressText))
            {
                Source = vm,
                Mode = BindingMode.OneWay
            });
        if (LagFixProgressBar != null)
            LagFixProgressBar.SetBinding(ProgressBar.ValueProperty, new Binding(nameof(DashboardViewModel.LagFixProgressPercent))
            {
                Source = vm,
                Mode = BindingMode.OneWay
            });
    }
}
