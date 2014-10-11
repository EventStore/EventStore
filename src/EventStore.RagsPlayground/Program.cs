using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using EventStore.Rags;

namespace RagsPlayground
{

    class SomeOptionType
    {
        [ArgDescription("The first string", "firstgroup")]
        public string MyFirstOption;
        [ArgDescription("The first string", "firstgroup")]
        public int MyFirstCount;

        public SomeOptionType()
        {
            MyFirstCount = 5;
            MyFirstOption = "greg";
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            var sources = GetConfig();
            sources.ToList().ForEach(x => x.Dump());
            var merged = sources.MergeOptions((existing, potential) => true); //last one in wins but can write your own function
            merged.Dump();
            merged.ApplyTo<SomeOptionType>();
        }

        private static IEnumerable<IEnumerable<OptionSource>> GetConfig()
        {
            //var commandline = CommandLine.Parse(args);
            yield return
                EnvironmentVariables.Parse<SomeOptionType>(x => NameTranslators.PrefixEnvironmentVariables(x, "ES"));
            yield return
                Yaml.FromFile(@"C:\shitbird.yaml", "foo"); //can get config file from commandline if it exists or help etc
            yield return
                TypeDefaultOptions.Get<SomeOptionType>();
            yield return
                MyOwnCustomDefaultsAintThatNeat();
        }

        private static IEnumerable<OptionSource> MyOwnCustomDefaultsAintThatNeat()
        {
            //your function can get them however you see fit
            yield return new OptionSource("myfunction", "shitbird", "3");
            yield return new OptionSource("myfunction", "shitbird2", "hello");
            yield return new OptionSource("myfunction", "answertoeverything", "42");
        }
    }
}
