using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using XenomOptimizer.ViewModels;

namespace XenomOptimizer.Views;

public partial class DownloadsModalControl : UserControl
{
    public static readonly DependencyProperty AnimatedVerticalOffsetProperty =
        DependencyProperty.RegisterAttached(
            "AnimatedVerticalOffset",
            typeof(double),
            typeof(DownloadsModalControl),
            new PropertyMetadata(0.0, OnAnimatedVerticalOffsetChanged));

    private static void OnAnimatedVerticalOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer sv)
        {
            sv.ScrollToVerticalOffset((double)e.NewValue);
        }
    }

    private WindowsOptimizationViewModel? _subscribedVm;

    public DownloadsModalControl()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedVm != null)
        {
            _subscribedVm.RequestScrollToDownloadItem -= OnRequestScrollToDownloadItem;
        }

        if (e.NewValue is WindowsOptimizationViewModel vm)
        {
            _subscribedVm = vm;
            _subscribedVm.RequestScrollToDownloadItem += OnRequestScrollToDownloadItem;
        }
        else
        {
            _subscribedVm = null;
        }
    }

    private void OnRequestScrollToDownloadItem(DownloadHistoryItemViewModel item)
    {
        Dispatcher.InvokeAsync(async () =>
        {
            await System.Threading.Tasks.Task.Yield();
            ScrollItemIntoView(item);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void ScrollItemIntoView(DownloadHistoryItemViewModel item)
    {
        if (CompletedDownloadsScrollViewer == null) return;

        double viewport = CompletedDownloadsScrollViewer.ViewportHeight;
        double scrollable = CompletedDownloadsScrollViewer.ScrollableHeight;
        if (scrollable <= 0.0) return;

        double targetOffset = CompletedDownloadsScrollViewer.VerticalOffset;
        FrameworkElement? container = null;
        if (CompletedDownloadsItemsControl != null)
        {
            container = CompletedDownloadsItemsControl.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
        }

        if (container != null && container.IsLoaded)
        {
            try
            {
                var transform = container.TransformToAncestor(CompletedDownloadsScrollViewer);
                Point pt = transform.Transform(new Point(0, 0));
                double itemTop = pt.Y;
                double itemBottom = pt.Y + container.ActualHeight;

                // If already comfortably in view with safe margins, no scroll needed
                if (itemTop >= 10.0 && itemBottom <= viewport - 15.0)
                {
                    return;
                }

                if (itemBottom > viewport - 15.0)
                {
                    // Item is below bottom of view: scroll down with extra 30px breathing room
                    double delta = itemBottom - viewport + 30.0;
                    targetOffset = Math.Min(scrollable, CompletedDownloadsScrollViewer.VerticalOffset + delta);
                }
                else if (itemTop < 10.0)
                {
                    // Item is above top of view: scroll up
                    double delta = itemTop - 15.0;
                    targetOffset = Math.Max(0.0, CompletedDownloadsScrollViewer.VerticalOffset + delta);
                }
            }
            catch
            {
                container = null;
            }
        }

        if (container == null)
        {
            // Fallback estimation by index in collection
            if (_subscribedVm != null)
            {
                int index = _subscribedVm.CompletedDownloads.IndexOf(item);
                if (index >= 0)
                {
                    double estimatedTop = index * 88.0;
                    double estimatedBottom = estimatedTop + 88.0;

                    if (estimatedBottom > CompletedDownloadsScrollViewer.VerticalOffset + viewport - 15.0)
                    {
                        targetOffset = Math.Min(scrollable, estimatedBottom - viewport + 35.0);
                    }
                    else if (estimatedTop < CompletedDownloadsScrollViewer.VerticalOffset + 10.0)
                    {
                        targetOffset = Math.Max(0.0, estimatedTop - 15.0);
                    }
                }
            }
        }

        AnimateScroll(targetOffset);
    }

    private void AnimateScroll(double targetOffset)
    {
        if (CompletedDownloadsScrollViewer == null) return;

        double currentOffset = CompletedDownloadsScrollViewer.VerticalOffset;
        if (Math.Abs(targetOffset - currentOffset) < 1.0)
            return;

        CompletedDownloadsScrollViewer.SetValue(AnimatedVerticalOffsetProperty, currentOffset);

        var anim = new DoubleAnimation
        {
            From = currentOffset,
            To = targetOffset,
            Duration = TimeSpan.FromMilliseconds(420),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };

        anim.Completed += (s, e) =>
        {
            CompletedDownloadsScrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, null);
            CompletedDownloadsScrollViewer.ScrollToVerticalOffset(targetOffset);
            UpdateScrollFade();
        };

        CompletedDownloadsScrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, anim);
    }

    private void CompletedDownloadsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Cancel any active smooth scroll animation when user manually touches the mouse wheel
        CompletedDownloadsScrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, null);
    }

    private Border? _scrollMaskBorder;
    private static readonly System.Windows.Media.LinearGradientBrush _defaultFadeBrush = CreateDefaultFadeBrush();

    private static System.Windows.Media.LinearGradientBrush CreateDefaultFadeBrush()
    {
        var brush = new System.Windows.Media.LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = new System.Windows.Media.GradientStopCollection
            {
                new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromArgb(255, 0, 0, 0), 0.0),
                new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromArgb(255, 0, 0, 0), 0.90),
                new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromArgb(0, 0, 0, 0), 1.0)
            }
        };
        brush.Freeze();
        return brush;
    }

    private void CompletedDownloadsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateScrollFade();
    }

    public void UpdateScrollFade()
    {
        if (CompletedDownloadsScrollViewer == null) return;

        if (_scrollMaskBorder == null && CompletedDownloadsScrollViewer.Template != null)
        {
            _scrollMaskBorder = CompletedDownloadsScrollViewer.Template.FindName("PART_ContentMaskBorder", CompletedDownloadsScrollViewer) as Border;
        }

        if (_scrollMaskBorder == null) return;

        double scrollable = CompletedDownloadsScrollViewer.ScrollableHeight;
        double offset = CompletedDownloadsScrollViewer.VerticalOffset;

        // When all items fit without scrolling, or when user has scrolled to the bottom:
        // REMOVE the fade completely so the last item is 100% solid, crisp, and never faded!
        if (scrollable <= 1.0 || offset >= scrollable - 2.0)
        {
            _scrollMaskBorder.OpacityMask = null;
            return;
        }

        // Distance from the bottom of the scroll list
        double remaining = scrollable - offset;

        // Smoothly fade the mask out as the user reaches the bottom (within the last 40 pixels)
        if (remaining < 40.0)
        {
            double factor = Math.Max(0.0, Math.Min(1.0, remaining / 40.0));
            double stopOffset = 0.90 + 0.10 * (1.0 - factor);
            byte bottomAlpha = (byte)(255 * (1.0 - factor));

            var smoothBrush = new System.Windows.Media.LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new System.Windows.Media.GradientStopCollection
                {
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromArgb(255, 0, 0, 0), 0.0),
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromArgb(255, 0, 0, 0), stopOffset),
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromArgb(bottomAlpha, 0, 0, 0), 1.0)
                }
            };
            smoothBrush.Freeze();
            _scrollMaskBorder.OpacityMask = smoothBrush;
        }
        else
        {
            _scrollMaskBorder.OpacityMask = _defaultFadeBrush;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (MultiCompletedView != null)
        {
            MultiCompletedView.IsVisibleChanged += MultiCompletedView_IsVisibleChanged;
        }
        Dispatcher.InvokeAsync(UpdateScrollFade, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void MultiCompletedView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && MultiCompletedView != null && MultiCompletedScale != null)
        {
            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            MultiCompletedView.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            var bounce = new DoubleAnimation(0.92, 1.0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 }
            };
            MultiCompletedScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, bounce);
            MultiCompletedScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, bounce);
            Dispatcher.InvokeAsync(UpdateScrollFade, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void ItemCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DownloadHistoryItemViewModel item)
        {
            item.ToggleActionsCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ItemMoreBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(UIElement.OpacityProperty, fadeIn);

            var scaleAnim = new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            ModalCardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
            ModalCardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
            Dispatcher.InvokeAsync(UpdateScrollFade, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void Backdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WindowsOptimizationViewModel vm)
        {
            vm.IsDownloadFlyoutOpen = false;
        }
        else if (DataContext is MainViewModel mvm)
        {
            mvm.WindowsVm.IsDownloadFlyoutOpen = false;
        }
    }
}
