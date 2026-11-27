using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace XenomOptimizer.Views;

public enum NotchPosition
{
    BottomLeft,
    BottomRight
}

/// <summary>
/// Vector card shape featuring smooth rounded outer corners and a custom cutout notch
/// with smooth inner (concave) and outer (convex) fillets.
/// Supports both BottomLeft (Driver Scanner) and BottomRight (Hardware Specifications) cutout notches.
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

    public static readonly DependencyProperty NotchPositionProperty =
        DependencyProperty.Register(nameof(NotchPosition), typeof(NotchPosition), typeof(NotchedCardShape),
            new FrameworkPropertyMetadata(NotchPosition.BottomLeft, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

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

    public NotchPosition NotchPosition
    {
        get => (NotchPosition)GetValue(NotchPositionProperty);
        set => SetValue(NotchPositionProperty, value);
    }

    protected override Geometry DefiningGeometry
    {
        get
        {
            double stroke = (Stroke != null && StrokeThickness > 0) ? StrokeThickness : 0.0;
            double hs = stroke / 2.0;

            double x0 = hs;
            double y0 = hs;
            double x1 = ActualWidth - hs;
            double y1 = ActualHeight - hs;

            double w = x1 - x0;
            double h = y1 - y0;
            if (w <= 0 || h <= 0) return Geometry.Empty;

            double R = Math.Min(CornerRadius, Math.Min(w / 2, h / 2));
            double notchW = Math.Min(NotchWidth, w - R);
            double notchH = Math.Min(NotchHeight, h - R);
            double innerR = Math.Min(InnerCornerRadius, Math.Min(notchW / 2, notchH / 2));

            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                if (NotchPosition == NotchPosition.BottomRight)
                {
                    ctx.BeginFigure(new Point(x0 + R, y0), true, true);
                    ctx.LineTo(new Point(x1 - R, y0), true, true);
                    ctx.ArcTo(new Point(x1, y0 + R), new Size(R, R), 0, false, SweepDirection.Clockwise, true, true);
                    ctx.LineTo(new Point(x1, y1 - notchH - innerR), true, true);
                    ctx.ArcTo(new Point(x1 - innerR, y1 - notchH), new Size(innerR, innerR), 0, false, SweepDirection.Clockwise, true, true);
                    ctx.LineTo(new Point(x1 - notchW + innerR, y1 - notchH), true, true);
                    ctx.ArcTo(new Point(x1 - notchW, y1 - notchH + innerR), new Size(innerR, innerR), 0, false, SweepDirection.Counterclockwise, true, true);
                    ctx.LineTo(new Point(x1 - notchW, y1 - innerR), true, true);
                    ctx.ArcTo(new Point(x1 - notchW - innerR, y1), new Size(innerR, innerR), 0, false, SweepDirection.Clockwise, true, true);
                    ctx.LineTo(new Point(x0 + R, y1), true, true);
                    ctx.ArcTo(new Point(x0, y1 - R), new Size(R, R), 0, false, SweepDirection.Clockwise, true, true);
                    ctx.LineTo(new Point(x0, y0 + R), true, true);
                    ctx.ArcTo(new Point(x0 + R, y0), new Size(R, R), 0, false, SweepDirection.Clockwise, true, true);
                }
                else
                {
                    ctx.BeginFigure(new Point(x0 + R, y0), true, true);
                    ctx.LineTo(new Point(x1 - R, y0), true, true);
                    ctx.ArcTo(new Point(x1, y0 + R), new Size(R, R), 0, false, SweepDirection.Clockwise, true, true);
                    ctx.LineTo(new Point(x1, y1 - R), true, true);
                    ctx.ArcTo(new Point(x1 - R, y1), new Size(R, R), 0, false, SweepDirection.Clockwise, true, true);
                    ctx.LineTo(new Point(x0 + notchW + innerR, y1), true, true);
                    ctx.ArcTo(new Point(x0 + notchW, y1 - innerR), new Size(innerR, innerR), 0, false, SweepDirection.Clockwise, true, true);
                    ctx.LineTo(new Point(x0 + notchW, y1 - notchH + innerR), true, true);
                    ctx.ArcTo(new Point(x0 + notchW - innerR, y1 - notchH), new Size(innerR, innerR), 0, false, SweepDirection.Counterclockwise, true, true);
                    ctx.LineTo(new Point(x0 + innerR, y1 - notchH), true, true);
                    ctx.ArcTo(new Point(x0, y1 - notchH - innerR), new Size(innerR, innerR), 0, false, SweepDirection.Clockwise, true, true);
                    ctx.LineTo(new Point(x0, y0 + R), true, true);
                    ctx.ArcTo(new Point(x0 + R, y0), new Size(R, R), 0, false, SweepDirection.Clockwise, true, true);
                }
            }
            geom.Freeze();
            return geom;
        }
    }
}
