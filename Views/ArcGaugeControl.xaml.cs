using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace XenomOptimizer.Views;

public partial class ArcGaugeControl : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(ArcGaugeControl),
            new PropertyMetadata(0.0, OnValueChanged));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(
            nameof(Subtitle),
            typeof(string),
            typeof(ArcGaugeControl),
            new PropertyMetadata("LOAD", OnSubtitleChanged));

    public static readonly DependencyProperty ArcBrushProperty =
        DependencyProperty.Register(
            nameof(ArcBrush),
            typeof(Brush),
            typeof(ArcGaugeControl),
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00D4FF")), OnArcBrushChanged));

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(
            nameof(TrackBrush),
            typeof(Brush),
            typeof(ArcGaugeControl),
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D212D")), OnTrackBrushChanged));

    public static readonly DependencyProperty IsFullCircleProperty =
        DependencyProperty.Register(
            nameof(IsFullCircle),
            typeof(bool),
            typeof(ArcGaugeControl),
            new PropertyMetadata(false, OnGeometryChanged));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public Brush ArcBrush
    {
        get => (Brush)GetValue(ArcBrushProperty);
        set => SetValue(ArcBrushProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public bool IsFullCircle
    {
        get => (bool)GetValue(IsFullCircleProperty);
        set => SetValue(IsFullCircleProperty, value);
    }

    private double _currentSweepAngle = 0.0;

    public ArcGaugeControl()
    {
        InitializeComponent();
        Loaded += (s, e) => Redraw(animate: false);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGaugeControl control)
        {
            control.UpdateValue((double)e.NewValue);
        }
    }

    private static void OnSubtitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGaugeControl control && control.SubtitleTextBlock != null)
        {
            control.SubtitleTextBlock.Text = e.NewValue?.ToString() ?? string.Empty;
        }
    }

    private static void OnArcBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGaugeControl control && control.ProgressPath != null)
        {
            if (e.NewValue is Brush brush)
            {
                control.ProgressPath.Stroke = brush;
                if (brush is SolidColorBrush scb && control.GlowEffect != null)
                {
                    control.GlowEffect.Color = scb.Color;
                }
            }
        }
    }

    private static void OnTrackBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGaugeControl control && control.TrackPath != null && e.NewValue is Brush brush)
        {
            control.TrackPath.Stroke = brush;
        }
    }

    private static void OnGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ArcGaugeControl control)
        {
            control.Redraw(animate: false);
        }
    }

    private void UpdateValue(double newValue)
    {
        double clamped = Math.Clamp(newValue, 0.0, 100.0);
        if (ValueTextBlock != null)
        {
            ValueTextBlock.Text = string.Format("{0:F0}%", clamped);
        }

        double maxSweep = IsFullCircle ? 359.99 : 240.0;
        double targetSweep = (clamped / 100.0) * maxSweep;

        // Smoothly animate the arc
        var anim = new DoubleAnimation
        {
            From = _currentSweepAngle,
            To = targetSweep,
            Duration = TimeSpan.FromMilliseconds(380),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        anim.Changed += (s, e) =>
        {
            // Fallback if needed
        };

        // Custom animation step
        AnimateSweepAngle(_currentSweepAngle, targetSweep);
    }

    private void AnimateSweepAngle(double fromAngle, double toAngle)
    {
        var anim = new DoubleAnimation
        {
            From = fromAngle,
            To = toAngle,
            Duration = TimeSpan.FromMilliseconds(350),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var clock = anim.CreateClock();
        clock.CurrentTimeInvalidated += (s, e) =>
        {
            if (clock.CurrentProgress.HasValue)
            {
                double prog = clock.CurrentProgress.Value;
                // Cubic ease out formula
                double eased = 1.0 - Math.Pow(1.0 - prog, 3);
                _currentSweepAngle = fromAngle + (toAngle - fromAngle) * eased;
                DrawProgressArc(_currentSweepAngle);
            }
        };
        clock.Completed += (s, e) =>
        {
            _currentSweepAngle = toAngle;
            DrawProgressArc(_currentSweepAngle);
        };
    }

    private void Redraw(bool animate)
    {
        if (TrackPath == null || ProgressPath == null) return;

        double centerAngle = IsFullCircle ? -90.0 : 150.0;
        double totalTrackSweep = IsFullCircle ? 359.99 : 240.0;

        double cx = IsFullCircle ? 40.0 : 74.0;
        double cy = IsFullCircle ? 40.0 : 60.0;
        double r = IsFullCircle ? 30.0 : 46.0;

        if (IsFullCircle)
        {
            GaugeCanvas.Width = 80;
            GaugeCanvas.Height = 80;
            TrackPath.StrokeThickness = 6.5;
            ProgressPath.StrokeThickness = 6.5;
            CenterContentPanel.Margin = new Thickness(0, 0, 0, 0);
            ValueTextBlock.FontSize = 17;
            SubtitleTextBlock.Visibility = Visibility.Collapsed;
        }
        else
        {
            GaugeCanvas.Width = 148;
            GaugeCanvas.Height = 110;
            TrackPath.StrokeThickness = 9.5;
            ProgressPath.StrokeThickness = 9.5;
            CenterContentPanel.Margin = new Thickness(0, 18, 0, 0);
            ValueTextBlock.FontSize = 27;
            SubtitleTextBlock.Visibility = Visibility.Visible;
            SubtitleTextBlock.Text = Subtitle;
        }

        // Draw track
        TrackPath.Data = CreateArcGeometry(cx, cy, r, centerAngle, totalTrackSweep);

        // Draw progress
        double clamped = Math.Clamp(Value, 0.0, 100.0);
        if (ValueTextBlock != null)
        {
            ValueTextBlock.Text = string.Format("{0:F0}%", clamped);
        }
        _currentSweepAngle = (clamped / 100.0) * totalTrackSweep;
        DrawProgressArc(_currentSweepAngle);
    }

    private void DrawProgressArc(double sweepAngle)
    {
        if (ProgressPath == null) return;

        if (sweepAngle <= 0.5)
        {
            ProgressPath.Data = null;
            return;
        }

        double startAngle = IsFullCircle ? -90.0 : 150.0;
        double cx = IsFullCircle ? 40.0 : 74.0;
        double cy = IsFullCircle ? 40.0 : 60.0;
        double r = IsFullCircle ? 30.0 : 46.0;

        ProgressPath.Data = CreateArcGeometry(cx, cy, r, startAngle, sweepAngle);
    }

    private static PathGeometry CreateArcGeometry(double cx, double cy, double radius, double startAngleDeg, double sweepAngleDeg)
    {
        double startRad = startAngleDeg * Math.PI / 180.0;
        double endAngleDeg = startAngleDeg + sweepAngleDeg;
        double endRad = endAngleDeg * Math.PI / 180.0;

        double x1 = cx + radius * Math.Cos(startRad);
        double y1 = cy + radius * Math.Sin(startRad);

        double x2 = cx + radius * Math.Cos(endRad);
        double y2 = cy + radius * Math.Sin(endRad);

        bool isLargeArc = sweepAngleDeg > 180.0;

        var figure = new PathFigure
        {
            StartPoint = new Point(x1, y1),
            IsClosed = false
        };

        figure.Segments.Add(new ArcSegment
        {
            Point = new Point(x2, y2),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = isLargeArc
        });

        var geom = new PathGeometry();
        geom.Figures.Add(figure);
        return geom;
    }
}
