using EventStore.Common.Utils;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EventStore.Common.Options
{
    public class EventStoreOptions
    {
        private static OptionSource[] _effectiveOptions;

        public static ArgAction<TOptions> InvokeAction<TOptions>(params string[] args)
        {
            return Args.InvokeAction<TOptions>(args);
        }

        public static TOptions Parse<TOptions>(string[] args, string environmentPrefix) where TOptions : class, IOptions, new()
        {
            var commandLineParser = new CommandLineParser();
            var yamlParser = new YamlParser();
            var environmentVariableProvider = new EnvironmentVariableProvider();
            var defaultOptionsProvider = new DefaultOptionsProvider();

            OptionSource[] defaultOptionSources = defaultOptionsProvider.Get<TOptions>();
            OptionSource[] commandLineOptionSources;
            OptionSource[] configurationFileOptionSources = null;
            OptionSource[] environmentVariableOptionSources;

            var validKeys = defaultOptionSources.Select(option => option.Name).ToArray();

            if (args == null || args.Length == 0)
            {
                var optionSources = environmentVariableProvider.Parse<TOptions>(environmentPrefix);
                _effectiveOptions = OptionsSourceMerger.SequentialMerge(
                    defaultOptionSources,
                    optionSources);
                return OptionsSourceParser.Parse<TOptions>(optionSources);
            }

            try
            {
                commandLineOptionSources = commandLineParser.Parse<TOptions>(args);
            }
            catch (ArgException ex)
            {
                throw new OptionException(ex.Message, String.Empty);
            }


            if (commandLineOptionSources.Any(x => x.Name == "Config"))
            {
                var configOptionSource = commandLineOptionSources.First(x => x.Name == "Config");
                if (!string.IsNullOrWhiteSpace(configOptionSource.Value.ToString()))
                {
                    if (File.Exists(configOptionSource.Value.ToString()))
                    {
                        var stagedConfigurationOptions = yamlParser.Parse(configOptionSource.Value.ToString(), String.Empty);

                        if (stagedConfigurationOptions != null)
                            configurationFileOptionSources = stagedConfigurationOptions.Where(option => validKeys.Contains(option.Name)).ToArray();
                        else
                            configurationFileOptionSources = new OptionSource[0];
                    }
                    else
                    {
                        Application.Exit(ExitCode.Error, string.Format("The specified configuration file {0} was not found.", configOptionSource.Value));
                    }

                }
            }

            environmentVariableOptionSources = environmentVariableProvider.Parse<TOptions>(environmentPrefix);
            _effectiveOptions = OptionsSourceMerger.SequentialMerge(
                        defaultOptionSources,
                        environmentVariableOptionSources,
                        configurationFileOptionSources,
                        commandLineOptionSources);
            return OptionsSourceParser.Parse<TOptions>(_effectiveOptions);
        }

        public static TOptions Parse<TOptions>(string configFile, string sectionName = "") where TOptions : class, new()
        {
            var yamlParser = new YamlParser();
            var optionsSource = yamlParser.Parse(configFile, sectionName);
            return OptionsSourceParser.Parse<TOptions>(optionsSource);
        }

        public static string GetUsage<TOptions>()
        {
            return ArgUsage.GetUsage<TOptions>();
        }

        public static string DumpOptions()
        {
            if (_effectiveOptions == null)
            {
                return "No options have been parsed";
            }
            var dumpOptionsBuilder = new StringBuilder();
            foreach (var option in _effectiveOptions)
            {
                var value = option.Value;
                var optionName = PascalCaseNameSplitter(option.Name).ToUpper();
                var valueToDump = value.ToString();
                var source = option.Source ?? "<DEFAULT>";
                if (value is Array)
                {
                    valueToDump = String.Empty;
                    var collection = value as Array;
                    if (collection.Length > 0)
                    {
                        valueToDump = "[ " + String.Join(", ", (IEnumerable<object>)value) + " ]";
                    }
                }
                dumpOptionsBuilder.AppendLine(String.Format("{0,-25} {1} ({2})", optionName + ":", String.IsNullOrEmpty(valueToDump) ? "<empty>" : valueToDump, source));
            }
            return dumpOptionsBuilder.ToString();
        }
        private static string PascalCaseNameSplitter(string name)
        {
            var regex = new System.Text.RegularExpressions.Regex(@"(?<=[A-Z])(?=[A-Z][a-z])|(?<=[^A-Z])(?=[A-Z])|(?<=[A-Za-z])(?=[^A-Za-z])");
            var convertedName = regex.Replace(name, " ");
            return convertedName;
        }
    }
}
