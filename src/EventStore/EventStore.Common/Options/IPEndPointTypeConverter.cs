using System;
using System.ComponentModel;
using System.Globalization;
using System.Net;

namespace EventStore.Common.Options
{
	[TypeConverter(typeof(IPEndPoint))]
	public class IPEndPointTypeConverter : TypeConverter
	{
		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			if (sourceType == typeof(string))
				return true;

			return base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			var s = value as string;
			if (s == null)
				return base.ConvertFrom(context, culture, value);

			var parts = s.Split(':');
			return new IPEndPoint(IPAddress.Parse(parts[0]), Int32.Parse(parts[1]));
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(string))
				return value.ToString();
			return base.ConvertTo(context, culture, value, destinationType);
		}
	}
}