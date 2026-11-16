using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace XenomOptimizer.Views;

/// <summary>
/// Vector card shape featuring smooth rounded outer corners and a custom bottom-left cutout notch
/// with smooth inner (concave) and outer (convex) fillets.
/// </summary>
public class NotchedCardShape : Shape
{
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(double), typeof(NotchedCardShape),
            new FrameworkPropertyMetadata(24.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty NotchWidthProperty =
        DependencyProperty.Register(nameof(NotchWidth), typeof(double), typeof(NotchedCardShape),
            new FrameworkPropertyMetadata(280.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty NotchHeightProperty =
        DependencyProperty.Register(nameof(NotchHeight), typeof(double), typeof(NotchedCardShape),
            new FrameworkPropertyMetadata(105.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty InnerCornerRadiusProperty =
        DependencyProperty.Register(nameof(InnerCornerRadius), typeof(double), typeof(NotchedCardShape),
            new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public double NotchWidth
    {
        get => (double)GetValue(NotchWidthProperty);
        set => SetValue(NotchWidthProperty, value);
    }

    public double NotchHeight
    {
        get => (double)GetValue(NotchHeightProperty);
        set => SetValue(NotchHeightProperty, value);
    }

    public double InnerCornerRadius
    {
        get => (double)GetValue(InnerCornerRadiusProperty);
        set => SetValue(InnerCornerRadiusProperty, value);
    }

    protected override Geometry DefiningGeometry
    {
        get
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return Geometry.Empty;

            double R = Math.Min(CornerRadius, Math.Min(w / 2, h / 2));
            double notchW = Math.Min(NotchWidth, w - R);
            double notchH = Math.Min(NotchHeight, h - R);
            double innerR = Math.Min(InnerCornerRadius, Math.Min(notchW / 2, notchH / 2));

            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(new Point(R, 0), true, true);
                ctx.LineTo(new Point(w - R, 0), true, true);
                ctx.ArcTo(new Point(w, R), new Size(R, R), 0, false, SweepDirection.Clockwise, true, true);
                ctx.LineTo(new Point(w, h - R), true, true);
                ctx.ArcTo(new Point(w - R, h), new Size(R, R), 0, false, SweepDirection.Clockwise, true, true);
                ctx.LineTo(new Point(notchW + innerR, h), true, true);
                ctx.ArcTo(new Point(notchW, h - innerR), new Size(innerR, innerR), 0, false, SweepDirection.Clockwise, true, true);
                ctx.LineTo(new Point(notchW, h - notchH + innerR), true, true);
                ctx.ArcTo(new Point(notchW - innerR, h - notchH), new Size(innerR, innerR), 0, false, SweepDirection.Counterclockwise, true, true);
                ctx.LineTo(new Point(innerR, h - notchH), true, true);
                ctx.ArcTo(new Point(0, h - notchH - innerR), new Size(innerR, innerR), 0, false, SweepDirection.Clockwise, true, true);
                ctx.LineTo(new Point(0, R), true, true);
                ctx.ArcTo(new Point(R, 0), new Size(R, R), 0, false, SweepDirection.Clockwise, true, true);
            }
            geom.Freeze();
            return geom;
        }
    }
}

