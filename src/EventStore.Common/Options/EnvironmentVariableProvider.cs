using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Common.Options
{
    public class EnvironmentVariableProvider : IOptionSourceProvider
    {
        private List<OptionSource> _parsedOptions;
        public OptionSource[] Parse<TOptions>(string environmentPrefix) where TOptions : class
        {
            _parsedOptions = new List<OptionSource>();
            foreach (var property in typeof(TOptions).GetProperties())
            {
                var environmentVariableName = EnvironmentVariableNameProvider.GetName(environmentPrefix, property.Name);
                var environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName);
                if (!String.IsNullOrEmpty(environmentVariableValue))
                {
                    _parsedOptions.Add(new OptionSource("Environment Variable", property.Name, environmentVariableValue));
                }
            }
            return _parsedOptions.ToArray();

        }
        public OptionSource[] GetEffectiveOptions()
        {
            return _parsedOptions.ToArray() ?? new OptionSource[] { };
        }
    }

    public class EnvironmentVariableNameProvider
    {
        public static string GetName(string environmentPrefix, string name)
        {
            var regex = new System.Text.RegularExpressions.Regex(@"(?<=[A-Z])(?=[A-Z][a-z])|(?<=[^A-Z])(?=[A-Z])|(?<=[A-Za-z])(?=[^A-Za-z])");
            var convertedName = regex.Replace(name, "_");
            return environmentPrefix + convertedName.ToUpper();
        }
    }
}
