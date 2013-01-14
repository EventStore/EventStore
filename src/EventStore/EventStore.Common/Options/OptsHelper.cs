using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using EventStore.Common.CommandLine.lib;
using EventStore.Common.Exceptions;
using EventStore.Common.Utils;
using Mono.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Common.Options
{
    public enum OptionOrigin
    {
        None,
        CommandLine,
        Environment,
        Config
    }

    public class OptionInfo<T>
    {
        public readonly bool Success;

        public readonly T Value;
        public readonly OptionOrigin Origin;
        public readonly string OriginName;
        public readonly string OptionName;

        public readonly string Error;

        public OptionInfo(bool success, T value, OptionOrigin origin, string originName, string optionName, string error)
        {
            Success = success;
            Value = value;
            Origin = origin;
            OriginName = originName;
            OptionName = optionName;
            Error = error;
        }
    }

    public class OptsHelper<TOptions> where TOptions: CommandLineOptionsBase, new() 
    {
        private readonly string _envPrefix;
        private readonly JObject[] _configs;
        private readonly TOptions _options;

        public OptsHelper(Func<TOptions, string[]> configs, string envPrefix)
        {
            Ensure.NotNull(configs, "configs");
            Ensure.NotNull(envPrefix, "envPrefix");

            _envPrefix = envPrefix;

            _options = new TOptions();
            if (!CommandLineParser.Default.ParseArguments(Environment.GetCommandLineArgs(), _options, Console.Error, ""))
                throw new ApplicationInitializationException("Error while parsing options.");

            var configFiles = configs(_options);
            if (configFiles.IsEmpty())
                _configs = new JObject[0];
            else
            {
                _configs = new JObject[configFiles.Length];
                for (int i = 0; i < _configs.Length; ++i)
                {
                    try
                    {
                        _configs[i] = JObject.Parse(File.ReadAllText(configFiles[i]));
                    }
                    catch (Exception exc)
                    {
                        throw new ApplicationInitializationException("Error while loading configuration files.", exc);
                    }
                }
            }
        }

        private OptionInfo<TResult> GetOptionValue<TResult>(Func<TOptions, Tuple<bool, TResult>> cmdLineExtractor, 
                                                            string envVariable,
                                                            string configPath, 
                                                            Func<string, TResult> converter)
        {
            if (cmdLineExtractor != null)
            {
                var r = cmdLineExtractor(_options);
                if (r.Item1) // success
                    return new OptionInfo<TResult>(true, r.Item2, OptionOrigin.CommandLine, "CommandLine", "", string.Empty);
            }
            throw new NotImplementedException();
        }
    }

    public class SingleNodeOpts
    {
        private class SingeNodeCmdLineOpts
        {
            public SingeNodeCmdLineOpts()
            {
            }
        }
    }
}
