using System.Windows;
using System.Windows.Data;
using System.Collections.Generic;
using System.Linq;
using MtgCommanderBuilder.Models;

namespace MtgCommanderBuilder.Views
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            bool isNull = value == null;
            bool invert = parameter?.ToString() == "Inverse";

            if (invert)
            {
                return isNull ? Visibility.Visible : Visibility.Collapsed;
            }
            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CardDeckStatusConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values == null || values.Length < 3) return string.Empty;

            var card = values[0] as Card;
            var deckCards = values[1] as IEnumerable<Card>;
            var commander = values[2] as Card;

            if (card == null) return string.Empty;

            // 1. Check if it is the Commander
            if (commander != null && commander.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase))
            {
                return "COMMANDER";
            }

            // 2. Check if it is in the deck
            if (deckCards != null)
            {
                var existing = deckCards.FirstOrDefault(c => c.Name.Equals(card.Name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    return $"IN DECK ({existing.Quantity}x)";
                }
            }

            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanInverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
    }

    public class StringEqualToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            string? valStr = value?.ToString();
            string? paramStr = parameter?.ToString();
            bool invert = false;

            if (paramStr != null && paramStr.StartsWith("!"))
            {
                invert = true;
                paramStr = paramStr.Substring(1);
            }

            bool equals = string.Equals(valStr, paramStr, StringComparison.OrdinalIgnoreCase);
            if (invert) equals = !equals;

            return equals ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SubtractOneConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int val)
            {
                return val - 1;
            }
            return 0;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int val)
            {
                return val + 1;
            }
            return 1;
        }
    }
}
