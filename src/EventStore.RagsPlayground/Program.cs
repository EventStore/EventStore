using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using EventStore.Rags;

namespace RagsPlayground
{

    public class SomeOptionType
    {
        [ArgDescription("The first string", "firstgroup")]
        public string MyFirstOption { get; set; }

        [ArgDescription("The first string", "firstgroup")]
        public int MyFirstCount { get; set; }

        [ArgDescription("Config", "firstgroup")]
        public string Config { get; set; }

        public SomeOptionType()
        {
            MyFirstCount = 5;
            MyFirstOption = "greg";
        }

        public override string ToString()
        {
            return string.Format("MyFirstOption: {0}, MyFirstCount: {1}", MyFirstOption, MyFirstCount);
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            var resolvedConfig = GetConfig(args)
                .Flatten()
                .ToLookup(x => x.Name)
                .Select(ResolvePrecedence);
                //.ApplyTo<SomeOptionType>();
            foreach (var item in resolvedConfig)
            {
                Console.WriteLine("{0} : {1}={2}", item.Source, item.Name, item.Value);
            }

        }

        private static OptionSource ResolvePrecedence(IGrouping<string, OptionSource> optionSources)
        {
            //go through and pick one based on your rules!
            return optionSources.First();
        }

        private static IEnumerable<IEnumerable<OptionSource>> GetConfig(string [] args)
        {
            var commandline = CommandLine.Parse<SomeOptionType>(args);
            var commanddict = commandline.ToDictionary(x => x.Name);
            yield return commandline;
            yield return
                EnvironmentVariables.Parse<SomeOptionType>(x => NameTranslators.PrefixEnvironmentVariables(x, "ES"));
            var configFile = commanddict["Config"].Value as string;
            if (configFile != null && File.Exists(configFile))
            {
                yield return
                    Yaml.FromFile(@"C:\shitbird.yaml", "foo");
            }
            yield return
                TypeDefaultOptions.Get<SomeOptionType>();
            yield return
                MyOwnCustomDefaultsAintThatNeat();
        }

        private static IEnumerable<OptionSource> MyOwnCustomDefaultsAintThatNeat()
        {
            //your function can get them however you see fit
            yield return OptionSource.Typed("myfunction", "shitbird", 3);
            yield return OptionSource.Typed("myfunction", "shitbird2", "hello");
            yield return OptionSource.String("myfunction", "answertoeverything", "42");
        }
    }
}
