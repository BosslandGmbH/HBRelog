using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using HighVoltz.HBRelog.Tasks;

namespace HighVoltz.HBRelog.Converters
{
    class SkipTaskMenuVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            CharacterProfile profile = (CharacterProfile)value;
            if (profile != null)
            {
                if (profile.TaskManager.StartupSequenceIsComplete && profile.TaskManager.Tasks.Count > 1)
                {
                    BMTask task = profile.TaskManager.Tasks.FirstOrDefault(t => t.IsRunning);
                    if (task != null)
                        return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
