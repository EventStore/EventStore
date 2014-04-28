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
        private static List<Tuple<string, object>> parsedOptions;
        public static TOptions Parse<TOptions>(string[] args, string environmentPrefix) where TOptions : class, IOptions, new()
        {
            parsedOptions = SetupOptionsForDumping<TOptions>();

            if (args == null || args.Length == 0)
            {
                var arguments = new TOptions();
                arguments = SetEnvironmentVariables<TOptions>(arguments, environmentPrefix);
                return arguments;
            }
            TOptions commandLineArguments = null;
            try
            {
                commandLineArguments = PowerArgs.Args.Parse<TOptions>(args);
            }
            catch (ArgException ex)
            {
                throw new OptionException(ex.Message, String.Empty);
            }
            if (File.Exists(commandLineArguments.Config))
            {
                var config = File.ReadAllText(commandLineArguments.Config);
                TOptions configAsJson = null;
                try
                {
                    configAsJson = Newtonsoft.Json.JsonConvert.DeserializeObject<TOptions>(config);
                    MergeFromConfiguration<TOptions>(configAsJson, commandLineArguments);
                }
                catch (Newtonsoft.Json.JsonReaderException ex)
                {
                    throw new OptionException(ex.Message, ex.Path);
                }
            }
            commandLineArguments = SetEnvironmentVariables<TOptions>(commandLineArguments, environmentPrefix);
            return commandLineArguments;
        }

        public static string GetUsage<T>()
        {
            return PowerArgs.ArgUsage.GetUsage<T>();
        }

        private static List<Tuple<string, object>> SetupOptionsForDumping<T>()
        {
            var parsedOptions = new List<Tuple<string, object>>();
            foreach (var property in typeof(T).GetProperties())
            {
                parsedOptions.Add(new Tuple<string, object>(property.Name, "default:" + "empty"));
            }
            return parsedOptions;
        }

        private static void SetDumpedOptions(string property, object value)
        {
            parsedOptions.Remove(parsedOptions.First(x => x.Item1 == property));
            parsedOptions.Add(new Tuple<string, object>(property, value));
        }

        private static T MergeFromConfiguration<T>(T argumentsFromConfig, T commandLineArguments) where T : IOptions, new()
        {
            var instanceToUseForDefaultValueComparrison = new T();
            foreach (var property in typeof(T).GetProperties())
            {
                var defaultValue = property.GetValue(instanceToUseForDefaultValueComparrison, null);
                var configValue = property.GetValue(argumentsFromConfig, null);
                var commandLineValue = property.GetValue(commandLineArguments, null);

                if (defaultValue != null &&
                   !defaultValue.Equals(configValue) &&
                    defaultValue.Equals(commandLineValue))
                {
                    var valueToUse = property.GetValue(argumentsFromConfig, null);
                    SetDumpedOptions(property.Name, "config:" + valueToUse);
                    property.SetValue(commandLineArguments, valueToUse, null);
                }
            }
            return commandLineArguments;
        }

        private static T SetEnvironmentVariables<T>(T eventStoreArguments, string environmentPrefix) where T : class, IOptions, new()
        {
            var instanceToUseForDefaultValueComparrison = new T();
            foreach (var property in typeof(T).GetProperties())
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
                        SetDumpedOptions(property.Name, "environment:" + valueToUse);
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

        public static string DumpOptions<T>() where T : IOptions, new()
        {
            if (parsedOptions == null)
            {
                return "No options have been parsed";
            }
            var dumpOptionsBuilder = new StringBuilder();
            foreach (var option in parsedOptions)
            {
                dumpOptionsBuilder.AppendLine(String.Format("{0} - {1}", option.Item1, option.Item2));
            }
            return dumpOptionsBuilder.ToString();
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
