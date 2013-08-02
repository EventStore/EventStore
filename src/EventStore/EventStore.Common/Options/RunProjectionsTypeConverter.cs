using System;
using System.ComponentModel;
using System.Globalization;

namespace EventStore.Common.Options
{
    public enum RunProjections
    {
        None,
        System,
        All
    }

    [TypeConverter(typeof(RunProjections))]
    public sealed class RunProjectionsTypeConverter : TypeConverter
    {
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            var v = ((string) value).ToLowerInvariant();
            switch (v)
            {
                case "none":
                    return RunProjections.None;
                case "system":
                    return RunProjections.System;
                case "all":
                    return RunProjections.All;
                default:
                    throw new InvalidCastException();
            }
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string);
        }
    }
}