using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace App1.Presentation.Converters;

public class BoolToBrushConverter : IValueConverter
{
    public Brush? TrueBrush { get; set; }
    public Brush? FalseBrush { get; set; }
    public bool ReturnUnsetOnFalse { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b && b)
            return TrueBrush ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 94, 130, 131));

        if (ReturnUnsetOnFalse)
            return Microsoft.UI.Xaml.DependencyProperty.UnsetValue;

        return FalseBrush ?? new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

