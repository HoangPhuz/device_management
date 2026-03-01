using System;
using Microsoft.UI.Xaml.Data;

namespace App1.Presentation.Converters;

public class AvailableToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int available) return available > 0;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
