using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;

namespace HighVoltz.HBRelog.Converters
{
    public class SpacifierConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string && targetType == typeof(string))
            {
                return GetSpaciousString((string)value);
            }
            throw new InvalidOperationException("value or target type is not supported");
        }

        public static string GetSpaciousString(string input)
        {
            string spaciousString = "";
            for (int i = 0; i < input.Length; i++)
            {
                if (i > 0 && char.IsUpper(input[i]) && i != input.Length - 1)
                    spaciousString += (" " + input[i]);
                else
                    spaciousString += input[i];
            }
            return spaciousString;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string && targetType == typeof(string))
            {
                return ((string)value).Replace(" ", "");
            }
            throw new InvalidOperationException("value or target type is not supported");
        }
    }
}
