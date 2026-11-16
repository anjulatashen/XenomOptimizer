using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace XenomOptimizer.Views;

public partial class RadialTickGaugeControl : UserControl
{
    // Exact 22 pills matching reference image!
    // Start at 180° (completely flat horizontal on left) down to 0° (completely flat horizontal on right)
    private const int TotalTicks = 22;
    private const double StartAngleDeg = 180.0;
    private const double EndAngleDeg = 0.0;
    private const double InnerRadius = 96.0;
    private const double OuterRadius = 145.0;
    private const double TickThickness = 10.5; // Clear, visible space between all adjacent plates!

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(RadialTickGaugeControl),
            new PropertyMetadata(0.0, OnValueChanged));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(
            nameof(Subtitle),
            typeof(string),
            typeof(RadialTickGaugeControl),
            new PropertyMetadata("Scan", OnSubtitleChanged));

    // Exact sampled neon lime / chartreuse from reference: #D1FE29
    public static readonly DependencyProperty ActiveBrushProperty =
        DependencyProperty.Register(
            nameof(ActiveBrush),
            typeof(Brush),
            typeof(RadialTickGaugeControl),
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D1FE29")), OnBrushesChanged));

    // Exact sampled dark pill color from reference: #2D3039
    public static readonly DependencyProperty InactiveBrushProperty =
        DependencyProperty.Register(
            nameof(InactiveBrush),
            typeof(Brush),
            typeof(RadialTickGaugeControl),
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D3039")), OnBrushesChanged));

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

    public Brush ActiveBrush
    {
        get => (Brush)GetValue(ActiveBrushProperty);
        set => SetValue(ActiveBrushProperty, value);
    }

    public Brush InactiveBrush
    {
        get => (Brush)GetValue(InactiveBrushProperty);
        set => SetValue(InactiveBrushProperty, value);
    }

    private readonly List<Line> _tickLines = new();

    public RadialTickGaugeControl()
    {
        InitializeComponent();
        InitializeTicks();
        Loaded += (s, e) =>
        {
            UpdateGauge(Value);
            if (SubtitleTextBlock != null)
            {
                SubtitleTextBlock.Text = Subtitle ?? string.Empty;
            }
        };
    }

    private void InitializeTicks()
    {
        GaugeCanvas.Children.Clear();
        _tickLines.Clear();

        double cx = GaugeCanvas.Width / 2.0; // 180.0
        double cy = 175.0; // Baseline Y: Pill 0 (180°) and Pill 21 (0°) sit flat horizontally at this exact level

        for (int i = 0; i < TotalTicks; i++)
        {
            double angleDeg = StartAngleDeg - (i / (double)(TotalTicks - 1)) * (StartAngleDeg - EndAngleDeg);
            double rad = angleDeg * (Math.PI / 180.0);

            double x1 = cx + InnerRadius * Math.Cos(rad);
            double y1 = cy - InnerRadius * Math.Sin(rad);
            double x2 = cx + OuterRadius * Math.Cos(rad);
            double y2 = cy - OuterRadius * Math.Sin(rad);

            var line = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                StrokeThickness = TickThickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Stroke = InactiveBrush
            };

            GaugeCanvas.Children.Add(line);
            _tickLines.Add(line);
        }
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RadialTickGaugeControl control)
        {
            control.UpdateGauge((double)e.NewValue);
        }
    }

    private static void OnSubtitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RadialTickGaugeControl control && control.SubtitleTextBlock != null)
        {
            control.SubtitleTextBlock.Text = e.NewValue?.ToString() ?? string.Empty;
        }
    }

    private static void OnBrushesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RadialTickGaugeControl control)
        {
            control.UpdateGauge(control.Value);
        }
    }

    private void UpdateGauge(double value)
    {
        if (ValueTextBlock != null)
        {
            ValueTextBlock.Text = $"{Math.Round(value)}%";
        }

        double pct = Math.Clamp(value / 100.0, 0.0, 1.0);
        int activeTicks = pct <= 0.001 ? 0 : (int)Math.Round(pct * TotalTicks);
        if (pct > 0.001 && activeTicks == 0) activeTicks = 1;

        var activeBrush = ActiveBrush;
        var inactiveBrush = InactiveBrush;

        for (int i = 0; i < _tickLines.Count; i++)
        {
            bool isActive = i < activeTicks;
            _tickLines[i].Stroke = isActive ? activeBrush : inactiveBrush;
            if (isActive)
            {
                _tickLines[i].Effect = new DropShadowEffect
                {
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Color = (Color)ColorConverter.ConvertFromString("#D1FE29"),
                    Opacity = 0.75
                };
            }
            else
            {
                _tickLines[i].Effect = null;
            }
        }
    }
}
