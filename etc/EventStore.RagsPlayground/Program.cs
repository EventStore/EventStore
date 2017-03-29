using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using EventStore.Rags;
using System.Net;

namespace RagsPlayground
{
    public class SomeOptionType
    {
        [ArgDescription("The first string", "firstgroup")]
        public string MyFirstOption { get; set; }

        [ArgDescription("The first string", "firstgroup")]
        public int MyFirstCount { get; set; }

        public string[] MyStringArrayOption { get; set; }

        public IPAddress[] MyIPAddresses { get; set; }

        public bool MyFlagOption { get; set; }

        [ArgDescription("Config", "firstgroup")]
        public string Config { get; set; }

        public Dictionary<string, string> Roles { get; set; }

        public SomeOptionType()
        {
            MyFirstCount = 5;
            MyFirstOption = "greg";
            MyStringArrayOption = new string[] { "first", "second" };
            MyIPAddresses = new IPAddress[] { IPAddress.Loopback, IPAddress.Loopback };
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
            var appliedConfig = resolvedConfig.ApplyTo<SomeOptionType>();
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

        private static IEnumerable<IEnumerable<OptionSource>> GetConfig(string[] args)
        {
            var commandline = CommandLine.Parse<SomeOptionType>(args).Normalize();
            var commanddict = commandline.ToDictionary(x => x.Name);
            yield return commandline;
            yield return
                EnvironmentVariables.Parse<SomeOptionType>(x => NameTranslators.PrefixEnvironmentVariable(x, "EVENTSTORE"));
            var configFile = commanddict.ContainsKey("config") ? commanddict["config"].Value as string : null;
            if (configFile != null && File.Exists(configFile))
            {
                yield return
                    Yaml.FromFile(configFile);
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
