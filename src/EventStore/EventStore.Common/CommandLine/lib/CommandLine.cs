#region License
//
// Command Line Library: CommandLine.cs
//
// Author:
//   Giacomo Stelluti Scala (gsscoder@gmail.com)
//
// Copyright (C) 2005 - 2012 Giacomo Stelluti Scala
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
#endregion
#region Edit Note
// source code copied from https://github.com/gsscoder/commandline 08.08.2012 on revision https://github.com/gsscoder/commandline/commit/1e2e01429d46418b0378fb1bb859251a9898b6da of branch master
// original file: https://github.com/gsscoder/commandline/commit/7abeabf838021c604a0df5520de42fdc65b09710#src/libcmdline/CommandLine.cs
#endregion
#region Using Directives


using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Reflection;
using System;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;
using System.Linq;


#endregion

namespace EventStore.Common.CommandLine.lib
{
    #region Attributes
    /// <summary>
    /// Provides base properties for creating an attribute, used to define rules for command line parsing.
    /// </summary>
    public abstract class BaseOptionAttribute : Attribute
    {
        private string _shortName;
        private object _defaultValue;
        private bool _hasDefaultValue;

        /// <summary>
        /// Short name of this command line option. You can use only one character.
        /// </summary>
        public string ShortName
        {
            get { return _shortName; }
            internal set
            {
                if (value != null && value.Length > 1)
                    throw new ArgumentException("Short name must have exactly 1 character", "shortName");

                _shortName = value;
            }
        }

        /// <summary>
        /// Long name of this command line option. This name is usually a single english word.
        /// </summary>
        public string LongName { get; internal set; }

        /// <summary>
        /// True if this command line option is required.
        /// </summary>
        public virtual bool Required { get; set; }

        /// <summary>
        /// Gets or sets mapped property default value.
        /// </summary>
        public object DefaultValue
        {
            get { return _defaultValue; }
            set
            {
                _defaultValue = value;
                _hasDefaultValue = true;
            }
        }

        internal bool HasShortName
        {
            get { return !string.IsNullOrEmpty(_shortName); }
        }

        internal bool HasLongName
        {
            get { return !string.IsNullOrEmpty(LongName); }
        }

        internal bool HasDefaultValue
        {
            get { return _hasDefaultValue; }
        }

        /// <summary>
        /// A short description of this command line option. Usually a sentence summary. 
        /// </summary>
        public string HelpText { get; set; }
    }

