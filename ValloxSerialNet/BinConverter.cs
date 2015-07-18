using System;
using System.Globalization;
using System.Windows.Data;

namespace ValloxSerialNet
{
    public class BinConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //string ret = System.Convert.ToString((byte)value, 2);

            string ret = string.Empty;
            int num = (byte)value;
            for (int i = 0; i < 8; i++)
            {
                int rem = num % 2;
                num = num / 2;
                if (rem == 1)
                {
                    ret = "1" + ret;
                }
                else
                {
                    ret = "0" + ret;
                }
            }

            return ret;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
