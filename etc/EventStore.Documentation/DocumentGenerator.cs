using EventStore.Common.Options;
using PowerArgs;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

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
                        optionDocumentation += String.Format("| Group  | Parameter | Environment *(all prefixed with EVENTSTORE_)* | Yaml | Description | Default |{0}", Environment.NewLine);
                        optionDocumentation += String.Format("| ------ | --------- | --------------------------------------------- | ---- | ----------- | ------- |{0}", Environment.NewLine);
                        var properties = options.GetType().GetProperties();
                        var argumentsDefinition = new CommandLineArgumentsDefinition(optionType);
                        var currentGroup = String.Empty;
                        foreach (var property in properties.OrderBy(x => x.Attr<ArgDescription>().Group))
                        {
                            var parameterRow = String.Empty;
                            var groupColumn = "|";
                            if (currentGroup != property.Attr<ArgDescription>().Group)
                            {
                                currentGroup = property.Attr<ArgDescription>().Group;
                                groupColumn += "**" + currentGroup + "**";
                            }

                            parameterRow += groupColumn;

                            var parameterDefinition = argumentsDefinition.Arguments.First(x => ((PropertyInfo)x.Source).Name == property.Name);
                            var parameterUsageFormat = "-{0} <br/>";
                            var parameterUsage = String.Empty;

                            foreach (var alias in parameterDefinition.Aliases.Reverse())
                            {
                                parameterUsage += String.Format(parameterUsageFormat, alias);
                                parameterUsageFormat = "--{0}=VALUE";
                            }

                            parameterRow += String.Format("|{0}", parameterUsage);
                            parameterRow += String.Format("|{0}", EnvironmentVariableNameProvider.GetName("EVENTSTORE_", property.Name.ToUpper()));
                            parameterRow += String.Format("|{0}", property.Name);
                            parameterRow += String.Format("|{0}", property.Attr<ArgDescription>().Description);
                            parameterRow += String.Format("|{0}|{1}", GetValues(property.GetValue(options, null)), Environment.NewLine);

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
            return value.ToString();
        }
    }
}
