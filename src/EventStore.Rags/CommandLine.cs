using System;
using System.Collections.Generic;
using System.Linq;

namespace EventStore.Rags
{
    public class CommandLine
    {
        public static IEnumerable<OptionSource> Parse<TOptions>(string[] args) where TOptions : class
        {
            var ret = new List<OptionSource>();
            var properties = typeof(TOptions).GetProperties();
            foreach (var argument in Parse(args).Normalize())
            {
                var property = properties.FirstOrDefault(x => string.Equals(argument.Item1, x.Name, System.StringComparison.OrdinalIgnoreCase));
                if (property != null)
                {
                    ret.Add(OptionSource.String("Command Line", property.Name, argument.Item2));
                }
            }
            return ret;
        }

        internal static IEnumerable<Tuple<string, string>> Parse(string[] args)
        {
            var result = new List<Tuple<string, string>>();
            for (int i = 0; i < args.Length; i++)
            {
                var token = args[i];

                if (token.StartsWith("-"))
                {
                    string key = token.Substring(1);

                    if (key.Length == 0) throw new ArgException("Missing argument value after '-'");

                    string value;

                    // Handles a special case --arg-name- where we have a trailing -
                    // it's a shortcut way of disabling an option
                    if (key.StartsWith("-") && key.EndsWith("-") || 
                        key.StartsWith("-") && key.EndsWith("+"))
                    {
                        value = key.Substring(key.Length - 1, 1);
                        key = key.Substring(1, key.Length - 2);
                    }
                    // Handles long form syntax --argName=argValue.
                    else if (key.StartsWith("-") && key.Contains("="))
                    {
                        var index = key.IndexOf("=");
                        value = key.Substring(index + 1);
                        key = key.Substring(1, index - 1);
                    }
                    else
                    {
                        if (key.StartsWith("-"))
                        {
                            key = key.Substring(1);
                        }
                        if (i == args.Length - 1)
                        {
                            value = "";
                        }
                        else
                        {
                            i++;
                            value = args[i];
                        }
                    }

                    yield return new Tuple<string, string>(key.Replace("-", ""), value);
                }
            }

            yield break;
        }
    }

    public static class CommandLineExtensions
    {
        public static IEnumerable<Tuple<string, string>> Normalize(this IEnumerable<Tuple<string, string>> source)
        {
            return source.Select(x => new Tuple<string, string>(x.Item1, 
                x.Item2 == "+" ? "True" : 
                x.Item2 == "-" ? "False" : x.Item2));
        }
    }
}