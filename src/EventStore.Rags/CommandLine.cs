using System.Collections.Generic;
using System.Linq;

namespace EventStore.Rags
{
    public class CommandLine
    {
        public static IEnumerable<OptionSource> Parse<TOptions>(string[] args) where TOptions : class
        {
            //TODO GFY drop lower here
            //From poking around we actually want to use argParser here not all of powerargs, it needs a definition and a context
            var ret = new List<OptionSource>();
            var parsedArguments = Parse(args);
            var properties = typeof(TOptions).GetProperties();
            foreach (var argument in parsedArguments)
            {
                var property = properties.FirstOrDefault(x => string.Equals(argument.Key, x.Name, System.StringComparison.OrdinalIgnoreCase));
                if (property != null)
                {
                    ret.Add(OptionSource.String("Command Line", property.Name, argument.Value));
                }
            }
            return ret;
        }

        internal static Dictionary<string, string> Parse(string[] args)
        {
            var result = new Dictionary<string, string>();
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
                    if (key.StartsWith("-") && key.EndsWith("-"))
                    {
                        key = key.Substring(1, key.Length - 2);
                    }
                    // Handles long form syntax --argName=argValue.
                    if (key.StartsWith("-") && key.Contains("="))
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

                    if (result.ContainsKey(key))
                    {
                        throw new DuplicateArgException("Argument specified more than once: " + key);
                    }

                    result.Add(key.Replace("-", ""), value);
                }
            }

            return result;
        }
    }
}