using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

namespace MarketDataCollector.Uwp.Converters;

/// <summary>
/// Converts a boolean to Visibility.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility v)
        {
            return v == Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Converts a boolean to inverse Visibility.
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is Visibility v)
        {
            return v != Visibility.Visible;
        }
        return true;
    }
}

/// <summary>
/// Converts a boolean connection status to a colored brush.
/// </summary>
public class BoolToConnectionStatusConverter : IValueConverter
{
    private static readonly SolidColorBrush ConnectedBrush = new(Colors.LimeGreen);
    private static readonly SolidColorBrush DisconnectedBrush = new(Colors.Red);

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool connected)
        {
            return connected ? ConnectedBrush : DisconnectedBrush;
        }
        return DisconnectedBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a boolean to Yes/No text.
/// </summary>
public class BoolToYesNoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? "Yes" : "No";
        }
        return "No";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is string s)
        {
            return s.Equals("Yes", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return !b;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }
}
