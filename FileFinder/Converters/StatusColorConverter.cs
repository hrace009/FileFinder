using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FileFinder.Models;

namespace FileFinder.Converters
{
    /// <summary>Converts SearchStatus → background brush for DataGrid rows.</summary>
    public class StatusToBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is SearchStatus status)
            {
                return status switch
                {
                    SearchStatus.Found => new SolidColorBrush(Color.FromRgb(198, 239, 206)),
                    SearchStatus.MultipleFound => new SolidColorBrush(Color.FromRgb(255, 235, 156)),
                    SearchStatus.NotFound => new SolidColorBrush(Color.FromRgb(255, 199, 206)),
                    _ => Brushes.Transparent
                };
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Converts SearchStatus → foreground brush for status text.</summary>
    public class StatusToForegroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is SearchStatus status)
            {
                return status switch
                {
                    SearchStatus.Found => new SolidColorBrush(Color.FromRgb(0, 97, 0)),
                    SearchStatus.MultipleFound => new SolidColorBrush(Color.FromRgb(156, 87, 0)),
                    SearchStatus.NotFound => new SolidColorBrush(Color.FromRgb(156, 0, 6)),
                    _ => Brushes.Gray
                };
            }
            return Brushes.Black;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Converts SearchType enum to bool for RadioButton IsChecked binding.</summary>
    public class EnumToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (parameter is string paramStr && value is Enum enumValue)
                return enumValue.ToString() == paramStr;
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is true && parameter is string paramStr)
                return Enum.Parse(targetType, paramStr);
            return Binding.DoNothing;
        }
    }
}