    /// <summary>
    /// Indicates the instance method that must be invoked when it becomes necessary show your help screen.
    /// The method signature is an instance method with no parameters and <see cref="System.String"/>
    /// return value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method,
            AllowMultiple = false,
            Inherited = true)]
    public sealed class HelpOptionAttribute : BaseOptionAttribute
    {
        private const string DEFAULT_HELP_TEXT = "Display this help screen.";

        /// <summary>
        /// Initializes a new instance of the <see cref="HelpOptionAttribute"/> class.
        /// </summary>
        public HelpOptionAttribute()
            : this(null, "help")
        {
            HelpText = DEFAULT_HELP_TEXT;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HelpOptionAttribute"/> class.
        /// Allows you to define short and long option names.
        /// </summary>
        /// <param name="shortName">The short name of the option or null if not used.</param>
        /// <param name="longName">The long name of the option or null if not used.</param>
        public HelpOptionAttribute(string shortName, string longName)
        {
            ShortName = shortName;
            LongName = longName;
            HelpText = DEFAULT_HELP_TEXT;
        }

        /// <summary>
        /// Returns always false for this kind of option.
        /// This behaviour can't be changed by design; if you try set <see cref="Required"/>
        /// an <see cref="System.InvalidOperationException"/> will be thrown.
        /// </summary>
        public override bool Required
        {
            get { return false; }
            set { throw new InvalidOperationException(); }
        }

        internal static void InvokeMethod(object target,
                Pair<MethodInfo, HelpOptionAttribute> pair, out string text)
        {
            text = null;

            var method = pair.Left;
            if (!CheckMethodSignature(method))
                throw new MemberAccessException();

            text = (string)method.Invoke(target, null);
        }

        private static bool CheckMethodSignature(MethodInfo value)
        {
            return value.ReturnType == typeof(string) && value.GetParameters().Length == 0;
        }
    }

    /// <summary>
    /// Models an option that can accept multiple values as separated arguments.
    /// </summary>
    public sealed class OptionArrayAttribute : OptionAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OptionArrayAttribute"/> class.
        /// </summary>
        /// <param name="shortName">The short name of the option or null if not used.</param>
        /// <param name="longName">The long name of the option or null if not used.</param>
        public OptionArrayAttribute(string shortName, string longName)
            : base(shortName, longName)
        {
        }
    }

    /// <summary>
    /// Models an option specification.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class OptionAttribute : BaseOptionAttribute
    {
        private readonly string _uniqueName;
        private string _mutuallyExclusiveSet;

        internal const string DefaultMutuallyExclusiveSet = "Default";

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionAttribute"/> class.
        /// </summary>
        /// <param name="shortName">The short name of the option or null if not used.</param>
        /// <param name="longName">The long name of the option or null if not used.</param>
        public OptionAttribute(string shortName, string longName)
        {
            if (!string.IsNullOrEmpty(shortName))
                _uniqueName = shortName;
            else if (!string.IsNullOrEmpty(longName))
                _uniqueName = longName;

            if (_uniqueName == null)
                throw new InvalidOperationException();

            base.ShortName = shortName;
            base.LongName = longName;
        }

#if UNIT_TESTS
        internal OptionInfo CreateOptionInfo()
        {
            return new OptionInfo(base.ShortName, base.LongName);
        }
#endif

        internal string UniqueName
        {
            get { return _uniqueName; }
        }

        /// <summary>
        /// Gets or sets the option's mutually exclusive set.
        /// </summary>
        public string MutuallyExclusiveSet
        {
            get { return _mutuallyExclusiveSet; }
            set
            {
                if (string.IsNullOrEmpty(value))
                    _mutuallyExclusiveSet = OptionAttribute.DefaultMutuallyExclusiveSet;
                else
                    _mutuallyExclusiveSet = value;
            }
        }
    }

    /// <summary>
    /// Models an option that can accept multiple values.
    /// Must be applied to a field compatible with an <see cref="System.Collections.Generic.IList&lt;T&gt;"/> interface
    /// of <see cref="System.String"/> instances.
    /// </summary>
    public sealed class OptionListAttribute : OptionAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OptionListAttribute"/> class.
        /// </summary>
        /// <param name="shortName">The short name of the option or null if not used.</param>
        /// <param name="longName">The long name of the option or null if not used.</param>
        public OptionListAttribute(string shortName, string longName)
            : base(shortName, longName)
        {
            Separator = ':';
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionListAttribute"/> class.
        /// </summary>
        /// <param name="shortName">The short name of the option or null if not used.</param>
        /// <param name="longName">The long name of the option or null if not used.</param>
        /// <param name="separator">Values separator character.</param>
        public OptionListAttribute(string shortName, string longName, char separator)
            : base(shortName, longName)
        {
            Separator = separator;
        }

        /// <summary>
        /// Gets or sets the values separator character.
        /// </summary>
        public char Separator { get; set; }
    }

    /// <summary>
    /// Models a list of command line arguments that are not options.
    /// Must be applied to a field compatible with an <see cref="System.Collections.Generic.IList&lt;T&gt;"/> interface
    /// of <see cref="System.String"/> instances.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property,
            AllowMultiple = false,
            Inherited = true)]
    public sealed class ValueListAttribute : Attribute
    {
        private readonly Type _concreteType;

        private ValueListAttribute()
        {
            MaximumElements = -1;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueListAttribute"/> class.
        /// </summary>
        /// <param name="concreteType">A type that implements <see cref="System.Collections.Generic.IList&lt;T&gt;"/>.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="concreteType"/> is null.</exception>
        public ValueListAttribute(Type concreteType)
            : this()
        {
            if (concreteType == null)
                throw new ArgumentNullException("concreteType");

            if (!typeof(IList<string>).IsAssignableFrom(concreteType))
                throw new CommandLineParserException("The types are incompatible.");

            _concreteType = concreteType;
        }

        /// <summary>
        /// Gets or sets the maximum element allow for the list managed by <see cref="ValueListAttribute"/> type.
        /// If lesser than 0, no upper bound is fixed.
        /// If equal to 0, no elements are allowed.
        /// </summary>
        public int MaximumElements { get; set; }

        internal Type ConcreteType
        {
            get { return _concreteType; }
        }

        internal static IList<string> GetReference(object target)
        {
            Type concreteType;
            var property = GetProperty(target, out concreteType);

            if (property == null || concreteType == null)
                return null;

            property.SetValue(target, Activator.CreateInstance(concreteType), null);

            return (IList<string>)property.GetValue(target, null);
        }

        internal static ValueListAttribute GetAttribute(object target)
        {
            var list = ReflectionUtil.RetrievePropertyList<ValueListAttribute>(target);
            if (list == null || list.Count == 0)
                return null;

            if (list.Count > 1)
                throw new InvalidOperationException();

            var pairZero = list[0];

            return pairZero.Right;
        }

        private static PropertyInfo GetProperty(object target, out Type concreteType)
        {
            concreteType = null;

            var list = ReflectionUtil.RetrievePropertyList<ValueListAttribute>(target);
            if (list == null || list.Count == 0)
                return null;

            if (list.Count > 1)
                throw new InvalidOperationException();

            var pairZero = list[0];
            concreteType = pairZero.Right.ConcreteType;

            return pairZero.Left;
        }
    }
    #endregion

    #region Core
    [Flags]
    internal enum ParserState : ushort
    {
        Success = 0x01,
        Failure = 0x02,
        MoveOnNextElement = 0x04
    }

    internal abstract class ArgumentParser
    {
        protected ArgumentParser()
        {
            this.PostParsingState = new List<ParsingError>();
        }

        public abstract ParserState Parse(IArgumentEnumerator argumentEnumerator, OptionMap map, object options);

        public List<ParsingError> PostParsingState { get; private set; }

        protected void DefineOptionThatViolatesFormat(OptionInfo option)
        {
            //this.PostParsingState.BadOptionInfo = option;
            //this.PostParsingState.ViolatesFormat = true;
            this.PostParsingState.Add(new ParsingError(option.ShortName, option.LongName, true));
        }

        public static ArgumentParser Create(string argument, bool ignoreUnknownArguments = false)
        {
            if (argument.Equals("-", StringComparison.InvariantCulture))
                return null;

            if (argument[0] == '-' && argument[1] == '-')
                return new LongOptionParser(ignoreUnknownArguments);

            if (argument[0] == '-')
                return new OptionGroupParser(ignoreUnknownArguments);

            return null;
        }

        public static bool IsInputValue(string argument)
        {
            if (argument.Length > 0)
                return argument.Equals("-", StringComparison.InvariantCulture) || argument[0] != '-';

            return true;
        }

#if UNIT_TESTS
        public static IList<string> PublicWrapperOfGetNextInputValues(IArgumentEnumerator ae)
        {
            return GetNextInputValues(ae);
        }
#endif

        protected static IList<string> GetNextInputValues(IArgumentEnumerator ae)
        {
            IList<string> list = new List<string>();

            while (ae.MoveNext())
            {
                if (IsInputValue(ae.Current))
                    list.Add(ae.Current);
                else
                    break;
            }
            if (!ae.MovePrevious())
                throw new CommandLineParserException();

            return list;
        }

        public static bool CompareShort(string argument, string option, bool caseSensitive)
        {
            return string.Compare(argument, "-" + option, !caseSensitive) == 0;
        }

        public static bool CompareLong(string argument, string option, bool caseSensitive)
        {
            return string.Compare(argument, "--" + option, !caseSensitive) == 0;
        }

        protected static ParserState BooleanToParserState(bool value)
        {
            return BooleanToParserState(value, false);
        }

        protected static ParserState BooleanToParserState(bool value, bool addMoveNextIfTrue)
        {
            if (value && !addMoveNextIfTrue)
                return ParserState.Success;

            if (value)
                return ParserState.Success | ParserState.MoveOnNextElement;

            return ParserState.Failure;
        }

        protected static void EnsureOptionAttributeIsArrayCompatible(OptionInfo option)
        {
            if (!option.IsAttributeArrayCompatible)
                throw new CommandLineParserException();
        }

        protected static void EnsureOptionArrayAttributeIsNotBoundToScalar(OptionInfo option)
        {
            if (!option.IsArray && option.IsAttributeArrayCompatible)
                throw new CommandLineParserException();
        }
    }

    /// <summary>
    /// Models a bad parsed option.
    /// </summary>
    public sealed class BadOptionInfo
    {
        internal BadOptionInfo()
        {
        }

        internal BadOptionInfo(string shortName, string longName)
        {
            ShortName = shortName;
            LongName = longName;
        }

        /// <summary>
        /// The short name of the option
        /// </summary>
        /// <value>Returns the short name of the option.</value>
        public string ShortName
        {
            get;
            internal set;
        }

        /// <summary>
        /// The long name of the option
        /// </summary>
        /// <value>Returns the long name of the option.</value>
        public string LongName
        {
            get;
            internal set;
        }
    }

    internal interface IArgumentEnumerator : IDisposable
    {
        string GetRemainingFromNext();

        string Next { get; }
        bool IsLast { get; }

        bool MoveNext();

        bool MovePrevious();

        string Current { get; }
    }

    internal sealed class LongOptionParser : ArgumentParser
    {
        private readonly bool _ignoreUnkwnownArguments;

        public LongOptionParser(bool ignoreUnkwnownArguments)
        {
            _ignoreUnkwnownArguments = ignoreUnkwnownArguments;
        }

        public override ParserState Parse(IArgumentEnumerator argumentEnumerator, OptionMap map, object options)
        {
            var parts = argumentEnumerator.Current.Substring(2).Split(new[] { '=' }, 2);
            var option = map[parts[0]];
            bool valueSetting;

            if (option == null)
            {
                if (_ignoreUnkwnownArguments)
                    return ParserState.MoveOnNextElement;

                this.PostParsingState.Add(new ParsingError(null, parts[0], true));
                return ParserState.Failure;
            }

            option.IsDefined = true;

            ArgumentParser.EnsureOptionArrayAttributeIsNotBoundToScalar(option);

            if (!option.IsBoolean)
            {
                if (parts.Length == 1 && (argumentEnumerator.IsLast || !ArgumentParser.IsInputValue(argumentEnumerator.Next)))
                    return ParserState.Failure;

                if (parts.Length == 2)
                {
                    if (!option.IsArray)
                    {
                        valueSetting = option.SetValue(parts[1], options);
                        if (!valueSetting)
                            this.DefineOptionThatViolatesFormat(option);

                        return ArgumentParser.BooleanToParserState(valueSetting);
                    }

                    ArgumentParser.EnsureOptionAttributeIsArrayCompatible(option);

                    var items = ArgumentParser.GetNextInputValues(argumentEnumerator);
                    items.Insert(0, parts[1]);

                    valueSetting = option.SetValue(items, options);
                    if (!valueSetting)
                        this.DefineOptionThatViolatesFormat(option);

                    return ArgumentParser.BooleanToParserState(valueSetting);
                }
                else
                {
                    if (!option.IsArray)
                    {
                        valueSetting = option.SetValue(argumentEnumerator.Next, options);
                        if (!valueSetting)
                            this.DefineOptionThatViolatesFormat(option);

                        return ArgumentParser.BooleanToParserState(valueSetting, true);
                    }

                    ArgumentParser.EnsureOptionAttributeIsArrayCompatible(option);

                    var items = ArgumentParser.GetNextInputValues(argumentEnumerator);

                    valueSetting = option.SetValue(items, options);
                    if (!valueSetting)
                        this.DefineOptionThatViolatesFormat(option);

                    //return ArgumentParser.BooleanToParserState(valueSetting, true);
                    return ArgumentParser.BooleanToParserState(valueSetting);
                }
            }

            if (parts.Length == 2)
            {
                this.DefineOptionThatViolatesFormat(option);
                return ParserState.Failure;
            }

            valueSetting = option.SetValue(true, options);
            if (!valueSetting)
                this.DefineOptionThatViolatesFormat(option);

            return ArgumentParser.BooleanToParserState(valueSetting);
        }
    }

    internal sealed class OneCharStringEnumerator : IArgumentEnumerator
    {
        private string _currentElement;
        private int _index;
        private readonly string _data;

        public OneCharStringEnumerator(string value)
        {
            Assumes.NotNullOrEmpty(value, "value");

            _data = value;
            _index = -1;
        }

        public string Current
        {
            get
            {
                if (_index == -1)
                    throw new InvalidOperationException();

                if (_index >= _data.Length)
                    throw new InvalidOperationException();

                return _currentElement;
            }
        }

        public string Next
        {
            get
            {
                if (_index == -1)
                    throw new InvalidOperationException();

                if (_index > _data.Length)
                    throw new InvalidOperationException();

                if (IsLast)
                    return null;

                return _data.Substring(_index + 1, 1);
            }
        }

        public bool IsLast
        {
            get { return _index == _data.Length - 1; }
        }

        public void Reset()
        {
            _index = -1;
        }

        public bool MoveNext()
        {
            if (_index < (_data.Length - 1))
            {
                _index++;
                _currentElement = _data.Substring(_index, 1);
                return true;
            }
            _index = _data.Length;

            return false;
        }

        public string GetRemainingFromNext()
        {
            if (_index == -1)
                throw new InvalidOperationException();

            if (_index > _data.Length)
                throw new InvalidOperationException();

            return _data.Substring(_index + 1);
        }

        public bool MovePrevious()
        {
            throw new NotSupportedException();
        }

        void IDisposable.Dispose()
        {
        }
    }

    internal sealed class OptionGroupParser : ArgumentParser
    {
        private readonly bool _ignoreUnkwnownArguments;

        public OptionGroupParser(bool ignoreUnkwnownArguments)
        {
            _ignoreUnkwnownArguments = ignoreUnkwnownArguments;
        }

        public override ParserState Parse(IArgumentEnumerator argumentEnumerator, OptionMap map, object options)
        {
            IArgumentEnumerator group = new OneCharStringEnumerator(argumentEnumerator.Current.Substring(1));
            while (group.MoveNext())
            {
                var option = map[group.Current];
                if (option == null)
                    return _ignoreUnkwnownArguments ? ParserState.MoveOnNextElement : ParserState.Failure;

                option.IsDefined = true;

                ArgumentParser.EnsureOptionArrayAttributeIsNotBoundToScalar(option);

                if (!option.IsBoolean)
                {
                    if (argumentEnumerator.IsLast && group.IsLast)
                        return ParserState.Failure;

                    bool valueSetting;
                    if (!group.IsLast)
                    {
                        if (!option.IsArray)
                        {
                            valueSetting = option.SetValue(group.GetRemainingFromNext(), options);
                            if (!valueSetting)
                                this.DefineOptionThatViolatesFormat(option);

                            return ArgumentParser.BooleanToParserState(valueSetting);
                        }

                        ArgumentParser.EnsureOptionAttributeIsArrayCompatible(option);

                        var items = ArgumentParser.GetNextInputValues(argumentEnumerator);
                        items.Insert(0, @group.GetRemainingFromNext());

                        valueSetting = option.SetValue(items, options);
                        if (!valueSetting)
                            this.DefineOptionThatViolatesFormat(option);

                        return ArgumentParser.BooleanToParserState(valueSetting, true);
                    }

                    if (!argumentEnumerator.IsLast && !ArgumentParser.IsInputValue(argumentEnumerator.Next))
                        return ParserState.Failure;
                    else
                    {
                        if (!option.IsArray)
                        {
                            valueSetting = option.SetValue(argumentEnumerator.Next, options);
                            if (!valueSetting)
                                this.DefineOptionThatViolatesFormat(option);

                            return ArgumentParser.BooleanToParserState(valueSetting, true);
                        }

                        ArgumentParser.EnsureOptionAttributeIsArrayCompatible(option);

                        var items = ArgumentParser.GetNextInputValues(argumentEnumerator);

                        valueSetting = option.SetValue(items, options);
                        if (!valueSetting)
                            this.DefineOptionThatViolatesFormat(option);

                        return ArgumentParser.BooleanToParserState(valueSetting);
                    }
                }

                if (!@group.IsLast && map[@group.Next] == null)
                    return ParserState.Failure;

                if (!option.SetValue(true, options))
                    return ParserState.Failure;
            }

            return ParserState.Success;
        }
    }

    [DebuggerDisplay("ShortName = {ShortName}, LongName = {LongName}")]
    internal sealed class OptionInfo
    {
        private readonly OptionAttribute _attribute;
        private readonly PropertyInfo _property;
        private readonly bool _required;
        private readonly string _helpText;
        private readonly string _shortName;
        private readonly string _longName;
        private readonly string _mutuallyExclusiveSet;
        private readonly object _defaultValue;
        private readonly bool _hasDefaultValue;
        private readonly object _setValueLock = new object();

        private readonly Dictionary<Type, Func<string, object>> _customParsableTypes = new Dictionary<Type, Func<string, object>>()
        {
            {typeof(IPAddress), IPAddress.Parse}
        };

        public OptionInfo(OptionAttribute attribute, PropertyInfo property)
        {
            if (attribute != null)
            {
                _required = attribute.Required;
                _helpText = attribute.HelpText;
                _shortName = attribute.ShortName;
                _longName = attribute.LongName;
                _mutuallyExclusiveSet = attribute.MutuallyExclusiveSet;
                _defaultValue = attribute.DefaultValue;
                _hasDefaultValue = attribute.HasDefaultValue;
                _attribute = attribute;
            }
            else
                throw new ArgumentNullException("attribute", "The attribute is mandatory");

            if (property != null)
                _property = property;
            else
                throw new ArgumentNullException("property", "The property is mandatory");
        }

#if UNIT_TESTS
        internal OptionInfo(string shortName, string longName)
        {
            _shortName = shortName;
            _longName = longName;
        }
#endif

        public static OptionMap CreateMap(object target, CommandLineParserSettings settings)
        {
            var list = ReflectionUtil.RetrievePropertyList<OptionAttribute>(target);
            if (list != null)
            {
                var map = new OptionMap(list.Count, settings);

                foreach (var pair in list)
                {
                    if (pair != null && pair.Right != null)
                        map[pair.Right.UniqueName] = new OptionInfo(pair.Right, pair.Left);
                }

                map.RawOptions = target;

                return map;
            }

            return null;
        }

        public bool SetValue(string value, object options)
        {
            if (_attribute is OptionListAttribute)
                return SetValueList(value, options);

            if (ReflectionUtil.IsNullableType(_property.PropertyType))
                return SetNullableValue(value, options);

            if (_customParsableTypes.ContainsKey(_property.PropertyType))
                return SetParsableValue(value, options);

            return SetValueScalar(value, options);
        }

        public bool SetValue(IList<string> values, object options)
        {
            Type elementType = _property.PropertyType.GetElementType();
            Array array = Array.CreateInstance(elementType, values.Count);

            for (int i = 0; i < array.Length; i++)
            {
                try
                {
                    lock (_setValueLock)
                    {
                        //array.SetValue(Convert.ChangeType(values[i], elementType, CultureInfo.InvariantCulture), i);
                        array.SetValue(Convert.ChangeType(values[i], elementType, Thread.CurrentThread.CurrentCulture), i);
                        _property.SetValue(options, array, null);
                    }
                }
                catch (FormatException)
                {
                    return false;
                }
            }

            return true;
        }

        private bool SetParsableValue(string value, object options)
        {
            try
            {
                var parser = _customParsableTypes[_property.PropertyType];
                object parsedValue = parser(value);
                lock (_setValueLock)
                {
                    _property.SetValue(options, parsedValue, null);
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        private bool SetValueScalar(string value, object options)
        {
            try
            {
                if (_property.PropertyType.IsEnum)
                {
                    lock (_setValueLock)
                    {
                        _property.SetValue(options, Enum.Parse(_property.PropertyType, value, true), null);
                    }
                }
                else
                {
                    lock (_setValueLock)
                    {
                        //_property.SetValue(options, Convert.ChangeType(value, _property.PropertyType, CultureInfo.InvariantCulture), null);
                        _property.SetValue(options, Convert.ChangeType(value, _property.PropertyType, Thread.CurrentThread.CurrentCulture), null);
                    }
                }
            }
            catch (InvalidCastException) // Convert.ChangeType
            {
                return false;
            }
            catch (FormatException) // Convert.ChangeType
            {
                return false;
            }
            catch (ArgumentException) // Enum.Parse
            {
                return false;
            }

            return true;
        }

        private bool SetNullableValue(string value, object options)
        {
            var nc = new NullableConverter(_property.PropertyType);

            try
            {
                lock (_setValueLock)
                {
                    //_property.SetValue(options, nc.ConvertFromString(null, CultureInfo.InvariantCulture, value), null);
                    _property.SetValue(options, nc.ConvertFromString(null, Thread.CurrentThread.CurrentCulture, value), null);
                }
            }
            // the FormatException (thrown by ConvertFromString) is thrown as Exception.InnerException,
            // so we've catch directly Exception
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public bool SetValue(bool value, object options)
        {
            lock (_setValueLock)
            {
                _property.SetValue(options, value, null);

                return true;
            }
        }

        private bool SetValueList(string value, object options)
        {
            lock (_setValueLock)
            {
                _property.SetValue(options, new List<string>(), null);

                var fieldRef = (IList<string>)_property.GetValue(options, null);
                var values = value.Split(((OptionListAttribute)_attribute).Separator);

                for (int i = 0; i < values.Length; i++)
                {
                    fieldRef.Add(values[i]);
                }

                return true;
            }
        }

        public void SetDefault(object options)
        {
            if (_hasDefaultValue)
            {
                lock (_setValueLock)
                {
                    try
                    {
                        _property.SetValue(options, _defaultValue, null);
                    }
                    catch (Exception e)
                    {
                        throw new CommandLineParserException("Bad default value.", e);
                    }
                }
            }
        }

        public string ShortName
        {
            get { return _shortName; }
        }

        public string LongName
        {
            get { return _longName; }
        }

        internal string NameWithSwitch
        {
            get
            {
                if (_longName != null)
                    return string.Concat("--", _longName);

                return string.Concat("-", _shortName);
            }
        }

        public string MutuallyExclusiveSet
        {
            get { return _mutuallyExclusiveSet; }
        }

        public bool Required
        {
            get { return _required; }
        }

        public string HelpText
        {
            get { return _helpText; }
        }

        public bool IsBoolean
        {
            get { return _property.PropertyType == typeof(bool); }
        }

        public bool IsArray
        {
            get { return _property.PropertyType.IsArray; }
        }

        public bool IsAttributeArrayCompatible
        {
            get { return _attribute is OptionArrayAttribute; }
        }

        public bool IsDefined { get; set; }

        public bool HasBothNames
        {
            get { return (_shortName != null && _longName != null); }
        }
    }

    internal sealed class OptionMap
    {
        sealed class MutuallyExclusiveInfo
        {
            int _count;

            public MutuallyExclusiveInfo(OptionInfo option)
            {
                BadOption = option;
            }

            public OptionInfo BadOption { get; private set; }

            public void IncrementOccurrence() { ++_count; }

            public int Occurrence { get { return _count; } }
        }

        readonly CommandLineParserSettings _settings;
        readonly Dictionary<string, string> _names;
        readonly Dictionary<string, OptionInfo> _map;
        readonly Dictionary<string, MutuallyExclusiveInfo> _mutuallyExclusiveSetMap;

        public OptionMap(int capacity, CommandLineParserSettings settings)
        {
            _settings = settings;

            IEqualityComparer<string> comparer;
            if (_settings.CaseSensitive)
                comparer = StringComparer.Ordinal;
            else
                comparer = StringComparer.OrdinalIgnoreCase;

            _names = new Dictionary<string, string>(capacity, comparer);
            _map = new Dictionary<string, OptionInfo>(capacity * 2, comparer);

            if (_settings.MutuallyExclusive)
            {
                //_mutuallyExclusiveSetMap = new Dictionary<string, int>(capacity, StringComparer.OrdinalIgnoreCase);
                _mutuallyExclusiveSetMap = new Dictionary<string, MutuallyExclusiveInfo>(capacity, StringComparer.OrdinalIgnoreCase);
            }
        }

        public OptionInfo this[string key]
        {
            get
            {
                OptionInfo option = null;

                if (_map.ContainsKey(key))
                    option = _map[key];
                else
                {
                    if (_names.ContainsKey(key))
                    {
                        var optionKey = _names[key];
                        option = _map[optionKey];
                    }
                }

                return option;
            }
            set
            {
                _map[key] = value;

                if (value.HasBothNames)
                    _names[value.LongName] = value.ShortName;
            }
        }

        public string[] GetMapKeys()
        {
            return _map.Keys.ToArray();
        }

        internal object RawOptions { private get; set; }

        public bool EnforceRules()
        {
            return EnforceMutuallyExclusiveMap() && EnforceRequiredRule();
        }

        public void SetDefaults()
        {
            foreach (OptionInfo option in _map.Values)
            {
                option.SetDefault(this.RawOptions);
            }
        }

        private bool EnforceRequiredRule()
        {
            foreach (OptionInfo option in _map.Values)
            {
                if (option.Required && !option.IsDefined)
                {
                    BuildAndSetPostParsingStateIfNeeded(this.RawOptions, option, true, null);
                    return false;
                }
            }
            return true;
        }

        private bool EnforceMutuallyExclusiveMap()
        {
            if (!_settings.MutuallyExclusive)
                return true;

            foreach (OptionInfo option in _map.Values)
            {
                if (option.IsDefined && option.MutuallyExclusiveSet != null)
                    BuildMutuallyExclusiveMap(option);
            }

            //foreach (int occurrence in _mutuallyExclusiveSetMap.Values)
            foreach (MutuallyExclusiveInfo info in _mutuallyExclusiveSetMap.Values)
            {
                if (info.Occurrence > 1) //if (occurrence > 1)
                {
                    //BuildAndSetPostParsingStateIfNeeded(this.RawOptions, null, null, true);
                    BuildAndSetPostParsingStateIfNeeded(this.RawOptions, info.BadOption, null, true);
                    return false;
                }
            }

            return true;
        }

        private void BuildMutuallyExclusiveMap(OptionInfo option)
        {
            var setName = option.MutuallyExclusiveSet;

            if (!_mutuallyExclusiveSetMap.ContainsKey(setName))
            {
                //_mutuallyExclusiveSetMap.Add(setName, 0);
                _mutuallyExclusiveSetMap.Add(setName, new MutuallyExclusiveInfo(option));
            }

            _mutuallyExclusiveSetMap[setName].IncrementOccurrence();
        }

        private static void BuildAndSetPostParsingStateIfNeeded(object options, OptionInfo option, bool? required, bool? mutualExclusiveness)
        {
            var commandLineOptionsBase = options as CommandLineOptionsBase;
            if (commandLineOptionsBase == null)
                return;

            var error = new ParsingError
            {
                BadOption =
                {
                    ShortName = option.ShortName,
                    LongName = option.LongName
                }
            };

            if (required != null) error.ViolatesRequired = required.Value;
            if (mutualExclusiveness != null) error.ViolatesMutualExclusiveness = mutualExclusiveness.Value;

            (commandLineOptionsBase).InternalLastPostParsingState.Errors.Add(error);
        }
    }

    internal sealed class Pair<TLeft, TRight>
        where TLeft : class
        where TRight : class
    {
        private readonly TLeft _left;
        private readonly TRight _right;

        public Pair(TLeft left, TRight right)
        {
            _left = left;
            _right = right;
        }

        public TLeft Left
        {
            get { return _left; }
        }

        public TRight Right
        {
            get { return _right; }
        }

        public override int GetHashCode()
        {
            int leftHash = (_left == null ? 0 : _left.GetHashCode());
            int rightHash = (_right == null ? 0 : _right.GetHashCode());

            return leftHash ^ rightHash;
        }

        public override bool Equals(object obj)
        {
            var other = obj as Pair<TLeft, TRight>;

            if (other == null)
                return false;

            return Equals(_left, other._left) && Equals(_right, other._right);
        }
    }

    /// <summary>
    /// Models a parsing error.
    /// </summary>
    public class ParsingError
    {
        internal ParsingError()
        {
            this.BadOption = new BadOptionInfo();
        }

        internal ParsingError(string shortName, string longName, bool format, bool unknownOption = false)
        {
            //this.BadOptionShortName = shortName;
            //this.BadOptionLongName = longName;
            this.BadOption = new BadOptionInfo(shortName, longName);

            this.ViolatesFormat = format;
            this.UnknownOption = unknownOption;
        }

        /// <summary>
        /// Gets or a the bad parsed option.
        /// </summary>
        /// <value>
        /// The bad option.
        /// </value>
        public BadOptionInfo BadOption { get; private set; }


        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="ParsingError"/> violates required.
        /// </summary>
        /// <value>
        /// <c>true</c> if violates required; otherwise, <c>false</c>.
        /// </value>
        public bool ViolatesRequired { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="ParsingError"/> violates format.
        /// </summary>
        /// <value>
        /// <c>true</c> if violates format; otherwise, <c>false</c>.
        /// </value>
        public bool ViolatesFormat { get; set; }

        public bool UnknownOption { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="ParsingError"/> violates mutual exclusiveness.
        /// </summary>
        /// <value>
        /// <c>true</c> if violates mutual exclusiveness; otherwise, <c>false</c>.
        /// </value>
        public bool ViolatesMutualExclusiveness { get; set; }

        public ParsingEnvironment ViolatingEnvironment { get; set; }
    }

    public enum ParsingEnvironment { CommandLine = 0, EnvironmentVariable, AppConfig }

    /// <summary>
    /// Models a type that records the parser state afeter parsing.
    /// </summary>
    public sealed class PostParsingState
    {
        internal PostParsingState()
        {
            Errors = new List<ParsingError>();
        }

        /// <summary>
        /// Gets a list of parsing errors.
        /// </summary>
        /// <value>
        /// Parsing errors.
        /// </value>
        public List<ParsingError> Errors { get; private set; }
    }

    internal sealed class StringArrayEnumerator : IArgumentEnumerator
    {
        private readonly string[] _data;
        private int _index;
        private readonly int _endIndex;

        public StringArrayEnumerator(string[] value)
        {
            Assumes.NotNull(value, "value");

            _data = value;
            _index = -1;
            _endIndex = value.Length;
        }

        public string Current
        {
            get
            {
                if (_index == -1)
                {
                    throw new InvalidOperationException();
                }
                if (_index >= _endIndex)
                {
                    throw new InvalidOperationException();
                }
                return _data[_index];
            }
        }

        public string Next
        {
            get
            {
                if (_index == -1)
                {
                    throw new InvalidOperationException();
                }
                if (_index > _endIndex)
                {
                    throw new InvalidOperationException();
                }
                if (IsLast)
                {
                    return null;
                }
                return _data[_index + 1];
            }
        }

        public bool IsLast
        {
            get { return _index == _endIndex - 1; }
        }

        public void Reset()
        {
            _index = -1;
        }

        public bool MoveNext()
        {
            if (_index < _endIndex)
            {
                _index++;
                return _index < _endIndex;
            }
            return false;
        }

        public string GetRemainingFromNext()
        {
            throw new NotSupportedException();
        }

        public bool MovePrevious()
        {
            if (_index <= 0)
            {
                throw new InvalidOperationException();
            }
            if (_index <= _endIndex)
            {
                _index--;
                return _index <= _endIndex;
            }
            return false;
        }

        void IDisposable.Dispose()
        {
        }
    }

    internal class TargetWrapper
    {
        private readonly object _target;
        private readonly IList<string> _valueList;
        private readonly ValueListAttribute _vla;

        public TargetWrapper(object target)
        {
            _target = target;
            _vla = ValueListAttribute.GetAttribute(_target);
            if (IsValueListDefined)
                _valueList = ValueListAttribute.GetReference(_target);
        }

        public bool IsValueListDefined { get { return _vla != null; } }

        public bool AddValueItemIfAllowed(string item)
        {
            if (_vla.MaximumElements == 0 || _valueList.Count == _vla.MaximumElements)
                return false;

            lock (this)
            {
                _valueList.Add(item);
            }

            return true;
        }
    }
    #endregion

    #region Parser
    /// <summary>
    /// Defines a basic interface to parse command line arguments.
    /// </summary>
    public interface ICommandLineParser
    {
        /// <summary>
        /// Parses a <see cref="System.String"/> array of command line arguments, setting values in <paramref name="options"/>
        /// parameter instance's public fields decorated with appropriate attributes.
        /// </summary>
        /// <param name="args">A <see cref="System.String"/> array of command line arguments.</param>
        /// <param name="options">An object's instance used to receive values.
        /// Parsing rules are defined using <see cref="BaseOptionAttribute"/> derived types.</param>
        /// <returns>True if parsing process succeed.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="args"/> is null.</exception>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        bool ParseArguments(string[] args, object options);

        /// <summary>
        /// Parses a <see cref="System.String"/> array of command line arguments, setting values in <paramref name="options"/>
        /// parameter instance's public fields decorated with appropriate attributes.
        /// This overload allows you to specify a <see cref="System.IO.TextWriter"/> derived instance for write text messages.         
        /// </summary>
        /// <param name="args">A <see cref="System.String"/> array of command line arguments.</param>
        /// <param name="options">An object's instance used to receive values.
        /// Parsing rules are defined using <see cref="BaseOptionAttribute"/> derived types.</param>
        /// <param name="helpWriter">Any instance derived from <see cref="System.IO.TextWriter"/>,
        /// usually <see cref="System.Console.Error"/>. Setting this argument to null, will disable help screen.</param>
        /// <returns>True if parsing process succeed.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="args"/> is null.</exception>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        bool ParseArguments(string[] args, object options, TextWriter helpWriter);

        /// <summary>
        /// Parses a <see cref="System.String"/> array of command line arguments, setting values in <paramref name="options"/>
        /// parameter instance's public fields decorated with appropriate attributes.
        /// This overload allows you to specify a <see cref="System.IO.TextWriter"/> derived instance for write text messages,
        /// and also prefix for environment variables.
        /// </summary>
        /// <param name="args">A <see cref="System.String"/> array of command line arguments.</param>
        /// <param name="options">An object's instance used to receive values.
        /// Parsing rules are defined using <see cref="BaseOptionAttribute"/> derived types.</param>
        /// <param name="helpWriter">Any instance derived from <see cref="System.IO.TextWriter"/>,
        /// usually <see cref="System.Console.Error"/>. Setting this argument to null, will disable help screen.</param>
        /// <param name="envVarPrefix">prefix taht will be appended to option's LongName property while trying to read args 
        /// from environment variables</param>
        /// <returns>True if parsing process succeed.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="args"/> is null.</exception>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="envVarPrefix"/> is null.</exception>
        bool ParseArguments(string[] args, object options, TextWriter helpWriter, string envVarPrefix);
    }

    /// <summary>
    /// Provides the abstract base class for a strongly typed options target. Used when you need to get parsing errors.
    /// </summary>
    public abstract class CommandLineOptionsBase
    {
        public abstract IEnumerable<KeyValuePair<string, string>> GetLoadedOptionsPairs();

        protected CommandLineOptionsBase()
        {
            LastPostParsingState = new PostParsingState();
        }

        protected PostParsingState LastPostParsingState { get; private set; }

        internal PostParsingState InternalLastPostParsingState
        {
            get { return LastPostParsingState; }
        }
    }

    /// <summary>
    /// This exception is thrown when a generic parsing error occurs.
    /// </summary>
    [Serializable]
    public sealed class CommandLineParserException : Exception
    {
        internal CommandLineParserException()
        {
        }

        internal CommandLineParserException(string message)
            : base(message)
        {
        }

        internal CommandLineParserException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        internal CommandLineParserException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Specifies a set of features to configure a <see cref="CommandLineParser"/> behavior.
    /// </summary>
    public sealed class CommandLineParserSettings
    {
        private const bool CASE_SENSITIVE_DEFAULT = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineParserSettings"/> class.
        /// </summary>
        public CommandLineParserSettings()
            : this(CASE_SENSITIVE_DEFAULT)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineParserSettings"/> class,
        /// setting the case comparison behavior.
        /// </summary>
        /// <param name="caseSensitive">If set to true, parsing will be case sensitive.</param>
        public CommandLineParserSettings(bool caseSensitive)
        {
            CaseSensitive = caseSensitive;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineParserSettings"/> class,
        /// setting the <see cref="System.IO.TextWriter"/> used for help method output.
        /// </summary>
        /// <param name="helpWriter">Any instance derived from <see cref="System.IO.TextWriter"/>,
        /// default <see cref="System.Console.Error"/>. Setting this argument to null, will disable help screen.</param>
        public CommandLineParserSettings(TextWriter helpWriter)
            : this(CASE_SENSITIVE_DEFAULT)
        {
            HelpWriter = helpWriter;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineParserSettings"/> class,
        /// setting case comparison and help output options.
        /// </summary>
        /// <param name="caseSensitive">If set to true, parsing will be case sensitive.</param>
        /// <param name="helpWriter">Any instance derived from <see cref="System.IO.TextWriter"/>,
        /// default <see cref="System.Console.Error"/>. Setting this argument to null, will disable help screen.</param>
        public CommandLineParserSettings(bool caseSensitive, TextWriter helpWriter)
        {
            CaseSensitive = caseSensitive;
            HelpWriter = helpWriter;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineParserSettings"/> class,
        /// setting case comparison and mutually exclusive behaviors.
        /// </summary>
        /// <param name="caseSensitive">If set to true, parsing will be case sensitive.</param>
        /// <param name="mutuallyExclusive">If set to true, enable mutually exclusive behavior.</param>
        public CommandLineParserSettings(bool caseSensitive, bool mutuallyExclusive)
        {
            CaseSensitive = caseSensitive;
            MutuallyExclusive = mutuallyExclusive;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineParserSettings"/> class,
        /// setting case comparison, mutually exclusive behavior and help output option.
        /// </summary>
        /// <param name="caseSensitive">If set to true, parsing will be case sensitive.</param>
        /// <param name="mutuallyExclusive">If set to true, enable mutually exclusive behavior.</param>
        /// <param name="helpWriter">Any instance derived from <see cref="System.IO.TextWriter"/>,
        /// default <see cref="System.Console.Error"/>. Setting this argument to null, will disable help screen.</param>
        public CommandLineParserSettings(bool caseSensitive, bool mutuallyExclusive, TextWriter helpWriter)
        {
            CaseSensitive = caseSensitive;
            MutuallyExclusive = mutuallyExclusive;
            HelpWriter = helpWriter;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineParserSettings"/> class,
        /// setting case comparison, mutually exclusive behavior and help output option.
        /// </summary>
        /// <param name="caseSensitive">If set to true, parsing will be case sensitive.</param>
        /// <param name="mutuallyExclusive">If set to true, enable mutually exclusive behavior.</param>
        /// <param name="ignoreUnknownArguments">If set to true, allow the parser to skip unknown argument, otherwise return a parse failure</param>
        /// <param name="helpWriter">Any instance derived from <see cref="System.IO.TextWriter"/>,
        /// default <see cref="System.Console.Error"/>. Setting this argument to null, will disable help screen.</param>
        public CommandLineParserSettings(bool caseSensitive, bool mutuallyExclusive, bool ignoreUnknownArguments, TextWriter helpWriter)
        {
            CaseSensitive = caseSensitive;
            MutuallyExclusive = mutuallyExclusive;
            HelpWriter = helpWriter;
            IgnoreUnknownArguments = ignoreUnknownArguments;
        }

        /// <summary>
        /// Gets or sets the case comparison behavior.
        /// Default is set to true.
        /// </summary>
        public bool CaseSensitive { internal get; set; }

        /// <summary>
        /// Gets or sets the mutually exclusive behavior.
        /// Default is set to false.
        /// </summary>
        public bool MutuallyExclusive { internal get; set; }

        /// <summary>
        /// Gets or sets the <see cref="System.IO.TextWriter"/> used for help method output.
        /// Setting this property to null, will disable help screen.
        /// </summary>
        public TextWriter HelpWriter { internal get; set; }

        /// <summary>
        /// Gets or sets a value indicating if the parser shall move on to the next argument and ignore the given argument if it
        /// encounter an unknown arguments
        /// </summary>
        /// <value>
        /// <c>true</c> to allow parsing the arguments with differents class options that do not have all the arguments.
        /// </value>
        /// <remarks>
        /// This allows fragmented version class parsing, useful for project with addon where addons also requires command line arguments but
        /// when these are unknown by the main program at build time.
        /// </remarks>
        public bool IgnoreUnknownArguments
        {
            internal get;
            set;
        }

        public string EnvironmentVariablePrefix { internal get; set; }
    }

    /// <summary>
    /// Provides methods to parse command line arguments.
    /// Default implementation for <see cref="ICommandLineParser"/>.
    /// </summary>
    public class CommandLineParser : ICommandLineParser
    {
        private static readonly ICommandLineParser _default = new CommandLineParser(true);
        private readonly CommandLineParserSettings _settings;

        // special constructor for singleton instance, parameter ignored
        private CommandLineParser(bool singleton)
        {
            _settings = new CommandLineParserSettings(false, false, Console.Error);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineParser"/> class.
        /// </summary>
        public CommandLineParser()
        {
            _settings = new CommandLineParserSettings();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandLineParser"/> class,
        /// configurable with a <see cref="CommandLineParserSettings"/> object.
        /// </summary>
        /// <param name="settings">The <see cref="CommandLineParserSettings"/> object is used to configure
        /// aspects and behaviors of the parser.</param>
        public CommandLineParser(CommandLineParserSettings settings)
        {
            Assumes.NotNull(settings, "settings");

            _settings = settings;
        }

        /// <summary>
        /// Singleton instance created with basic defaults.
        /// </summary>
        public static ICommandLineParser Default
        {
            get { return _default; }
        }

        /// <summary>
        /// Parses a <see cref="System.String"/> array of command line arguments, setting values in <paramref name="options"/>
        /// parameter instance's public fields decorated with appropriate attributes.
        /// </summary>
        /// <param name="args">A <see cref="System.String"/> array of command line arguments.</param>
        /// <param name="options">An object's instance used to receive values.
        /// Parsing rules are defined using <see cref="BaseOptionAttribute"/> derived types.</param>
        /// <returns>True if parsing process succeed.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="args"/> is null.</exception>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        public virtual bool ParseArguments(string[] args, object options)
        {
            return ParseArguments(args, options, _settings.HelpWriter);
        }

        /// <summary>
        /// Parses a <see cref="System.String"/> array of command line arguments, setting values in <paramref name="options"/>
        /// parameter instance's public fields decorated with appropriate attributes.
        /// This overload allows you to specify a <see cref="System.IO.TextWriter"/> derived instance for write text messages.         
        /// </summary>
        /// <param name="args">A <see cref="System.String"/> array of command line arguments.</param>
        /// <param name="options">An object's instance used to receive values.
        /// Parsing rules are defined using <see cref="BaseOptionAttribute"/> derived types.</param>
        /// <param name="helpWriter">Any instance derived from <see cref="System.IO.TextWriter"/>,
        /// usually <see cref="System.Console.Error"/>. Setting this argument to null, will disable help screen.</param>
        /// <returns>True if parsing process succeed.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="args"/> is null.</exception>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        public virtual bool ParseArguments(string[] args, object options, TextWriter helpWriter)
        {
            Assumes.NotNull(args, "args");
            Assumes.NotNull(options, "options");

            var pair = ReflectionUtil.RetrieveMethod<HelpOptionAttribute>(options);

            if (pair != null && helpWriter != null)
            {
                if (ParseHelp(args, pair.Right) || !DoParseArguments(args, options))
                {
                    string helpText;
                    HelpOptionAttribute.InvokeMethod(options, pair, out helpText);
                    helpWriter.Write(helpText);
                    return false;
                }
                return true;
            }

            return DoParseArguments(args, options);
        }

        /// <summary>
        /// Parses a <see cref="System.String"/> array of command line arguments, setting values in <paramref name="options"/>
        /// parameter instance's public fields decorated with appropriate attributes.
        /// This overload allows you to specify a <see cref="System.IO.TextWriter"/> derived instance for write text messages,
        /// and also prefix for environment variables.
        /// </summary>
        /// <param name="args">A <see cref="System.String"/> array of command line arguments.</param>
        /// <param name="options">An object's instance used to receive values.
        /// Parsing rules are defined using <see cref="BaseOptionAttribute"/> derived types.</param>
        /// <param name="helpWriter">Any instance derived from <see cref="System.IO.TextWriter"/>,
        /// usually <see cref="System.Console.Error"/>. Setting this argument to null, will disable help screen.</param>
        /// <param name="envVarPrefix">prefix taht will be appended to option's LongName property while trying to read args 
        /// from environment variables</param>
        /// <returns>True if parsing process succeed.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="args"/> is null.</exception>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="envVarPrefix"/> is null.</exception>
        public virtual bool ParseArguments(string[] args, object options, TextWriter helpWriter, string envVarPrefix)
        {
            Assumes.NotNull(envVarPrefix, "envVarPrefix");
            _settings.EnvironmentVariablePrefix = envVarPrefix;
            return ParseArguments(args, options, helpWriter);
        }

        private bool DoParseArguments(string[] args, object options)
        {
            bool hadError = false;
            var optionMap = OptionInfo.CreateMap(options, _settings);
            optionMap.SetDefaults();
            var target = new TargetWrapper(options);

            IArgumentEnumerator arguments = new StringArrayEnumerator(args);
            while (arguments.MoveNext())
            {
                string argument = arguments.Current;
                if (!string.IsNullOrEmpty(argument))
                {
                    ArgumentParser parser = ArgumentParser.Create(argument, _settings.IgnoreUnknownArguments);
                    if (parser != null)
                    {
                        ParserState result = parser.Parse(arguments, optionMap, options);
                        if ((result & ParserState.Failure) == ParserState.Failure)
                        {
                            SetPostParsingStateIfNeeded(options, parser.PostParsingState);
                            hadError = true;
                            continue;
                        }

                        if ((result & ParserState.MoveOnNextElement) == ParserState.MoveOnNextElement)
                            arguments.MoveNext();
                    }
                    else
                    {
                        if (target.IsValueListDefined)
                        {
                            if (!target.AddValueItemIfAllowed(argument))
                            {
                                hadError = true;
                            }
                        }
                        else
                        {
                            hadError = true;
                            SetPostParsingStateIfNeeded(options, new[] { new ParsingError(null, argument, false, true) });
                        }
                    }
                }
            }

            // modified by MM: trying to read params' LongName from Environment Variable and App.config file
            hadError |= ReadMissingArgsIfAny(options, optionMap, _settings.EnvironmentVariablePrefix);

            hadError |= !optionMap.EnforceRules();

            return !hadError;
        }

        private static bool ReadMissingArgsIfAny(object options, OptionMap optionMap, string envVariablePrefix)
        {
            bool hadError = false;
            var keys = optionMap.GetMapKeys();
            foreach (var key in keys)
            {
                var option = optionMap[key];
                if (option.IsDefined || string.IsNullOrEmpty(option.LongName))
                    continue;

                bool noErrors = true;
                var environment = ParsingEnvironment.CommandLine;

                var envVarKey = string.Format("{0}{1}", envVariablePrefix, option.LongName);
                var envVarValue = Environment.GetEnvironmentVariable(envVarKey);
                if (!string.IsNullOrEmpty(envVarValue))
                {
                    noErrors = option.SetValue(envVarValue, options);
                    environment = ParsingEnvironment.EnvironmentVariable;
                    option.IsDefined = true;
                }
                else
                {
                    var configValue = ConfigurationManager.AppSettings[option.LongName];
                    if (!string.IsNullOrEmpty(configValue))
                    {
                        noErrors = option.SetValue(configValue, options);
                        environment = ParsingEnvironment.AppConfig;
                        option.IsDefined = true;
                    }
                }

                if (!noErrors)
                {
                    hadError = true;
                    SetPostParsingStateIfNeeded(options, new[]
                    {
                        new ParsingError(null, option.LongName, true) { ViolatingEnvironment = environment }
                    });
                }
            }
            return hadError;
        }

        private bool ParseHelp(string[] args, HelpOptionAttribute helpOption)
        {
            bool caseSensitive = _settings.CaseSensitive;

            for (int i = 0; i < args.Length; i++)
            {
                if (!string.IsNullOrEmpty(helpOption.ShortName))
                {
                    if (ArgumentParser.CompareShort(args[i], helpOption.ShortName, caseSensitive))
                        return true;
                }

                if (!string.IsNullOrEmpty(helpOption.LongName))
                {
                    if (ArgumentParser.CompareLong(args[i], helpOption.LongName, caseSensitive))
                        return true;
                }
            }

            return false;
        }

        //private static void SetPostParsingStateIfNeeded(object options, PostParsingState state)
        private static void SetPostParsingStateIfNeeded(object options, IEnumerable<ParsingError> state)
        {
            var commandLineOptionsBase = options as CommandLineOptionsBase;
            if (commandLineOptionsBase != null)
                (commandLineOptionsBase).InternalLastPostParsingState.Errors.AddRange(state);
        }
    }
    #endregion

    #region Utility
    internal static class Assumes
    {
        public static void NotNull<T>(T value, string paramName)
                where T : class
        {
            if (value == null)
                throw new ArgumentNullException(paramName);
        }

        public static void NotNullOrEmpty(string value, string paramName)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException(paramName);
        }

        public static void NotZeroLength<T>(T[] array, string paramName)
        {
            if (array.Length == 0)
                throw new ArgumentOutOfRangeException(paramName);
        }
    }

    internal static class ReflectionUtil
    {
        public static IList<Pair<PropertyInfo, TAttribute>> RetrievePropertyList<TAttribute>(object target)
                where TAttribute : Attribute
        {
            IList<Pair<PropertyInfo, TAttribute>> list = new List<Pair<PropertyInfo, TAttribute>>();
            if (target != null)
            {
                var propertiesInfo = target.GetType().GetProperties();

                foreach (var property in propertiesInfo)
                {
                    if (property != null && (property.CanRead && property.CanWrite))
                    {
                        var setMethod = property.GetSetMethod();
                        if (setMethod != null && !setMethod.IsStatic)
                        {
                            var attribute = Attribute.GetCustomAttribute(property, typeof(TAttribute), false);
                            if (attribute != null)
                                list.Add(new Pair<PropertyInfo, TAttribute>(property, (TAttribute)attribute));
                        }
                    }
                }
            }

            return list;
        }

        public static Pair<MethodInfo, TAttribute> RetrieveMethod<TAttribute>(object target)
                where TAttribute : Attribute
        {
            var info = target.GetType().GetMethods();

            foreach (MethodInfo method in info)
            {
                if (!method.IsStatic)
                {
                    Attribute attribute =
                        Attribute.GetCustomAttribute(method, typeof(TAttribute), false);
                    if (attribute != null)
                        return new Pair<MethodInfo, TAttribute>(method, (TAttribute)attribute);
                }
            }

            return null;
        }

        public static TAttribute RetrieveMethodAttributeOnly<TAttribute>(object target)
                where TAttribute : Attribute
        {
            var info = target.GetType().GetMethods();

            foreach (MethodInfo method in info)
            {
                if (!method.IsStatic)
                {
                    Attribute attribute =
                        Attribute.GetCustomAttribute(method, typeof(TAttribute), false);
                    if (attribute != null)
                        return (TAttribute)attribute;
                }
            }

            return null;
        }

        public static IList<TAttribute> RetrievePropertyAttributeList<TAttribute>(object target)
                where TAttribute : Attribute
        {
            IList<TAttribute> list = new List<TAttribute>();
            var info = target.GetType().GetProperties();

            foreach (var property in info)
            {
                if (property != null && (property.CanRead && property.CanWrite))
                {
                    var setMethod = property.GetSetMethod();
                    if (setMethod != null && !setMethod.IsStatic)
                    {
                        var attribute = Attribute.GetCustomAttribute(property, typeof(TAttribute), false);
                        if (attribute != null)
                            list.Add((TAttribute)attribute);
                    }
                }
            }

            return list;
        }

        public static TAttribute GetAttribute<TAttribute>()
            where TAttribute : Attribute
        {
            object[] a = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(TAttribute), false);
            if (a == null || a.Length <= 0) return null;
            return (TAttribute)a[0];
        }

        public static bool IsNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    }

    internal static class StringUtil
    {
        public static string Spaces(int count)
        {
            return new String(' ', count);
        }
    }
    #endregion
}