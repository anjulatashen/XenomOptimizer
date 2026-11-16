using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace XenomOptimizer.Converters;

/// <summary>
/// Modern Windows 11 Fluent Auto-Hide Behavior for ScrollBars.
/// Shows scrollbar smoothly on scroll/hover and automatically fades it out after 1.6s of inactivity.
/// </summary>
public static class ScrollBarAutoHideHelper
{
    public static readonly DependencyProperty AutoHideProperty =
        DependencyProperty.RegisterAttached(
            "AutoHide",
            typeof(bool),
            typeof(ScrollBarAutoHideHelper),
            new PropertyMetadata(false, OnAutoHideChanged));

    public static bool GetAutoHide(DependencyObject obj) => (bool)obj.GetValue(AutoHideProperty);
    public static void SetAutoHide(DependencyObject obj, bool value) => obj.SetValue(AutoHideProperty, value);

    private static readonly DependencyProperty TimerProperty =
        DependencyProperty.RegisterAttached("Timer", typeof(DispatcherTimer), typeof(ScrollBarAutoHideHelper));

    private static void OnAutoHideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollBar sb)
        {
            if ((bool)e.NewValue)
            {
                sb.Loaded += OnScrollBarLoaded;
                sb.Unloaded += OnScrollBarUnloaded;
                if (sb.IsLoaded)
                {
                    AttachToScrollBar(sb);
                }
            }
            else
            {
                sb.Loaded -= OnScrollBarLoaded;
                sb.Unloaded -= OnScrollBarUnloaded;
                DetachFromScrollBar(sb);
            }
        }
    }

    private static void OnScrollBarLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollBar sb)
        {
            AttachToScrollBar(sb);
        }
    }

    private static void OnScrollBarUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollBar sb)
        {
            DetachFromScrollBar(sb);
        }
    }

    private static readonly DependencyProperty ParentScrollViewerProperty =
        DependencyProperty.RegisterAttached("ParentScrollViewer", typeof(ScrollViewer), typeof(ScrollBarAutoHideHelper));

    private const int DefaultHideDelayMs = 900;

    private static void AttachToScrollBar(ScrollBar sb)
    {
        sb.ValueChanged -= OnScrollBarValueChanged;
        sb.ValueChanged += OnScrollBarValueChanged;

        sb.MouseEnter -= OnScrollBarMouseEnter;
        sb.MouseEnter += OnScrollBarMouseEnter;

        sb.MouseMove -= OnScrollBarMouseMove;
        sb.MouseMove += OnScrollBarMouseMove;

        sb.MouseLeave -= OnScrollBarMouseLeave;
        sb.MouseLeave += OnScrollBarMouseLeave;

        sb.PreviewMouseDown -= OnScrollBarPreviewMouseDown;
        sb.PreviewMouseDown += OnScrollBarPreviewMouseDown;

        sb.PreviewMouseUp -= OnScrollBarPreviewMouseUp;
        sb.PreviewMouseUp += OnScrollBarPreviewMouseUp;

        // Auto-hide inactivity timer (Reduced to 900ms after scrolling stops)
        var timer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(DefaultHideDelayMs)
        };
        timer.Tick += (s, ev) =>
        {
            timer.Stop();
            // Automatically fade out when user stops scrolling unless directly hovering/dragging the thumb
            if (!sb.IsMouseOver && !sb.IsMouseCaptured)
            {
                FadeOut(sb);
            }
        };
        sb.SetValue(TimerProperty, timer);

        // Listen for parent ScrollViewer events (hover, mouse move, scroll wheel, and scroll position change)
        void HookParentScrollViewer()
        {
            var sv = FindVisualAncestor<ScrollViewer>(sb);
            if (sv != null)
            {
                sb.SetValue(ParentScrollViewerProperty, sv);

                sv.ScrollChanged -= OnScrollViewerScrollChanged;
                sv.ScrollChanged += OnScrollViewerScrollChanged;

                sv.MouseEnter -= OnScrollViewerMouseEnter;
                sv.MouseEnter += OnScrollViewerMouseEnter;

                sv.MouseMove -= OnScrollViewerMouseMove;
                sv.MouseMove += OnScrollViewerMouseMove;

                sv.PreviewMouseWheel -= OnScrollViewerPreviewMouseWheel;
                sv.PreviewMouseWheel += OnScrollViewerPreviewMouseWheel;

                sv.MouseLeave -= OnScrollViewerMouseLeave;
                sv.MouseLeave += OnScrollViewerMouseLeave;
            }
        }

        if (sb.IsLoaded)
        {
            HookParentScrollViewer();
        }
        else
        {
            RoutedEventHandler? loadedHandler = null;
            loadedHandler = (s, e) =>
            {
                sb.Loaded -= loadedHandler;
                HookParentScrollViewer();
            };
            sb.Loaded += loadedHandler;
        }

        // Initially show the scrollbar on load so user immediately sees that content is scrollable!
        sb.Opacity = 1.0;
        ScheduleHide(sb);
    }

    private static void DetachFromScrollBar(ScrollBar sb)
    {
        sb.ValueChanged -= OnScrollBarValueChanged;
        sb.MouseEnter -= OnScrollBarMouseEnter;
        sb.MouseMove -= OnScrollBarMouseMove;
        sb.MouseLeave -= OnScrollBarMouseLeave;
        sb.PreviewMouseDown -= OnScrollBarPreviewMouseDown;
        sb.PreviewMouseUp -= OnScrollBarPreviewMouseUp;

        if (sb.GetValue(ParentScrollViewerProperty) is ScrollViewer sv)
        {
            sv.ScrollChanged -= OnScrollViewerScrollChanged;
            sv.MouseEnter -= OnScrollViewerMouseEnter;
            sv.MouseMove -= OnScrollViewerMouseMove;
            sv.PreviewMouseWheel -= OnScrollViewerPreviewMouseWheel;
            sv.MouseLeave -= OnScrollViewerMouseLeave;
            sb.ClearValue(ParentScrollViewerProperty);
        }

        if (sb.GetValue(TimerProperty) is DispatcherTimer timer)
        {
            timer.Stop();
            sb.ClearValue(TimerProperty);
        }
    }

    private static void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer sv && (Math.Abs(e.VerticalChange) > 0.001 || Math.Abs(e.HorizontalChange) > 0.001))
        {
            var sb = FindChild<ScrollBar>(sv, "PART_VerticalScrollBar");
            if (sb != null) ShowAndScheduleHide(sb);
        }
    }

    private static void OnScrollViewerMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            var sb = FindChild<ScrollBar>(sv, "PART_VerticalScrollBar");
            if (sb != null) ShowAndScheduleHide(sb);
        }
    }

    private static void OnScrollViewerMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            // Only wake up scrollbar on mouse move if cursor is within 40px of right edge (near scrollbar gutter)
            var pos = e.GetPosition(sv);
            if (pos.X >= sv.ActualWidth - 40)
            {
                var sb = FindChild<ScrollBar>(sv, "PART_VerticalScrollBar");
                if (sb != null) ShowAndScheduleHide(sb);
            }
        }
    }

    private static void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            var sb = FindChild<ScrollBar>(sv, "PART_VerticalScrollBar");
            if (sb != null) ShowAndScheduleHide(sb);
        }
    }

    private static void OnScrollViewerMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            var sb = FindChild<ScrollBar>(sv, "PART_VerticalScrollBar");
            if (sb != null && !sb.IsMouseOver) ScheduleHide(sb, 350);
        }
    }

    private static T? FindChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
    {
        if (parent == null) return null;
        int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild && typedChild.Name == childName)
                return typedChild;
            var found = FindChild<T>(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    private static void OnScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is ScrollBar sb)
        {
            ShowAndScheduleHide(sb);
        }
    }

    private static void OnScrollBarMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is ScrollBar sb)
        {
            ShowAndCancelHide(sb);
        }
    }

    private static void OnScrollBarMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is ScrollBar sb)
        {
            ShowAndCancelHide(sb);
        }
    }

    private static void OnScrollBarMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is ScrollBar sb)
        {
            ScheduleHide(sb, 400);
        }
    }

    private static void OnScrollBarPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ScrollBar sb)
        {
            ShowAndCancelHide(sb);
        }
    }

    private static void OnScrollBarPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ScrollBar sb)
        {
            ScheduleHide(sb);
        }
    }

    private static void ShowAndScheduleHide(ScrollBar sb, int delayMs = DefaultHideDelayMs)
    {
        FadeIn(sb);
        ScheduleHide(sb, delayMs);
    }

    private static void ShowAndCancelHide(ScrollBar sb)
    {
        FadeIn(sb);
        if (sb.GetValue(TimerProperty) is DispatcherTimer timer)
        {
            timer.Stop();
        }
    }

    private static void ScheduleHide(ScrollBar sb, int delayMs = DefaultHideDelayMs)
    {
        if (sb.GetValue(TimerProperty) is DispatcherTimer timer)
        {
            timer.Stop();
            timer.Interval = TimeSpan.FromMilliseconds(delayMs);
            timer.Start();
        }
    }

    private static void FadeIn(ScrollBar sb)
    {
        if (sb.Opacity >= 0.99) return;

        var anim = new DoubleAnimation
        {
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(130),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        sb.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private static void FadeOut(ScrollBar sb)
    {
        if (sb.Opacity <= 0.01) return;

        var anim = new DoubleAnimation
        {
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(240),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        sb.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private static T? FindVisualAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}

