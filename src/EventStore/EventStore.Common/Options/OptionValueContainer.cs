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
using System.Linq;
using EventStore.Common.Utils;
using Newtonsoft.Json.Linq;

namespace EventStore.Common.Options
{
    internal class OptionValueContainer<T> : IOptionContainer
    {
        object IOptionContainer.FinalValue { get { return FinalValue; } }

        public T FinalValue
        {
            get
            {
                if (!_isSet && !_hasDefault)
                    throw new InvalidOperationException(string.Format("No value provided for option '{0}'.", Name));
                return _isSet ? Value : _default;
            }
        }

        public string Name { get; private set; }
        public T Value { get; private set; }
        public bool IsSet { get { return _isSet; } }
        public bool HasDefault { get { return _hasDefault; } }

        public OptionOrigin Origin { get; set; }
        public string OriginName { get; set; }
        public string OriginOptionName { get; set; }

        private readonly string _cmdPrototype;
        private readonly string _envVariable;
        private readonly string[] _jsonPath;
        
        private readonly T _default;
        private readonly bool _hasDefault;

        private bool _isSet;

        public OptionValueContainer(string name, string cmdPrototype, string envVariable, string[] jsonPath, bool hasDefault, T @default)
        {
            Ensure.NotNullOrEmpty(name, "name");
            if (jsonPath != null && jsonPath.Length == 0)
                throw new ArgumentException("JsonPath array is empty.", "jsonPath");

            Name = name;
            _cmdPrototype = cmdPrototype;
            _envVariable = envVariable;
            _jsonPath = jsonPath;

            _hasDefault = hasDefault;
            _default = @default;
            if (_hasDefault && _default == null)
                throw new ArgumentException("It is told that default is present, but default value is null.", "hasDefault");

            Origin = OptionOrigin.None;
            OriginName = "<uninitialized>";
            OriginOptionName = name;
            _isSet = false;
        }

        public void ParsingFromCmdLine(T value)
        {
            Origin = OptionOrigin.CommandLine;
            OriginName = OptionOrigin.CommandLine.ToString();
            OriginOptionName = _cmdPrototype.Split('|').Last().Trim('=');

            if (_isSet)
                throw new OptionException(string.Format("Option {0} is set more than once.", OriginOptionName), OriginOptionName);

            Value = value;
            _isSet = true;
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

            try
            {
                Value = OptionContainerHelpers.ConvertFromString<T>(varValue);
                _isSet = true;
            }
            catch (Exception exc)
            {
                throw new OptionException(
                        string.Format("Could not convert environment variable {0} (value: '{1}') to type {2}.",
                                      _envVariable,
                                      varValue,
                                      typeof(T).Name),
                        _envVariable,
                        exc);
            }
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

            try
            {
                Value = OptionContainerHelpers.ConvertFromJToken<T>(token);
                _isSet = true;
            }
            catch (Exception exc)
            {
                throw new OptionException(
                        string.Format("Could not convert JToken {0} at '{1}' to type {2}. JToken: {3}.",
                                      OriginOptionName,
                                      configName,
                                      typeof(T).Name,
                                      token),
                        OriginOptionName,
                        exc);
            }
        }
    }
}