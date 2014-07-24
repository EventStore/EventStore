using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;

namespace EventStore.Common.Utils
{
    public class IPEndPointArrayConverter : ArrayConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }
        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            var values = value as IEnumerable;
            if (values != null)
            {
                var ipEndPointList = new List<IPEndPoint>();
                foreach (var val in values)
                {
                    ipEndPointList.Add((IPEndPoint)new IPEndPointConverter().ConvertFrom(val));
                }
                return ipEndPointList.ToArray();
            }
            var valueAsString = value as string;
            if (valueAsString != null)
            {
                var ipEndPointList = valueAsString.Split(new[] { "," }, StringSplitOptions.None).Select(x => new IPEndPointConverter().ConvertFrom(x));
                return ipEndPointList.ToArray();
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
