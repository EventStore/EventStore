// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text;
using EventStore.Common.Utils;
using Newtonsoft.Json.Linq;

namespace EventStore.Common.Options
{
    public class OptsHelper
    {
        static OptsHelper()
        {
	        TypeDescriptor.AddAttributes(typeof (IPAddress), new TypeConverterAttribute(typeof (IPAddressTypeConverter)));
	        TypeDescriptor.AddAttributes(typeof (IPEndPoint), new TypeConverterAttribute(typeof (IPEndPointTypeConverter)));
        }

        private readonly string _envPrefix;
	    private readonly string _defaultJsonConfigFile;
        private readonly string _configMember;
        private readonly OptionSet _optionSet;
        private readonly Dictionary<string, IOptionContainer> _optionContainers = new Dictionary<string, IOptionContainer>();

        public OptsHelper(Expression<Func<string[]>> configs, string envPrefix, string defaultJsonConfigFile)
        {
            Ensure.NotNull(envPrefix, "envPrefix");

            _envPrefix = envPrefix;
	        _defaultJsonConfigFile = defaultJsonConfigFile;
            _configMember = configs == null ? null : MemberName(configs);
            _optionSet = new OptionSet();
        }

        private static string MemberName<T>(Expression<Func<T>> expr)
        {
            Ensure.NotNull(expr, "expr");

            var memberExpr = expr.Body as MemberExpression;
            if (memberExpr == null)
                throw new ArgumentException("Given expression tree must represent MemberExpression.", "expr");

            return memberExpr.Member.Name;
        }
        
        public void Register<T>(Expression<Func<T>> member, string cmdPrototype, string envName, string jsonPath,
                                T? @default = null, string description = null, bool hidden = false, bool stopParseIfSet=false)
            where T : struct
        {
            Ensure.NotNull(member, "member");

            var optionName = MemberName(member);
            if (typeof(T) == typeof(bool))
            {
                var option = new OptionFlagContainer(optionName,
                                                     cmdPrototype,
                                                     GetEnvVarName(envName),
                                                     GetJsonPath(jsonPath),
                                                     (bool?)(object)@default, 
                                                     stopParseIfSet);
                _optionContainers.Add(optionName, option);
                if (cmdPrototype.IsNotEmptyString())
                    _optionSet.Add(cmdPrototype, description, option.ParsingFromCmdLine, hidden);
            }
            else
            {
                var option = new OptionValueContainer<T>(optionName,
                                                         cmdPrototype,
                                                         GetEnvVarName(envName),
                                                         GetJsonPath(jsonPath),
                                                         @default.HasValue,
                                                         @default ?? default(T));
                _optionContainers.Add(optionName, option);
                if (cmdPrototype.IsNotEmptyString())
                    _optionSet.Add(cmdPrototype, description, (T value) => option.ParsingFromCmdLine(value), hidden);
            }
        }

        public void RegisterRef<T>(Expression<Func<T>> member, string cmdPrototype, string envName, string jsonPath,
                                   T @default = null, string description = null, bool hidden = false) 
            where T : class
        {
            Ensure.NotNull(member, "member");

            var optionName = MemberName(member);
            var option = new OptionValueContainer<T>(optionName,
                                                     cmdPrototype,
                                                     GetEnvVarName(envName),
                                                     GetJsonPath(jsonPath),
                                                     @default != null,
                                                     @default);
            _optionContainers.Add(optionName, option);
            if (cmdPrototype.IsNotEmptyString())
                _optionSet.Add(cmdPrototype, description, (T value) => option.ParsingFromCmdLine(value), hidden);
        }

        public void RegisterArray<T>(Expression<Func<T[]>> member, string cmdPrototype, string envName, string separator,
                                     string jsonPath, T[] @default = null, string description = null, bool hidden = false)
        {
            Ensure.NotNull(member, "member");

            var optionName = MemberName(member);
            var option = new OptionArrayContainer<T>(optionName,
                                                     cmdPrototype,
                                                     GetEnvVarName(envName),
                                                     separator,
                                                     GetJsonPath(jsonPath),
                                                     @default);
            _optionContainers.Add(optionName, option);
            if (cmdPrototype.IsNotEmptyString())
                _optionSet.Add(cmdPrototype, description, (T value) => option.ParsingFromCmdLine(value), hidden);
        }

        private string GetEnvVarName(string envVarName)
        {
            return envVarName.IsEmptyString() ? null : (_envPrefix + envVarName).ToUpper();
        }

        private string[] GetJsonPath(string jsonPath)
        {
            if (jsonPath.IsEmptyString())
                return null;
            var parts = jsonPath.Split('.');
            foreach (var part in parts)
            {
                if (part.IsEmptyString() || part.Contains(" "))
                    throw new ArgumentException(string.Format("Wrong format of part of JSON path: {0}", part));
            }
            Debug.Assert(parts.Length > 0);
            return parts;
        }

