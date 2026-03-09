using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace App1.Presentation.Converters;

public class PositiveIntToBrushConverter : IValueConverter
{
    public Brush? PositiveBrush { get; set; }
    public Brush? NonPositiveBrush { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int n && n > 0)
            return PositiveBrush ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 50, 118, 177));

        return NonPositiveBrush ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 51, 51, 51));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

