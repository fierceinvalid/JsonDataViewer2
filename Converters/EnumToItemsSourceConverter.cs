// File: EnumToItemsSourceConverter.cs

using System;
using System.Globalization;
using System.Linq; 
using System.Windows.Data;
using System.Windows.Markup; 

namespace JsonDataViewer.Converters
{
    public class EnumToItemsSourceConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }

        // FIX: The converter now uses the 'parameter' field passed from XAML 
        // which contains the Type (vm:ViewMode)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // The parameter is expected to be the Type of the Enum
            if (parameter is Type enumType && enumType.IsEnum)
            {
                // Returns the values of the enum (UserGroupAppPerm, etc.)
                return Enum.GetValues(enumType).Cast<object>();
            }
            return new string[0];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}