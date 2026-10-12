using DivaModManager.Common.Helpers;
using System;
using System.Windows.Data;

namespace DivaModManager.Common.Converters
{
    /// <summary>For displaying loadout names. Only "Default" is translated (the name itself is a key in the settings file, so it isn't changed)</summary>
    public class LoadoutNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value as string == "Default" ? UiTranslator.T("Default") : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
