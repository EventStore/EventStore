using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Common.Options
{
    internal class OptionsSourceRulesRunner
    {
        public static void RunRules(string[] validOptions, OptionSource[] optionSources)
        {
            UnknownOptionRules(validOptions, optionSources);
        }
        private static void UnknownOptionRules(string[] validOptions, OptionSource[] optionSources)
        {
            if (optionSources == null) return;
            var possibleInvalidOptions = optionSources.Select(option => option.Name).Except(validOptions);
            if (possibleInvalidOptions.Any())
            {
                var message = "The following options are not valid." + Environment.NewLine;
                message += String.Join(",", possibleInvalidOptions);
                throw new OptionException(message, String.Empty);
            }
        }
    }
}
