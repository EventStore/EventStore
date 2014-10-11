using System.Collections.Generic;
using System.Linq;
using PowerArgs;

namespace EventStore.Rags
{
    public class CommandLine
    {
        public static IEnumerable<OptionSource> Parse<TOptions>(string[] args) where TOptions : class
        {
            //TODO GFY drop lower here
            //From poking around we actually want to use argParser here not all of powerargs, it needs a definition and a context
            var ret = new List<OptionSource>();
            var definition = new CommandLineArgumentsDefinition(typeof(TOptions));
            var options = (TOptions)Args.Parse(definition, args).Value;
            var changedOptions = definition.Arguments.Where(x => x.RevivedValue != null).ToList();
            foreach (var changedOption in changedOptions)
            {
                var optionName = ((System.Reflection.PropertyInfo)changedOption.Source).Name;
                ret.Add(OptionSource.Typed("Command Line", optionName, changedOption.RevivedValue));
            }
            return ret;
        }
    }
}