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
using EventStore.Common.Options;
using EventStore.Common.Utils;
using Mono.Options;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Tests.Common.Options
{
    public class OptsHelper
    {
        static OptsHelper()
        {
            TypeDescriptor.AddAttributes(typeof(IPAddress), new TypeConverterAttribute(typeof(IPAddressTypeConverter)));
        }

        private readonly string _envPrefix;
        private readonly string _configMember;
        private readonly OptionSet _optionSet;
        private readonly Dictionary<string, IOptionContainer> _optionContainers = new Dictionary<string, IOptionContainer>();

        public OptsHelper(Expression<Func<string[]>> configs, string envPrefix)
        {
            Ensure.NotNull(envPrefix, "envPrefix");

            _envPrefix = envPrefix;
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

        public void RegisterFlag(Expression<Func<bool>> member, string cmdPrototype, string jsonPath, string envName, 
                                 bool? @default = null, string description = null)
        {
            Ensure.NotNull(member, "member");

            var optionName = MemberName(member);
            var option = new OptionFlagContainer(optionName,
                                                 cmdPrototype,
                                                 GetEnvVarName(envName),
                                                 GetJsonPath(jsonPath),
                                                 @default);
            _optionContainers.Add(optionName, option);
            if (cmdPrototype.IsNotEmptyString())
                _optionSet.Add(cmdPrototype, description, option.ParsingFromCmdLine);
        }

        public void Register<T>(Expression<Func<T>> member, string cmdPrototype, string jsonPath, string envName, 
                                T? @default = null, string description = null) 
            where T : struct
        {
            Ensure.NotNull(member, "member");

            var optionName = MemberName(member);
            var option = new OptionValueContainer<T>(optionName,
                                                     cmdPrototype,
                                                     GetEnvVarName(envName),
                                                     GetJsonPath(jsonPath),
                                                     @default.HasValue,
                                                     @default ?? default(T));
            _optionContainers.Add(optionName, option);
            if (cmdPrototype.IsNotEmptyString())
                _optionSet.Add(cmdPrototype, description, (T value) => option.ParsingFromCmdLine(value));
        }

        public void RegisterRef<T>(Expression<Func<T>> member, string cmdPrototype, string jsonPath, string envName, 
                                   T @default = null, string description = null) 
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
                _optionSet.Add(cmdPrototype, description, (T value) => option.ParsingFromCmdLine(value));
        }

        public void RegisterArray<T>(Expression<Func<T[]>> member, string cmdPrototype, string jsonPath, 
                                     string envName, string separator,
                                     T[] @default = null, string description = null)
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
                _optionSet.Add(cmdPrototype, description, (T value) => option.ParsingFromCmdLine(value));
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

        public void Parse(params string[] args)
        {
            _optionSet.Parse(args);

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
            jsonConfigPaths.Add(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"));

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
                if (!optionContainer.IsSet && !optionContainer.HasDefault)
                {
                    throw new OptionException(string.Format("No value is provided for option '{0}'.", optionContainer.Name),
                                              optionContainer.Name);
                }
            }
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
                sb.AppendFormat("{0}: {1} ({2} at {3}{4})",
                                option.Name.ToUpper().Replace("_", " "),
                                option.FinalValue,
                                option.OriginOptionName,
                                option.Origin,
                                option.Origin == OptionOrigin.Config ? " " + option.OriginName : string.Empty);
            }
            return sb.ToString();
        }
    }
}