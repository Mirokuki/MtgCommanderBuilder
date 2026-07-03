using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MtgCommanderBuilder.Converters
{
    public class PercentageToStrokeDashArrayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = 0;
            if (value is int intVal) percent = intVal;
            else if (value is double dblVal) percent = dblVal;

            percent = Math.Max(0, Math.Min(100, percent));
            
            // Circumference of diameter 130 ellipse (width/height 130) with thickness 10 is 130 * Math.PI = 408.4.
            // Dash array elements are relative to stroke thickness.
            // A dash value of 1.0 represents length equal to thickness (10).
            // So total length in dash units = (130 * Math.PI) / 10 = 40.84.
            double total = 40.84;
            
            // Check parameter to support different circle sizes (e.g. 100 vs 130 diameter)
            if (parameter is string paramStr && double.TryParse(paramStr, out double customTotal))
            {
                total = customTotal;
            }

            double stroke = (percent / 100.0) * total;
            double gap = total - stroke;

            return new DoubleCollection(new double[] { stroke, gap });
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
