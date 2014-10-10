using System;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Rags
{
    public class EnvironmentVariables 
    {
        public IEnumerable<OptionSource> Parse<TOptions>(Func<string, string> nameTranslator) where TOptions : class
        {
            return 
                (from property in typeof (TOptions).GetProperties() 
                    let environmentVariableName = nameTranslator(property.Name) 
                    let environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName) 
                    where !String.IsNullOrEmpty(environmentVariableValue) 
                    select new OptionSource("Environment Variable", property.Name, environmentVariableValue)).ToList();
        }
    }
}