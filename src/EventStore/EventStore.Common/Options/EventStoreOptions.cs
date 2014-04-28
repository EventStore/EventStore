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
        private static List<Tuple<string, OptionSource>> parsedOptions;

        public static TOptions Parse<TOptions>(string[] args, string environmentPrefix) where TOptions : class, IOptions, new()
        {
            parsedOptions = SetupOptionsForDumping<TOptions>();

            if (args == null || args.Length == 0)
            {
                var arguments = new TOptions();
                arguments = SetEnvironmentVariables<TOptions>(arguments, environmentPrefix);
                ReEvaluateOptionsForDumping(arguments, "environment");
                return arguments;
            }
            TOptions options = null;
            try
            {
                options = PowerArgs.Args.Parse<TOptions>(args);
                ReEvaluateOptionsForDumping(options, "commandline");
            }
            catch (ArgException ex)
            {
                throw new OptionException(ex.Message, String.Empty);
            }
            if (File.Exists(options.Config))
            {
                var config = File.ReadAllText(options.Config);
                TOptions configAsJson = null;
                try
                {
                    configAsJson = Newtonsoft.Json.JsonConvert.DeserializeObject<TOptions>(config);
                    MergeFromConfiguration<TOptions>(configAsJson, options);
                }
                catch (Newtonsoft.Json.JsonReaderException ex)
                {
                    throw new OptionException(ex.Message, ex.Path);
                }
            }
            options = SetEnvironmentVariables<TOptions>(options, environmentPrefix);
            return options;
        }

        public static string GetUsage<TOptions>()
        {
            return PowerArgs.ArgUsage.GetUsage<TOptions>();
        }

        private static List<Tuple<string, OptionSource>> SetupOptionsForDumping<TOptions>() where TOptions : IOptions, new()
        {
            TOptions options = new TOptions();
            var parsedOptions = new List<Tuple<string, OptionSource>>();
            foreach (var property in typeof(TOptions).GetProperties())
            {
                var defaultValue = property.GetValue(options, null);
                parsedOptions.Add(new Tuple<string, OptionSource>(property.Name, new OptionSource("default", defaultValue)));
            }
            return parsedOptions;
        }

        private static void SetDumpedOptions(string property, string source, object value)
        {
            parsedOptions.Remove(parsedOptions.First(x => x.Item1 == property));
            parsedOptions.Add(new Tuple<string, OptionSource>(property, new OptionSource(source, value)));
        }

        private static List<Tuple<string, OptionSource>> ReEvaluateOptionsForDumping<TOptions>(TOptions currentOptions, string source) where TOptions : IOptions, new()
        {
            TOptions options = new TOptions();
            var parsedOptions = new List<Tuple<string, OptionSource>>();
            foreach (var property in typeof(TOptions).GetProperties())
            {
                var defaultValue = property.GetValue(options, null);
                var currentValue = property.GetValue(currentOptions, null);
                if (defaultValue != null && !defaultValue.Equals(currentValue))
                {
                    SetDumpedOptions(property.Name, source, currentValue);
                }
            }
            return parsedOptions;
        }

        private static TOptions MergeFromConfiguration<TOptions>(TOptions argumentsFromConfig, TOptions commandLineArguments) where TOptions : IOptions, new()
        {
            var instanceToUseForDefaultValueComparrison = new TOptions();
            foreach (var property in typeof(TOptions).GetProperties())
            {
                var defaultValue = property.GetValue(instanceToUseForDefaultValueComparrison, null);
                var configValue = property.GetValue(argumentsFromConfig, null);
                var commandLineValue = property.GetValue(commandLineArguments, null);

                if (defaultValue != null &&
                   !defaultValue.Equals(configValue) &&
                    defaultValue.Equals(commandLineValue))
                {
                    var valueToUse = property.GetValue(argumentsFromConfig, null);
                    SetDumpedOptions(property.Name, "config", valueToUse);
                    property.SetValue(commandLineArguments, valueToUse, null);
                }
            }
            return commandLineArguments;
        }

        private static TOptions SetEnvironmentVariables<TOptions>(TOptions eventStoreArguments, string environmentPrefix) where TOptions : class, IOptions, new()
        {
            var instanceToUseForDefaultValueComparrison = new TOptions();
            foreach (var property in typeof(TOptions).GetProperties())
            {
                var defaultValue = property.GetValue(instanceToUseForDefaultValueComparrison, null);
                var environmentVariableName = EnvironmentVariableNameProvider.GetName(environmentPrefix, property.Name);
                var environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName);
                var currentValue = property.GetValue(eventStoreArguments, null);

                if (defaultValue != null &&
                    defaultValue.Equals(currentValue) &&
                    environmentVariableValue != null)
                {
                    try
                    {
                        var valueToUse = Convert.ChangeType(environmentVariableValue, property.PropertyType);
                        SetDumpedOptions(property.Name, "environment", valueToUse);
                        property.SetValue(eventStoreArguments, valueToUse, null);
                    }
                    catch (FormatException ex)
                    {
                        throw new OptionException(ex.Message, String.Empty);
                    }
                }
            }
            return eventStoreArguments;
        }

        public static string DumpOptions<TOptions>() where TOptions : IOptions, new()
        {
            if (parsedOptions == null)
            {
                return "No options have been parsed";
            }
            var dumpOptionsBuilder = new StringBuilder();
            foreach (var option in parsedOptions)
            {
                var value = option.Item2.Value;
                var valueToDump = value.ToString();
                if (value is Array)
                {
                    valueToDump = "[ " + String.Join(", ", (IEnumerable<object>)value) + " ]";
                }
                dumpOptionsBuilder.AppendLine(String.Format("{0} - {1}:{2}", option.Item1, option.Item2.Source, String.IsNullOrEmpty(valueToDump) ? "<empty>" : valueToDump));
            }
            return dumpOptionsBuilder.ToString();
        }
    }
    public struct OptionSource
    {
        public string Source;
        public object Value;
        public OptionSource(string source, object value)
        {
            this.Source = source;
            this.Value = value;
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
