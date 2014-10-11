using System.Collections.Generic;
using System.Linq;

namespace EventStore.Rags
{
    public static class TypeDefaultOptions
    {
        public static IEnumerable<OptionSource> Get<TOptions>() where TOptions : new()
        {
            var defaultOptions = new TOptions();
            return typeof (TOptions).GetProperties()
                .Select(property => new OptionSource("<DEFAULT>", property.Name, property.GetValue(defaultOptions, null).ToString()));
        }
    }
}