        public string[] Parse(params string[] args)
        {
            var excessArguments = _optionSet.Parse(args).ToArray();

            if (_optionContainers.Values.Any(optionContainer => optionContainer.DontParseFurther))
            {
                return excessArguments;
            }

            foreach (var optionContainer in _optionContainers.Values)
            {
                if (optionContainer.IsSet)
                    continue;

                optionContainer.ParseFromEnvironment();
            }

            var jsonConfigPaths = new List<string>();
            var jsonConfigs = new List<Tuple<JObject, string>>();

            IOptionContainer configOption;
            if (_configMember != null && _optionContainers.TryGetValue(_configMember, out configOption))
            {
                var configOpt = configOption as OptionArrayContainer<string>;
                if (configOpt == null)
                    throw new InvalidOperationException(string.Format("Config option '{0}' is not of array of string type.", _configMember));

                if (configOpt.IsSet)
                    jsonConfigPaths.AddRange(configOpt.FinalValue);
            }

            // add default configs, if they exist
	        var pwd = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			if (pwd != null)
			{
				jsonConfigPaths.Add(Path.Combine(pwd, _defaultJsonConfigFile));
			}

            foreach (var configPath in jsonConfigPaths.Where(File.Exists))
            {
                try
                {
                    JObject json = JObject.Parse(File.ReadAllText(configPath));
                    jsonConfigs.Add(Tuple.Create(json, configPath));
                }
                catch (Exception exc)
                {
                    throw new Exception(string.Format("Couldn't parse JSON config at '{0}': {1}", configPath, exc.Message), exc);
                }
            }

            foreach (var optionContainer in _optionContainers.Values)
            {
                foreach (var jsonConfig in jsonConfigs)
                {
                    if (optionContainer.IsSet)
                        continue;

                    optionContainer.ParseFromConfig(jsonConfig.Item1, jsonConfig.Item2);
                }
            }

            foreach (var optionContainer in _optionContainers.Values)
            {
                if (!optionContainer.IsSet)
                {
                    if (!optionContainer.HasDefault)
                        throw new OptionException(string.Format("No value is provided for option '{0}'.", optionContainer.Name),
                                                  optionContainer.Name);
                    optionContainer.Origin = OptionOrigin.Default;
                    optionContainer.OriginName = "<DEFAULT>";
                    optionContainer.OriginOptionName = optionContainer.Name;
                }
            }

            return excessArguments;
        }

        public T Get<T>(Expression<Func<T>> member)
        {
            Ensure.NotNull(member, "member");
            var optionName = MemberName(member);

            IOptionContainer optionContainer;
            if (!_optionContainers.TryGetValue(optionName, out optionContainer))
                throw new InvalidOperationException(string.Format("No option '{0}' was registered.", optionName));

            Debug.Assert(optionContainer.IsSet || optionContainer.HasDefault);
            Debug.Assert(optionContainer.FinalValue != null);

            if (typeof (T) != optionContainer.FinalValue.GetType())
            {
                throw new InvalidOperationException(
                        string.Format("Wrong type of requested option {0}. Registered type: {1}, requested type: {2}.",
                                      optionName,
                                      optionContainer.FinalValue.GetType().Name,
                                      typeof (T).Name));
            }

            return (T) optionContainer.FinalValue;
        }

        public string GetUsage()
        {
            using (var stream = new StringWriter())
            {
                stream.WriteLine("Usage:");
                _optionSet.WriteOptionDescriptions(stream);
                return stream.ToString();
            }
        }

        public string DumpOptions()
        {
            var sb = new StringBuilder();
            foreach (var option in _optionContainers.Values)
            {
                var ss = new StringBuilder();
                foreach (var c in option.Name)
                {
                    if (ss.Length > 0 && char.IsLower(ss[ss.Length - 1]) && char.IsUpper(c))
                        ss.Append(' ');
                    ss.Append(c);
                }
                var optionName = ss.ToString().ToUpper();
                var value = option.FinalValue is IEnumerable<object>
                                    ? string.Join(", ", ((IEnumerable<object>) option.FinalValue).ToArray())
                                    : option.FinalValue;
                if (value is string && (string)value == "")
                    value = "<empty>";
                switch (option.Origin)
                {
                    case OptionOrigin.None:
                        throw new InvalidOperationException("Shouldn't get here ever.");
                    case OptionOrigin.CommandLine:
                        sb.AppendFormat("{0,-25} {1} ({2}{3} from command line)\n",
                                        optionName + ":",
                                        value,
                                        option.OriginOptionName.Length == 1 ? "-" : "--",
                                        option.OriginOptionName);
                        break;
                    case OptionOrigin.Environment:
                        sb.AppendFormat("{0,-25} {1} ({2} environment variable)\n",
                                        optionName + ":",
                                        value,
                                        option.OriginOptionName);
                        break;
                    case OptionOrigin.Config:
                        sb.AppendFormat("{0,-25} {1} ({2} in config at '{3}')\n",
                                        optionName + ":",
                                        value,
                                        option.OriginOptionName,
                                        option.OriginName);
                        break;
                    case OptionOrigin.Default:
                        sb.AppendFormat("{0,-25} {1} (<DEFAULT>)\n", optionName + ":", value);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return sb.ToString();
        }
    }
}