using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using HighVoltz.HBRelog.Tasks;

namespace HighVoltz.HBRelog.Converters
{
    class TaskIsRunningConverter : IValueConverter
    {
        static FontStyleConverter _styleConverter = new FontStyleConverter();
        static FontWeightConverter _weightConverter = new FontWeightConverter();
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool isrunning = (bool)value;
            if (targetType == typeof(TextDecorationCollection))
            {
                return !isrunning ? null: TextDecorations.Underline;
            }
            else if (targetType == typeof(FontWeight))
            {
                return isrunning ? _weightConverter.ConvertFrom("Bold") : _weightConverter.ConvertFrom("Normal");
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
