using MaterialDesignThemes.Wpf;
using System;
using System.Globalization;
using System.Windows.Data;

namespace VideoGameLibrary.Converters
{
    // value = puntuación actual (0-5), parameter = posición de la estrella ("1".."5")
    public class RatingToStarKindConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int rating = value is int i ? i : 0;
            int position = int.Parse(parameter?.ToString() ?? "0");
            return rating >= position ? PackIconKind.Star : PackIconKind.StarOutline;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
