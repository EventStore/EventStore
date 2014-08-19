using EventStore.Common.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;

namespace EventStore.Common.Options
{
    public static class OptionsSourceParser
    {
        public static TOptions Parse<TOptions>(OptionSource[] optionSources) where TOptions : class, new()
        {
            var options = new TOptions();
            foreach (var property in typeof(TOptions).GetProperties())
            {
                if (optionSources.Any(x => x.Name == property.Name))
                {
                    object value = null;
                    try
                    {
                        value = optionSources.First(x => x.Name == property.Name).Value;
                        if (value == null) continue;
                        var typeConverter = GetTypeConverter(property.PropertyType);
                        if (typeConverter.CanConvertFrom(value.GetType()) || 
                            typeConverter.CanConvertFrom(value.GetType().BaseType))
                        {
                            property.SetValue(options, typeConverter.ConvertFrom(value), null);
                        }
                        else if (typeConverter.CanConvertFrom(typeof(string)))
                        {
                            property.SetValue(options, typeConverter.ConvertFromString(value.ToString()), null);
                        }
                        else
                        {
                            property.SetValue(options, value, null);
                        }
                    }
                    catch
                    {
                        throw new OptionException(String.Format("The value {0} could not be converted to {1}", value, property.PropertyType.Name), property.Name);
                    }
                }
            }
            return options;
        }

        private static Dictionary<Type, System.ComponentModel.TypeConverter> RegisteredTypeConverters =
        new Dictionary<Type, System.ComponentModel.TypeConverter>
        {   
            {typeof(IPAddress), new IPAddressConverter()},
            {typeof(IPEndPoint), new IPEndPointConverter()},
            {typeof(IPEndPoint[]), new IPEndPointArrayConverter()}
        };

        private static System.ComponentModel.TypeConverter GetTypeConverter(Type typeToConvertTo)
        {
            if (RegisteredTypeConverters.ContainsKey(typeToConvertTo))
            {
                return RegisteredTypeConverters[typeToConvertTo];
            }
            return TypeDescriptor.GetConverter(typeToConvertTo);
        }
    }
}
