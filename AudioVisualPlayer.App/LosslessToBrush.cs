using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AudioVisualPlayer.App.Themes;

namespace AudioVisualPlayer.App;

public class LosslessToBrush : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new SolidColorBrush(value is true ? ThemeManager.Current.SuccessColor : ThemeManager.Current.BorderColor);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}