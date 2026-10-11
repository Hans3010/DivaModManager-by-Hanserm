using DivaModManager.Common.Helpers;
using System;
using System.Windows.Data;

namespace DivaModManager.Common.Converters
{
    /// <summary>ロードアウト名の表示用。"Default" だけ翻訳する（名前そのものは設定ファイルのキーなので変えない）</summary>
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
