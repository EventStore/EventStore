using EventStore.Common.Utils;
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
                .ToLookup(x => x.Name)
                .Select(ResolvePrecedence);
            return _effectiveOptions.ApplyTo<TOptions>();
        }

        private static IEnumerable<IEnumerable<OptionSource>> GetConfig<TOptions>(string[] args, string environmentPrefix) where TOptions : class, IOptions, new()
        {
            var commandline = CommandLine.Parse<TOptions>(args).Normalize();
            var commanddict = commandline.ToDictionary(x => x.Name);
            yield return commandline;
            yield return
                EnvironmentVariables.Parse<TOptions>(x => NameTranslators.PrefixEnvironmentVariable(x, environmentPrefix));
            var configFile = commanddict.ContainsKey("Config") ? commanddict["Config"].Value as string : null;
            if (configFile != null && File.Exists(configFile))
            {
                yield return
                    Yaml.FromFile(configFile);
            }
            yield return
                TypeDefaultOptions.Get<TOptions>();
        }

        private static OptionSource ResolvePrecedence(IGrouping<string, OptionSource> optionSources)
        {
            return optionSources.First();
        }

        public static TOptions Parse<TOptions>(string configFile, string sectionName = "") where TOptions : class, new()
        {
            return Yaml.FromFile(configFile, sectionName).ApplyTo<TOptions>();
        }

        public static string GetUsage<TOptions>()
        {
            //return ArgUsage.GetUsage<TOptions>();
            return String.Empty;
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
                dumpOptionsBuilder.AppendLine(String.Format("{0,-25} {1} ({2})", optionName + ":", String.IsNullOrEmpty(valueToDump) ? "<empty>" : valueToDump, option.Source));
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
