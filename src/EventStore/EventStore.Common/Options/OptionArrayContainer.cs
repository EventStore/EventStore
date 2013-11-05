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
using System.Linq;
using EventStore.Common.Utils;
using Newtonsoft.Json.Linq;

namespace EventStore.Common.Options
{
    internal class OptionArrayContainer<T> : IOptionContainer
    {
        object IOptionContainer.FinalValue { get { return FinalValue; } }

        public T[] FinalValue
        {
            get
            {
                if (Value == null && _default == null)
                    throw new InvalidOperationException(string.Format("No value provided for option '{0}'.", Name));
                return Value ?? _default;
            }
        }

        public string Name { get; private set; }
        public T[] Value { get; private set; }
        public bool IsSet { get { return Value != null; } }
        public bool HasDefault { get { return _default != null; } }

        public OptionOrigin Origin { get; set; }
        public string OriginName { get; set; }
        public string OriginOptionName { get; set; }

        private readonly string _cmdPrototype;
        private readonly string _envVariable;
        private readonly string _separator;
        private readonly string[] _jsonPath;
        private readonly T[] _default;

        private readonly List<T> _cmdLineList = new List<T>();

        public OptionArrayContainer(string name, string cmdPrototype, string envVariable, string separator, string[] jsonPath, T[] @default)
        {
            Ensure.NotNullOrEmpty(name, "name");
            if (envVariable.IsNotEmptyString() && separator.IsEmptyString())
                throw new ArgumentException("No value separator is provided for environment variable.");
            if (jsonPath != null && jsonPath.Length == 0)
                throw new ArgumentException("JsonPath array is empty.", "jsonPath");

            Name = name;
            _cmdPrototype = cmdPrototype;
            _envVariable = envVariable;
            _separator = separator;
            _jsonPath = jsonPath;
            _default = @default;

            Origin = OptionOrigin.None;
            OriginName = "<uninitialized>";
            OriginOptionName = name;
        }

        public void ParsingFromCmdLine(T value)
        {
            Origin = OptionOrigin.CommandLine;
            OriginName = OptionOrigin.CommandLine.ToString();
            OriginOptionName = _cmdPrototype.Split('|').Last().Trim('=');

            _cmdLineList.Add(value);
            Value = _cmdLineList.ToArray();
        }

        public bool DontParseFurther
        {
            get { return false; }
        }

        public void ParseFromEnvironment()
        {
            if (_envVariable.IsEmptyString())
                return;

            var varValue = Environment.GetEnvironmentVariable(_envVariable);
            if (varValue == null)
                return;

            Origin = OptionOrigin.Environment;
            OriginName = OptionOrigin.Environment.ToString();
            OriginOptionName = _envVariable;

            var parts = varValue.Split(new[] {_separator}, StringSplitOptions.None);

            var values = new List<T>();
            foreach (var part in parts)
            {
                try
                {
                    var value = OptionContainerHelpers.ConvertFromString<T>(part);
                    values.Add(value);
                }
                catch (Exception exc)
                {
                    throw new OptionException(
                            string.Format("Could not convert part of environment variable {0} (part: '{1}', value: '{2}') to type {3}.",
                                          _envVariable,
                                          part,
                                          varValue,
                                          typeof (T).Name),
                            _envVariable,
                            exc);
                }
            }

            Value = values.ToArray();
        }

        public void ParseFromConfig(JObject json, string configName)
        {
            Ensure.NotNullOrEmpty(configName, "configName");
            if (_jsonPath == null)
                return;

            Origin = OptionOrigin.Config;
            OriginName = configName;
            OriginOptionName = string.Join(".", _jsonPath);

            var token = OptionContainerHelpers.GetTokenByJsonPath(json, _jsonPath);
            if (token == null)
                return;

            if (token.Type != JTokenType.Array)
            {
                throw new OptionException(
                        string.Format("Property '{0}' (value: {1}) in JSON config at '{2}' is not array.",
                                      OriginOptionName,
                                      token,
                                      configName),
                        OriginOptionName);
            }

            var values = new List<T>();
            foreach (var item in (JArray) token)
            {
                try
                {
                    var value = OptionContainerHelpers.ConvertFromJToken<T>(item); 
                    values.Add(value);
                }
                catch (Exception exc)
                {
                    throw new OptionException(
                            string.Format("Could not convert part of JSON array {0} at '{1}' to type {2}. JToken: {3}.",
                                          OriginOptionName,
                                          configName,
                                          typeof(T).Name,
                                          token),
                            OriginOptionName,
                            exc);
                }
            }

            Value = values.ToArray();
        }
    }
}