﻿using EventStore.Common.Utils;
using EventStore.Rags;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EventStore.Common.Options
{
    public class EventStoreOptions
    {
        private static IEnumerable<OptionSource> _effectiveOptions;

        public static TOptions Parse<TOptions>(string[] args, string environmentPrefix) where TOptions : class, IOptions, new()
        {
            _effectiveOptions = GetConfig<TOptions>(args, environmentPrefix)
                .Flatten()
                .Cleanup()
                .ToLookup(x => x.Name.ToLower())
                .Select(ResolvePrecedence)
                .EnsureExistence<TOptions>()
                .EnsureCorrectType<TOptions>();
            return _effectiveOptions.ApplyTo<TOptions>();
        }

        private static IEnumerable<IEnumerable<OptionSource>> GetConfig<TOptions>(string[] args, string environmentPrefix) where TOptions : class, IOptions, new()
        {
            var commandline = CommandLine.Parse<TOptions>(args).Normalize();
            var commanddict = commandline.ToDictionary(x => x.Name.ToLower());
            yield return commandline;
            yield return
                EnvironmentVariables.Parse<TOptions>(x => NameTranslators.PrefixEnvironmentVariable(x, environmentPrefix));
            var configFile = commanddict.ContainsKey("config") ? commanddict["config"].Value as string : null;
            if (configFile != null)
            {
                if (!File.Exists(configFile))
                {
                    throw new OptionException(String.Format("The specified config file {0} could not be found", configFile), "config");
                }
                yield return
                    Yaml.FromFile(configFile);
            }

            yield return
                TypeDefaultOptions.Get<TOptions>();
        }

        private static OptionSource ResolvePrecedence(IGrouping<string, OptionSource> optionSources)
        {
            var options = optionSources.OrderBy(x =>
                x.Source == "Command Line" ? 0 :
                x.Source == "Environment Variable" ? 1 :
                x.Source == "Config File" ? 2 : 3);
            return options.First();
        }

        public static TOptions Parse<TOptions>(string configFile, string sectionName = "") where TOptions : class, new()
        {
            return Yaml.FromFile(configFile, sectionName).ApplyTo<TOptions>();
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
            var defaultOptionsHeading = "DEFAULT OPTIONS:";
            var displayingModifiedOptions = true;
            dumpOptionsBuilder.AppendLine("MODIFIED OPTIONS:");
            dumpOptionsBuilder.AppendLine();
            if (_effectiveOptions.First().Source.ToLower().Contains("default"))
            {
                dumpOptionsBuilder.AppendLine("NONE");
                dumpOptionsBuilder.AppendLine();
                dumpOptionsBuilder.AppendLine(defaultOptionsHeading);
                dumpOptionsBuilder.AppendLine();
                displayingModifiedOptions = false;
            }
            foreach (var option in _effectiveOptions)
            {
                if (option.Source.ToLower().Contains("default") && displayingModifiedOptions)
                {
                    dumpOptionsBuilder.AppendLine();
                    dumpOptionsBuilder.AppendLine(defaultOptionsHeading);
                    dumpOptionsBuilder.AppendLine();
                    displayingModifiedOptions = false;
                }
                var value = option.Value;
                var optionName = NameTranslators.CombineByPascalCase(option.Name, " ").ToUpper();
                var valueToDump = value == null ? String.Empty : value.ToString();
                if (value is Array)
                {
                    valueToDump = String.Empty;
                    var collection = value as Array;
                    if (collection.Length > 0)
                    {
                        valueToDump = "[ " + String.Join(", ", (IEnumerable<object>)value) + " ]";
                    }
                }
                dumpOptionsBuilder.AppendLine(String.Format("\t{0,-25} {1} ({2})", optionName + ":", String.IsNullOrEmpty(valueToDump) ? "<empty>" : valueToDump, option.Source));
            }
            return dumpOptionsBuilder.ToString();
        }
    }
    public static class RagsExtensions
    {
        public static IEnumerable<OptionSource> Cleanup(this IEnumerable<OptionSource> optionSources)
        {
            return optionSources.Select(x => new OptionSource(x.Source, x.Name.Replace("-", ""), x.IsTyped, x.Value));
        }
        public static IEnumerable<OptionSource> EnsureExistence<TOptions>(this IEnumerable<OptionSource> optionSources) where TOptions : class
        {
            var properties = typeof(TOptions).GetProperties();
            foreach (var optionSource in optionSources)
            {
                if (!properties.Any(x => x.Name.Equals(optionSource.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new OptionException(String.Format("The option {0} is not a known option", optionSource.Name), optionSource.Name);
                }
            }
            return optionSources;
        }
        public static IEnumerable<OptionSource> EnsureCorrectType<TOptions>(this IEnumerable<OptionSource> optionSources) where TOptions : class, new()
        {
            var properties = typeof(TOptions).GetProperties();
            var revived = new TOptions();
            foreach (var optionSource in optionSources)
            {
                var property = properties.First(x => x.Name.Equals(optionSource.Name, StringComparison.OrdinalIgnoreCase));
                try
                {
                    if (optionSource.Value == null) continue;
                    if (optionSource.IsTyped)
                    {
                        property.SetValue(revived, optionSource.Value, null);
                    }
                    else
                    {
                        object revivedValue = null;
                        if (optionSource.Value.GetType().IsArray)
                        {
                            var commaJoined = string.Join(",", ((string[])optionSource.Value));
                            revivedValue = TypeMap.Translate(property.PropertyType, optionSource.Name, commaJoined);
                        }
                        else
                        {
                            revivedValue = TypeMap.Translate(property.PropertyType, optionSource.Name, optionSource.Value.ToString());
                        }
                        property.SetValue(revived, revivedValue, null);
                    }
                }
                catch
                {
                    throw new OptionException(String.Format("The value {0} could not be converted to {1}", optionSource.Value, property.PropertyType.Name), property.Name);
                }
            }
            return optionSources;
        }
    }
}
