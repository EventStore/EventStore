using PowerArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Common.Options
{
    public class CommandLineParser : IOptionSourceProvider
    {
        private List<OptionSource> _parsedOptions;
        public OptionSource[] Parse<TOptions>(string[] args) where TOptions : class
        {
            _parsedOptions = new List<OptionSource>();
            var definition = new CommandLineArgumentsDefinition(typeof(TOptions));
            var options = (TOptions)Args.Parse(definition, args).Value;
            var changedOptions = definition.Arguments.Where(x => x.RevivedValue != null).ToList();
            foreach (var changedOption in changedOptions)
            {
                var optionName = ((System.Reflection.PropertyInfo)changedOption.Source).Name;
                _parsedOptions.Add(new OptionSource("Command Line", optionName, changedOption.RevivedValue));
            }
            return _parsedOptions.ToArray();
        }
        public OptionSource[] GetEffectiveOptions()
        {
            return _parsedOptions.ToArray() ?? new OptionSource[] { };
        }
    }
}
