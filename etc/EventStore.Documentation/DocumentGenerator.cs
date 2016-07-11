using EventStore.Common.Options;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EventStore.Rags;

namespace EventStore.Documentation
{
    public class DocumentGenerator
    {
        public void Generate(string[] eventStoreBinaryPaths, string outputPath)
        {
            var documentation = String.Empty;
            foreach (var eventStoreBinaryPath in eventStoreBinaryPaths)
            {
                if (!Directory.Exists(eventStoreBinaryPath))
                {
                    Console.WriteLine("The path <{0}> does not exist", eventStoreBinaryPath);
                    continue;
                }
                foreach (var assemblyFilePath in new DirectoryInfo(eventStoreBinaryPath).GetFiles().Where(x => x.Name.Contains("EventStore") && x.Name.EndsWith("exe")))
                {
                    var assembly = Assembly.LoadFrom(assemblyFilePath.FullName);
                    var optionTypes = assembly.GetTypes().Where(x => typeof(IOptions).IsAssignableFrom(x));

                    foreach (var optionType in optionTypes)
                    {
                        var optionConstructor = optionType.GetConstructor(new Type[] { });
                        var options = optionConstructor.Invoke(null);
                        var optionDocumentation = String.Format("###{0}{1}", options.GetType().Name, Environment.NewLine);

                        var properties = options.GetType().GetProperties();
                        var currentGroup = String.Empty;
                        foreach (var property in properties.OrderBy(x => x.Attr<ArgDescriptionAttribute>().Group))
                        {
                            var parameterRow = String.Empty;
                            if (currentGroup != property.Attr<ArgDescriptionAttribute>().Group)
                            {
                                currentGroup = property.Attr<ArgDescriptionAttribute>().Group;
                                optionDocumentation += String.Format("{0}### {1}{0}{0}", Environment.NewLine, currentGroup);
                                optionDocumentation += String.Format("| Parameter | Environment *(all prefixed with EVENTSTORE_)* | Yaml | Description |{0}", Environment.NewLine);
                                optionDocumentation += String.Format("| --------- | --------------------------------------------- | ---- | ----------- |{0}", Environment.NewLine);
                            }

                            var parameterDefinition = new ArgumentUsageInfo(property);
                            var parameterUsageFormat = "-{0}=VALUE<br/>";
                            var parameterUsage = String.Empty;

                            foreach (var alias in parameterDefinition.Aliases)
                            {
                                parameterUsage += alias + "<br/>";
                            }
                            parameterUsage += String.Format(parameterUsageFormat, parameterDefinition.Name);

                            parameterRow += String.Format("|{0}", parameterUsage);
                            parameterRow += String.Format("|{0}", NameTranslators.PrefixEnvironmentVariable(property.Name, "").ToUpper());
                            parameterRow += String.Format("|{0}", property.Name);

                            var defaultValue = GetValues(property.GetValue(options, null));
                            var defaultString = defaultValue == "" ? "" : String.Format(" (Default: {0})", defaultValue);
                            string[] possibleValues = null;
                            if (property.PropertyType.IsEnum)
                            {
                                possibleValues = property.PropertyType.GetEnumNames();
                            }
                            parameterRow += String.Format("|{0}{1} {2}|{3}",
                                    property.Attr<ArgDescriptionAttribute>().Description,
                                    defaultString, possibleValues != null ? "Possible Values:" + String.Join(",", possibleValues) : String.Empty,
                                    Environment.NewLine);

                            optionDocumentation += parameterRow;
                        }

                        optionDocumentation += Environment.NewLine;
                        documentation += optionDocumentation;
                    }
                }
                if (!String.IsNullOrEmpty(documentation))
                {
                    Console.WriteLine("Writing generated document to {0}", outputPath);
                    File.WriteAllText(outputPath, documentation);
                }
                else
                {
                    Console.Error.WriteLine("The generated document is empty, please ensure that the event store binary paths that you supplied are correct.");
                }
            }
        }
        public static string GetValues(object value)
        {
            if (value is Array)
            {
                var values = String.Empty;
                foreach (var val in (Array)value)
                {
                    values += val + ",";
                }
                return values.Length > 1 ? values.Substring(0, values.Length - 1) : "n/a";
            }
            return value == null ? "" : value.ToString();
        }
    }
}
