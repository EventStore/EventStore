using EventStore.Common.Utils;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using EventStore.Common.Yaml.Serialization;
using EventStore.Common.Yaml.Serialization.Utilities;

namespace EventStore.Common.Options
{
    public class EventStoreOptions
    {
        private const string DefaultOptionSource = "<DEFAULT>";
        private const string FromEnvironmentVariable = "from environment variable";
        private const string FromCommandLine = "from commandline";
        private const string FromConfigFile = "from config";
        private static List<Tuple<string, OptionSource>> _parsedOptions;

        public static ArgAction<TOptions> InvokeAction<TOptions>(params string[] args)
        {
            return Args.InvokeAction<TOptions>(args);
        }

        public static TOptions Parse<TOptions>(string[] args, string environmentPrefix) where TOptions : class, IOptions, new()
        {
            _parsedOptions = SetupOptionsForDumping<TOptions>();

            if (args == null || args.Length == 0)
            {
                var arguments = new TOptions();
                arguments = SetEnvironmentVariables(arguments, environmentPrefix);
                ReEvaluateOptionsForDumping(arguments, FromEnvironmentVariable);
                return arguments;
            }
            TOptions options;
            try
            {
                options = Args.Parse<TOptions>(args);
                ReEvaluateOptionsForDumping(options, FromCommandLine);
            }
            catch (ArgException ex)
            {
                throw new OptionException(ex.Message, String.Empty);
            }

            if (!string.IsNullOrWhiteSpace(options.Config))
            {
                if (File.Exists(options.Config))
                {
                    var config = File.ReadAllText(options.Config);
                    try
                    {
                        var deserializer = new Deserializer();
                        TypeConverter.RegisterTypeConverter<IPEndPoint, IPEndPointConverter>();
                        var configAsYaml = deserializer.Deserialize<TOptions>(new StringReader(config));
                        MergeFromConfiguration(configAsYaml, options);
                        ReEvaluateOptionsForDumping(options, FromConfigFile);
                    }
                    catch (Exception ex)
                    {
                        var failureMessage = "Invalid configuration file specified. ";
                        failureMessage += ex.Message;
                        throw new OptionException(failureMessage, String.Empty);
                    }
                }
                else
                {
                    Application.Exit(ExitCode.Error, string.Format("The specified configuration file {0} was not found.", options.Config));
                }
            }

            options = SetEnvironmentVariables(options, environmentPrefix);
            return options;
        }

        public static string GetUsage<TOptions>()
        {
            return ArgUsage.GetUsage<TOptions>();
        }

        private static List<Tuple<string, OptionSource>> SetupOptionsForDumping<TOptions>() where TOptions : IOptions, new()
        {
            var options = new TOptions();
            var parsedOptions = new List<Tuple<string, OptionSource>>();
            foreach (var property in typeof(TOptions).GetProperties())
            {
                var defaultValue = property.GetValue(options, null);
                parsedOptions.Add(new Tuple<string, OptionSource>(property.Name, new OptionSource(DefaultOptionSource, defaultValue)));
            }
            return parsedOptions;
        }

        private static void SetDumpedOptions(string property, string source, object value)
        {
            var optionToReplace = _parsedOptions.First(x => x.Item1 == property);
            var indexOfOption = _parsedOptions.IndexOf(optionToReplace);
            _parsedOptions.Remove(optionToReplace);
            _parsedOptions.Insert(indexOfOption, new Tuple<string, OptionSource>(property, new OptionSource(source, value)));
        }

        private static void ReEvaluateOptionsForDumping<TOptions>(TOptions currentOptions, string source) where TOptions : IOptions, new()
        {
            var options = new TOptions();
            foreach (var property in typeof(TOptions).GetProperties())
            {
                var defaultValue = property.GetValue(options, null);
                var currentValue = property.GetValue(currentOptions, null);
                if (defaultValue != null && !defaultValue.Equals(currentValue))
                {
                    SetDumpedOptions(property.Name, source, currentValue);
                }
            }
        }

        private static void MergeFromConfiguration<TOptions>(TOptions argumentsFromConfig, TOptions commandLineArguments) where TOptions : IOptions, new()
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
                    SetDumpedOptions(property.Name, FromConfigFile, valueToUse);
                    property.SetValue(commandLineArguments, valueToUse, null);
                }
            }
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
                        SetDumpedOptions(property.Name, FromEnvironmentVariable, valueToUse);
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

        public static string DumpOptions()
        {
            if (_parsedOptions == null)
            {
                return "No options have been parsed";
            }
            var dumpOptionsBuilder = new StringBuilder();
            foreach (var option in _parsedOptions)
            {
                var value = option.Item2.Value;
                var optionName = PascalCaseNameSplitter(option.Item1).ToUpper();
                var valueToDump = value.ToString();
                var source = option.Item2.Source;
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
    public struct OptionSource
    {
        public string Source;
        public object Value;
        public OptionSource(string source, object value)
        {
            Source = source;
            Value = value;
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