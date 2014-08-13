using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Common.Options
{
    public class DefaultOptionsProvider : IOptionSourceProvider
    {
        private List<OptionSource> _parsedOptions;
        public OptionSource[] Get<TOptions>() where TOptions : IOptions, new()
        {
            _parsedOptions = new List<OptionSource>();
            var defaultOptions = new TOptions();
            foreach (var property in typeof(TOptions).GetProperties())
            {
                _parsedOptions.Add(new OptionSource("<DEFAULT>", property.Name, property.GetValue(defaultOptions, null)));
            }
            return _parsedOptions.ToArray();
        }
        public OptionSource[] GetEffectiveOptions()
        {
            return _parsedOptions.ToArray() ?? new OptionSource[] { };
        }
    }
}
