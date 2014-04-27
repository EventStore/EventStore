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
        public static T Parse<T>(string[] args) where T : class, IOptions, new()
        {
            parsedOptions = SetupOptionsForDumping<T>();
            if (args == null || args.Length == 0)
            {
                var arguments = new T();
                arguments = SetEnvironmentVariables<T>(arguments);
                return arguments;
            }
            var commandLineArguments = PowerArgs.Args.Parse<T>(args);
            if (File.Exists(commandLineArguments.Config))
            {
                var config = File.ReadAllText(commandLineArguments.Config);
                var configAsJson = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(config);
                MergeFromConfiguration<T>(configAsJson, commandLineArguments);
            }
            commandLineArguments = SetEnvironmentVariables<T>(commandLineArguments);
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
                parsedOptions.Add(new Tuple<string, object>(property.Name, "default"));
            }
            return parsedOptions;
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
                    property.SetValue(commandLineArguments, property.GetValue(argumentsFromConfig, null), null);
                }
            }
            return commandLineArguments;
        }

        private static T SetEnvironmentVariables<T>(T eventStoreArguments) where T : class, IOptions, new()
        {
            var instanceToUseForDefaultValueComparrison = new T();
            foreach (var property in typeof(T).GetProperties())
            {
                var defaultValue = property.GetValue(instanceToUseForDefaultValueComparrison, null);
                var environmentVariableName = EnvironmentVariableNameProvider.GetName(property.Name);
                var environmentVariableValue = Environment.GetEnvironmentVariable(environmentVariableName);
                var currentValue = property.GetValue(eventStoreArguments, null);

                if (defaultValue != null &&
                    defaultValue.Equals(currentValue) &&
                    environmentVariableValue != null)
                {
                    var valueToSet = Convert.ChangeType(environmentVariableValue, property.PropertyType);
                    property.SetValue(eventStoreArguments, valueToSet, null);
                }
            }
            return eventStoreArguments;
        }
        public static string DumpOptions<T>() where T : IOptions, new()
        {
            return String.Empty;
            //var sb = new StringBuilder();
            //foreach (var option in _optionContainers.Values)
            //{
            //    var ss = new StringBuilder();
            //    foreach (var c in option.Name)
            //    {
            //        if (ss.Length > 0 && char.IsLower(ss[ss.Length - 1]) && char.IsUpper(c))
            //            ss.Append(' ');
            //        ss.Append(c);
            //    }
            //    var optionName = ss.ToString().ToUpper();
            //    var value = option.FinalValue is IEnumerable<object>
            //                        ? string.Join(", ", ((IEnumerable<object>)option.FinalValue).ToArray())
            //                        : option.FinalValue;
            //    if (value is string && (string)value == "")
            //        value = "<empty>";
            //    switch (option.Origin)
            //    {
            //        case OptionOrigin.None:
            //            throw new InvalidOperationException("Shouldn't get here ever.");
            //        case OptionOrigin.CommandLine:
            //            sb.AppendFormat("{0,-25} {1} ({2}{3} from command line)\n",
            //                            optionName + ":",
            //                            value,
            //                            option.OriginOptionName.Length == 1 ? "-" : "--",
            //                            option.OriginOptionName);
            //            break;
            //        case OptionOrigin.Environment:
            //            sb.AppendFormat("{0,-25} {1} ({2} environment variable)\n",
            //                            optionName + ":",
            //                            value,
            //                            option.OriginOptionName);
            //            break;
            //        case OptionOrigin.Config:
            //            sb.AppendFormat("{0,-25} {1} ({2} in config at '{3}')\n",
            //                            optionName + ":",
            //                            value,
            //                            option.OriginOptionName,
            //                            option.OriginName);
            //            break;
            //        case OptionOrigin.Default:
            //            sb.AppendFormat("{0,-25} {1} (<DEFAULT>)\n", optionName + ":", value);
            //            break;
            //        default:
            //            throw new ArgumentOutOfRangeException();
            //    }
            //}
            //return sb.ToString();
        }
    }
    public class EnvironmentVariableNameProvider
    {
        public static string GetName(string name)
        {
            var regex = new System.Text.RegularExpressions.Regex(@"(?<=[A-Z])(?=[A-Z][a-z])|(?<=[^A-Z])(?=[A-Z])|(?<=[A-Za-z])(?=[^A-Za-z])");
            var convertedName = regex.Replace(name, "_");
            return "ES_" + convertedName.ToUpper();
        }
    }
}
