using EventStore.Common.Log;
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
            var possibleInvalidOptions = optionSources.Select(option => option.Name)
                                                      .Except(validOptions);
            if (possibleInvalidOptions.Any())
            {
                var invalidOptions = possibleInvalidOptions
                                        .Select(invalidOption => optionSources.First(x => x.Name == invalidOption))
                                        .ToArray();

                var message = InformAboutUnknownOptions(validOptions, optionSources, invalidOptions);
                message += InformAboutPossibleCasingIssues(validOptions, optionSources, invalidOptions);

                throw new OptionException(message, String.Empty);
            }
        }
        private static string InformAboutUnknownOptions(string[] validOptions, OptionSource[] optionSources, OptionSource[] invalidOptions)
        {
            var message = "The following unknown options have been specified." + Environment.NewLine;
            message += String.Join(", ", invalidOptions.Select(x => x.Name).ToArray()) + Environment.NewLine;
            return message;
        }
        private static string InformAboutPossibleCasingIssues(string[] validOptions, OptionSource[] optionSources, OptionSource[] invalidOptions)
        {
            var possibleIncorrectlyCasedOptions = invalidOptions.Select(option => option.Name.ToLower())
                                                        .Intersect(validOptions.Select(option => option.ToLower()));

            if (possibleIncorrectlyCasedOptions.Any())
            {
                var incorrectlyCasedOptions = possibleIncorrectlyCasedOptions.Select(option =>
                                                new
                                                {
                                                    Correct = optionSources.First(x => x.Name.ToLower() == option).Name,
                                                    Incorrect = invalidOptions.First(x => x.Name.ToLower() == option).Name
                                                });
                string message = "The following options are incorrectly cased." + Environment.NewLine;
                message += String.Join(", ", incorrectlyCasedOptions.Select(x => x.Incorrect + " should be " + x.Correct).ToArray()) + Environment.NewLine;
                return message;
            }
            return String.Empty;
        }
    }
}
