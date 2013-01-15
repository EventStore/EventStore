using System;
using System.ComponentModel;
using System.Globalization;
using System.Net;

namespace EventStore.Common.Options
{
    public class IPAddressTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            Console.WriteLine("CAN CONVERT CALLED.");

            if (sourceType == typeof(string))
                return true;

            Console.WriteLine("NO!!!!!!!!!!!!!!!!!!! CAN CONVERT CALLED.");

            return base.CanConvertFrom(context, sourceType);
        }
        
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            Console.WriteLine("CONVERTFROM CALLED.");

            var s = value as string;
            if (s != null)
                return IPAddress.Parse(s);

            Console.WriteLine("NO!!!!!!!!!!!!!!!!!!! CONVERTFROM.");

            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            Console.WriteLine("CONVERTTO CALLED.");

            if (destinationType == typeof (string))
                return value.ToString();
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
