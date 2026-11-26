using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace XenomOptimizer.Converters;

/// <summary>Converts boolean to Visibility (true = Visible, false = Collapsed).</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>Converts boolean to Visibility (true = Collapsed, false = Visible).</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}

/// <summary>Converts boolean to its inverse (true -> false, false -> true).</summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}

/// <summary>Converts string to SolidColorBrush.</summary>
public class StringToColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string colorStr)
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                return new System.Windows.Media.SolidColorBrush(color);
            }
            catch { }
        }
        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Converts string hex to Color.</summary>
public class StringToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string colorStr)
        {
            try
            {
                return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
            }
            catch { }
        }
        return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A3E635");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Converts a non-empty string to Visibility.Visible.</summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Converts disk media type to appropriate accent brush (NVMe = Cyan, HDD = Amber, SSD = Green).</summary>
public class MediaTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string type = value?.ToString() ?? "";
        if (type.Contains("NVMe", StringComparison.OrdinalIgnoreCase))
            return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00D4FF"));
        if (type.Contains("HDD", StringComparison.OrdinalIgnoreCase))
            return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F59E0B"));
        return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A3E635"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts usage percentage (0-100) to a dynamic status brush:
/// Default (&lt; 80%): Electric Blue (#00D4FF)
/// Warning (&gt;= 80%): Amber Orange (#F59E0B)
/// Critical (&gt;= 90%): Danger Red (#EF4444)
/// </summary>
public class UsageToBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush BlueBrush;
    private static readonly System.Windows.Media.SolidColorBrush OrangeBrush;
    private static readonly System.Windows.Media.SolidColorBrush RedBrush;

    static UsageToBrushConverter()
    {
        BlueBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00D4FF"));
        BlueBrush.Freeze();
        OrangeBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F59E0B"));
        OrangeBrush.Freeze();
        RedBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EF4444"));
        RedBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double pct = 0;
        if (value is double d) pct = d;
        else if (value is int i) pct = i;
        else if (value is float f) pct = f;
        else if (value != null && double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            pct = parsed;

        if (pct >= 90.0)
            return RedBrush;
        if (pct >= 80.0)
            return OrangeBrush;
        return BlueBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>Converts percentage (0-100) to StrokeDashArray for an arc gauge.</summary>
public class PercentageToStrokeDashArrayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double pct = 0;
        if (value is double d) pct = d;
        else if (value is int i) pct = i;
        else if (double.TryParse(value?.ToString(), out var parsed)) pct = parsed;

        pct = Math.Clamp(pct, 0, 100) / 100.0;
        double radius = 26;
        if (parameter != null && double.TryParse(parameter.ToString(), out var r))
            radius = r;

        double thickness = 5.5;
        double circumference = 2 * Math.PI * radius;
        double strokeUnits = circumference / thickness;
        double dash = pct >= 0.999 ? strokeUnits + 4 : Math.Max(0.01, strokeUnits * pct);
        double gap = pct >= 0.999 ? 0 : Math.Max(0.01, strokeUnits * (1.0 - pct) + 40);

        return new System.Windows.Media.DoubleCollection { dash, gap };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts percentage (0-100) to StrokeDashArray for a 240-degree horseshoe/arch gauge (like Image 2).
/// </summary>
public class ArcGaugeStrokeDashConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string mode = parameter?.ToString() ?? "progress";
        double size = 64;
        double thickness = 7.0;
        double spanDeg = 200.0;

        double radius = (size - thickness) / 2.0; // 28.5
        double circumference = 2 * Math.PI * radius; // ~179.07
        double strokeUnits = circumference / thickness; // ~25.58
        double arcUnits = strokeUnits * (spanDeg / 360.0); // ~14.21
        double gapUnits = strokeUnits - arcUnits; // ~11.37

        // When StrokeDashCap="Round", WPF extends each dash by 0.5 stroke unit on each side (total 1.0 unit).
        if (mode.Equals("track", StringComparison.OrdinalIgnoreCase))
        {
            double trackDash = Math.Max(0.01, arcUnits - 1.0);
            double trackGap = gapUnits + 1.0;
            return new System.Windows.Media.DoubleCollection { trackDash, trackGap };
        }
        else
        {
            double pct = 0;
            if (value is double d) pct = d;
            else if (value is int i) pct = i;
            else if (double.TryParse(value?.ToString(), out var parsed)) pct = parsed;

            pct = Math.Clamp(pct, 0, 100) / 100.0;

            if (pct <= 0.005)
            {
                return new System.Windows.Media.DoubleCollection { 0.001, strokeUnits * 2 };
            }

            // Active progress dash from start to (pct * arcUnits)
            double progDash = Math.Max(0.01, pct * arcUnits - 1.0);
            double progGap = strokeUnits * 2;
            return new System.Windows.Media.DoubleCollection { progDash, progGap };
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts percentage (0-100) to StrokeDashArray for vector Path arch gauge.
/// </summary>
public class ArchPathDashConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double pct = 0;
        if (value is double d) pct = d;
        else if (value is int i) pct = i;
        else if (double.TryParse(value?.ToString(), out var parsed)) pct = parsed;

        pct = Math.Clamp(pct, 0, 100) / 100.0;
        double totalUnits = 22.0;

        if (pct <= 0.005)
            return new System.Windows.Media.DoubleCollection { 0.001, totalUnits * 2 };

        if (pct >= 0.995)
            return new System.Windows.Media.DoubleCollection { totalUnits + 2, 0 };

        double dash = Math.Max(0.01, pct * totalUnits - 0.5);
        double gap = totalUnits * 2.0;

        return new System.Windows.Media.DoubleCollection { dash, gap };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts scroll offset/value to Opacity.
/// When at the very top (value <= 0.5), returns 0.0 (hidden).
/// When scrolled down (value > 0.5), returns 0.75 (visible as thin line).
/// </summary>
public class ScrollOffsetToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return d > 0.5 ? 0.75 : 0.0;
        }
        if (value is float f)
        {
            return f > 0.5f ? 0.75 : 0.0;
        }
        if (double.TryParse(value?.ToString(), out var parsed))
        {
            return parsed > 0.5 ? 0.75 : 0.0;
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts download progress percent (0 - 100) to pixel height (default max 36px).
/// </summary>
public class DownloadPercentToHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double maxHeight = 36.0;
        if (parameter != null && double.TryParse(parameter.ToString(), out double parsedMax))
        {
            maxHeight = parsedMax;
        }

        if (value is double pct)
        {
            pct = Math.Clamp(pct, 0.0, 100.0);
            return (pct / 100.0) * maxHeight;
        }
        if (value is int intPct)
        {
            intPct = Math.Clamp(intPct, 0, 100);
            return (intPct / 100.0) * maxHeight;
        }
        if (double.TryParse(value?.ToString(), out var parsedPct))
        {
            parsedPct = Math.Clamp(parsedPct, 0.0, 100.0);
            return (parsedPct / 100.0) * maxHeight;
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts integer equality with parameter to Visibility.
/// </summary>
public class IntEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value != null && parameter != null)
        {
            return value.ToString() == parameter.ToString() ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Compares value with parameter string/object, returns true if equal.
/// </summary>
public class EqualityToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null && parameter == null) return true;
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NotNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value != null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}


