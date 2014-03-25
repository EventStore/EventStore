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