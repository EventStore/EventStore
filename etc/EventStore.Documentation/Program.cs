using EventStore.Common.Options;
using PowerArgs;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EventStore.Documentation
{
    class Program
    {
        static void Main(string[] args)
        {
            var documentation = String.Empty;
            foreach (var assemblyFilePath in new DirectoryInfo(".").GetFiles().Where(x => x.Name.Contains("EventStore") && x.Name.EndsWith("exe")))
            {
                var assembly = Assembly.LoadFrom(assemblyFilePath.FullName);
                var optionTypes = assembly.GetTypes().Where(x => typeof(IOptions).IsAssignableFrom(x));

                foreach (var optionType in optionTypes)
                {
                    var optionConstructor = optionType.GetConstructor(new Type[]{});
                    var options = optionConstructor.Invoke(null);
                    var optionDocumentation = String.Format("###{0}{1}", options.GetType().Name, Environment.NewLine);
                    optionDocumentation += String.Format("| Parameter | Environment *(all prefixed with EVENTSTORE_)* | Json | Description | Default |{0}", Environment.NewLine);
                    optionDocumentation += String.Format("| --------- | --------------------------------------------- | ---- | ----------- | ------- |{0}", Environment.NewLine);
                    var properties = options.GetType().GetProperties();
                    var argumentsDefinition = new CommandLineArgumentsDefinition(optionType);
                    foreach (var property in properties)
                    {
                        var parameterRow = String.Empty;
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
                        parameterRow += String.Format("|{0}", FirstCharToLower(property.Name));
                        parameterRow += String.Format("|{0}", property.Attr<ArgDescription>().Description);
                        parameterRow += String.Format("|{0}|{1}", GetValues(property.GetValue(options)), Environment.NewLine);

                        optionDocumentation += parameterRow;
                    }
                    optionDocumentation += Environment.NewLine;
                    documentation += optionDocumentation;
                }
            }
            File.WriteAllText("documentation.html", documentation);
        }

        public static string GetValues(object value)
        {
            if(value is Array)
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

        public static string FirstCharToLower(string input)
        {
            if (String.IsNullOrEmpty(input))
                throw new ArgumentException("ARGH!");
            return input.First().ToString().ToLower() + String.Join("", input.Skip(1));
        }
    }
}
