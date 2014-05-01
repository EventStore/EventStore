﻿using System.Collections.Generic;

namespace PowerArgs
{
    internal class ArgParser
    {
        internal static ParseResult Parse(PowerArgs.ArgHook.HookContext context)
        {
            var args = context.CmdLineArgs;

            ParseResult result = new ParseResult();

            int argumentPosition = 0;
            for (int i = 0; i < args.Length; i++)
            {
                var token = args[i];

                if (i == 0 && context.Definition.Actions.Count > 0 && context.Definition.FindMatchingAction(token) != null)
                {
                    result.ImplicitParameters.Add(0, token);
                    argumentPosition++;
                }
                else if (token.StartsWith("/"))
                {
                    var param = ParseSlashExplicitOption(token);
                    if (result.ExplicitParameters.ContainsKey(param.Key)) throw new DuplicateArgException("Argument specified more than once: " + param.Key);
                    result.ExplicitParameters.Add(param.Key, param.Value);
                    argumentPosition = -1;
                }
                else if (token.StartsWith("-"))
                {
                    string key = token.Substring(1);

                    if (key.Length == 0) throw new ArgException("Missing argument value after '-'");

                    string value;

                    // Handles long form syntax --argName=argValue.
                    if (key.StartsWith("-") && key.Contains("="))
                    {
                        var index = key.IndexOf("=");
                        value = key.Substring(index + 1);
                        key = key.Substring(0, index);
                    }
                    else
                    {
                        if (i == args.Length - 1)
                        {
                            value = "";
                        }
                        else if (IsBool(key, context))
                        {
                            var next = args[i + 1].ToLower();

                            if (next == "true" || next == "false" || next == "0" || next == "1")
                            {
                                i++;
                                value = next;
                            }
                            else
                            {
                                value = "true";
                            }
                        }
                        else
                        {
                            i++;
                            value = args[i];
                        }
                    }

                    if (result.ExplicitParameters.ContainsKey(key))
                    {
                        throw new DuplicateArgException("Argument specified more than once: " + key);
                    }

                    result.ExplicitParameters.Add(key, value);
                    argumentPosition = -1;
                }
                else
                {
                    if (argumentPosition < 0) throw new UnexpectedArgException("Unexpected argument: " + token);
                    result.ImplicitParameters.Add(argumentPosition, token);
                    argumentPosition++;
                }
            }

            return result;
        }

        private static bool IsBool(string key, PowerArgs.ArgHook.HookContext context)
        {
            var match = context.Definition.FindMatchingArgument(key, true);
            if (match == null) return false;

            return match.ArgumentType == typeof(bool);
        }

        private static KeyValuePair<string, string> ParseSlashExplicitOption(string a)
        {
            var key = a.Contains(":") ? a.Substring(1, a.IndexOf(":") - 1).Trim() : a.Substring(1, a.Length - 1);
            var value = a.Contains(":") ? a.Substring(a.IndexOf(":") + 1).Trim() : "";

            if (key.Length == 0) throw new ArgException("Missing argument value after '/'");

            return new KeyValuePair<string, string>(key, value);
        }
    }
}
