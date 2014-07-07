using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace PowerArgs
{
    /// <summary>
    /// Instances of this class represent a single command line argument that users can specify on the command line.
    /// Supported syntaxes include:
    ///     -argumentName argumentValue
    ///     /argumentName:argumentValue
    ///     -argumentName                   - If the argument is a boolean it will be true in this case.
    ///     --argumentName=argumentValue    - Only works if you have added an alias that starts with --.
    ///     argumentValue                   - Only works if this argument defines the Position property as >= 0
    /// </summary>
    public class CommandLineArgument
    {
        private AttrOverride overrides;

        /// <summary>
        /// The values that can be used as specifiers for this argument on the command line
        /// </summary>
        public AliasCollection Aliases { get; private set; }

        /// <summary>
        /// Metadata that has been injected into this Argument
        /// </summary>
        public List<ICommandLineArgumentMetadata> Metadata { get; private set; }

        internal ReadOnlyCollection<ArgValidator> Validators
        {
            get
            {
                return Metadata.Metas<ArgValidator>().OrderByDescending(v => v.Priority).ToList().AsReadOnly();
            }
        }

        internal ReadOnlyCollection<ArgHook> Hooks
        {
            get
            {
                return Metadata.Metas<ArgHook>().AsReadOnly();
            }
        }

        internal ReadOnlyCollection<UsageHook> UsageHooks
        {
            get
            {
                return Metadata.Metas<UsageHook>().AsReadOnly();
            }
        }

        /// <summary>
        /// The CLR type of this argument.
        /// </summary>
        public Type ArgumentType { get; private set; }

        /// <summary>
        /// Specifies whether or not the parser should ignore case when trying to find a match for this argument on the command line.  Defaults to true.
        /// </summary>
        public bool IgnoreCase
        {
            get
            {
                return overrides.Get<ArgIgnoreCase, bool>(Metadata, p => p.IgnoreCase, true);
            }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            set
            {
                overrides.Set(value);
            }
        }

        /// <summary>
        /// If this is a positional argument then set this value >= 0 and users can specify a value without specifying an argument alias.  Defaults to -1.
        /// </summary>
        public int Position
        {
            get
            {
                return overrides.Get<ArgPosition, int>(Metadata, p => p.Position, -1);
            }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            set
            {
                overrides.Set(value);
            }
        }

        /// <summary>
        /// The default value for this argument in the event it is optional and the user did not specify it.
        /// </summary>
        public object DefaultValue
        {
            get
            {
                return overrides.Get<DefaultValueAttribute, object>(Hooks, d => d.Value);
            }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            set
            {
                overrides.Set(value);
            }
        }

        /// <summary>
        /// The description for this argument that appears in the auto generated usage.
        /// </summary>
        public string Description
        {
            get
            {
                return overrides.Get<ArgDescription, string>(Metadata, d => d.Description, string.Empty);
            }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            set
            {
                overrides.Set(value);
            }
        }

        /// <summary>
        /// Gets or sets whether or not this argument is required.
        /// </summary>
        public bool IsRequired
        {
            get
            {
                return overrides.Get<ArgRequired, bool>(Validators, v => true, false);
            }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            set
            {
                overrides.Set(value);
            }
        }

        /// <summary>
        /// If this argument was inferred from a type then the source is either a PropertyInfo or a ParameterInfo.  If this argument
        /// was created manually then this value will be null.
        /// </summary>
        public object Source { get; set; }
        
        /// <summary>
        /// This property will contain the parsed value of the command line argument if parsing completed successfully.
        /// </summary>
        public object RevivedValue { get; set; }

        /// <summary>
        /// The first alias of this argument or null if no aliases are defined.
        /// </summary>
        public string DefaultAlias
        {
            get
            {
                return Aliases.FirstOrDefault();
            }
        }

        internal CommandLineArgument()
        {
            overrides = new AttrOverride();
            Aliases = new AliasCollection(() => { return Metadata.Metas<ArgShortcut>(); }, () => { return IgnoreCase; });
            PropertyInitializer.InitializeFields(this, 1);
            ArgumentType = typeof(string);
            Position = -1;
        }
 
        /// <summary>
        /// Creates a command line argument of the given type and sets the first default alias.
        /// </summary>
        /// <param name="t">The CLR type of the argument</param>
        /// <param name="defaultAlias">The default name that users will use to specify this argument</param>
        /// <param name="ignoreCase">If true, the parser will match this argument even if the specifier doesn't match case.  True by default.</param>
        public CommandLineArgument(Type t, string defaultAlias, bool ignoreCase = true) : this()
        {
            if (t == null) throw new InvalidArgDefinitionException("Argument types cannot be null");

            ArgumentType = t;
            IgnoreCase = ignoreCase;
            Aliases.Add(defaultAlias);

            Metadata.AddRange(t.Attrs<IArgMetadata>().AssertAreAllInstanceOf<ICommandLineArgumentMetadata>());
        }

        /// <summary>
        /// Gets the string representation of this argument.
        /// </summary>
        /// <returns>the string representation of this argument.</returns>
        public override string ToString()
        {
            var ret = "";
            if (Aliases.Count > 0) ret += DefaultAlias + "<" + ArgumentType.Name + ">";

            ret += "(Aliases=" + Aliases.Count + ")";
            ret += "(Validators=" + Validators.Count() + ")";
            ret += "(Hooks=" + Hooks.Count() + ")";

            return ret;
        }

        internal static CommandLineArgument Create(PropertyInfo property, List<string> knownAliases)
        {
            var ret = PropertyInitializer.CreateInstance<CommandLineArgument>();
            ret.DefaultValue = property.HasAttr<DefaultValueAttribute>() ? property.Attr<DefaultValueAttribute>().Value : null;
            ret.Position = property.HasAttr<ArgPosition>() ? property.Attr<ArgPosition>().Position : -1;
            ret.Source = property;
            ret.ArgumentType = property.PropertyType;

            ret.IgnoreCase = true;

            if (property.DeclaringType.HasAttr<ArgIgnoreCase>() && property.DeclaringType.Attr<ArgIgnoreCase>().IgnoreCase == false)
            {
                ret.IgnoreCase = false;
            }

            if (property.HasAttr<ArgIgnoreCase>() && property.Attr<ArgIgnoreCase>().IgnoreCase == false)
            {
                ret.IgnoreCase = false;
            }


            ret.Aliases.AddRange(FindDefaultShortcuts(property, knownAliases, ret.IgnoreCase));

            // TODO - I think the first generic call can just be more specific
            ret.Metadata.AddRange(property.Attrs<IArgMetadata>().AssertAreAllInstanceOf<ICommandLineArgumentMetadata>());

            return ret;
        }

        internal static CommandLineArgument Create(ParameterInfo parameter)
        {
            var ret = PropertyInitializer.CreateInstance<CommandLineArgument>();
            ret.Position = parameter.Position;
            ret.ArgumentType = parameter.ParameterType;
            ret.Source = parameter;
            ret.DefaultValue = parameter.HasAttr<DefaultValueAttribute>() ? parameter.Attr<DefaultValueAttribute>().Value : null;
            
            ret.IgnoreCase = true;

            if (parameter.Member.DeclaringType.HasAttr<ArgIgnoreCase>() && parameter.Member.DeclaringType.Attr<ArgIgnoreCase>().IgnoreCase == false)
            {
                ret.IgnoreCase = false;
            }

            if (parameter.HasAttr<ArgIgnoreCase>() && parameter.Attr<ArgIgnoreCase>().IgnoreCase == false)
            {
                ret.IgnoreCase = false;
            }

            ret.Aliases.Add(parameter.Name);

            ret.Metadata.AddRange(parameter.Attrs<IArgMetadata>().AssertAreAllInstanceOf<ICommandLineArgumentMetadata>());

            return ret;
        }

        internal void RunArgumentHook(ArgHook.HookContext context, Func<ArgHook, int> orderby, Action<ArgHook> hookAction)
        {
            context.Property = Source as PropertyInfo;
            context.CurrentArgument = this;

            foreach (var hook in Hooks.OrderBy(orderby))
            {
                hookAction(hook);
            }

            context.Property = null;
            context.CurrentArgument = null;
        }


        internal void RunBeforePopulateProperty(ArgHook.HookContext context)
        {
            RunArgumentHook(context, h => h.BeforePopulatePropertyPriority, (h) => { h.BeforePopulateProperty(context); });
        }

        internal void RunAfterPopulateProperty(ArgHook.HookContext context)
        {
            RunArgumentHook(context, h => h.AfterPopulatePropertyPriority, (h) => { h.AfterPopulateProperty(context); });
        }

        internal void Validate(ref string commandLineValue)
        {
            if (ArgumentType == typeof(SecureStringArgument) && Validators.Count() > 0)
            {
                throw new InvalidArgDefinitionException("Properties of type SecureStringArgument cannot be validated.  If your goal is to make the argument required then the[ArgRequired] attribute is not needed.  The SecureStringArgument is designed to prompt the user for a value only if your code asks for it after parsing.  If your code never reads the SecureString property then the user is never prompted and it will be treated as an optional parameter.  Although discouraged, if you really, really need to run custom logic against the value before the rest of your program runs then you can implement a custom ArgHook, override RunAfterPopulateProperty, and add your custom attribute to the SecureStringArgument property.");
            }

            foreach (var v in Validators)
            {
                if (v.ImplementsValidateAlways)
                {
                    try { v.ValidateAlways(this, ref commandLineValue); }
                    catch (NotImplementedException)
                    {
                        // TODO P0 - Test to make sure the old, PropertyInfo based validators properly work.
                        v.ValidateAlways(Source as PropertyInfo, ref commandLineValue);
                    }
                }
                else if (commandLineValue != null)
                {
                    v.Validate(Aliases.First(), ref commandLineValue);
                }
            }
        }

        internal void Revive(string commandLineValue)
        {
            if (ArgRevivers.CanRevive(ArgumentType) && commandLineValue != null)
            {
                try
                {
                    if (ArgumentType.IsEnum)
                    {
                        RevivedValue = ArgRevivers.ReviveEnum(ArgumentType, commandLineValue, IgnoreCase);
                    }
                    else
                    {
                        RevivedValue = ArgRevivers.Revive(ArgumentType, Aliases.First(), commandLineValue);
                    }
                }
                catch (ArgException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (ex.InnerException != null && ex.InnerException is ArgException)
                    {
                        throw ex.InnerException;
                    }
                    else
                    {
                        if (ArgumentType.IsEnum) throw new ArgException("'" + commandLineValue + "' is not a valid value for " + Aliases.First() + ". Available values are [" + string.Join(", ", Enum.GetNames(ArgumentType)) + "]", ex);
                        else throw new ArgException(ex.Message, ex);
                    }
                }
            }
            else if (ArgRevivers.CanRevive(ArgumentType) && ArgumentType == typeof(SecureStringArgument))
            {
                RevivedValue = ArgRevivers.Revive(ArgumentType, Aliases.First(), commandLineValue);
            }
            else if (commandLineValue != null)
            {
                throw new ArgException("Unexpected argument '" + Aliases.First() + "' with value '" + commandLineValue + "'");
            }
        }

        internal bool IsMatch(string key)
        {
            var ret = Aliases.Where(a => a.Equals(key, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)).Count() > 0;
            return ret;
        }

        internal static bool IsArgument(PropertyInfo property)
        {
            if (property.HasAttr<ArgIgnoreAttribute>()) return false;
            if (CommandLineAction.IsActionImplementation(property)) return false;

            if (property.Name == Constants.ActionPropertyConventionName &&
                property.HasAttr<ArgPosition>() &&
                property.Attr<ArgPosition>().Position == 0 &&
                property.HasAttr<ArgRequired>())
            {
                return false;
            }

            return true;
        }

        internal static bool IsArgument(ParameterInfo parameter)
        {
            if (parameter.HasAttr<ArgIgnoreAttribute>()) return false;
            return true;
        }

        internal static void PopulateArguments(List<CommandLineArgument> arguments, ArgHook.HookContext context)
        {
            foreach (var argument in arguments)
            {
                argument.FindMatchingArgumentInRawParseData(context);
                argument.RunBeforePopulateProperty(context);
                argument.Validate(ref context.ArgumentValue);
                argument.Revive(context.ArgumentValue);
                argument.RunAfterPopulateProperty(context);
            }
        }

        internal static List<string> FindDefaultShortcuts(PropertyInfo info, List<string> knownShortcuts, bool ignoreCase)
        {
            List<string> ret = new List<string>();

            var argumentName = info.Name;

            bool excludeName = info.Attrs<ArgShortcut>().Where(s => s.Policy == ArgShortcutPolicy.ShortcutsOnly).Count() > 0;

            if (excludeName == false)
            {
                knownShortcuts.Add(info.Name);

                if (CommandLineAction.IsActionImplementation(info) && argumentName.EndsWith(Constants.ActionArgConventionSuffix))
                {
                    ret.Add(info.Name.Substring(0, argumentName.Length - Constants.ActionArgConventionSuffix.Length));
                }
                else
                {
                    ret.Add(argumentName);
                }
            }

            var attrs = info.Attrs<ArgShortcut>();

            if (attrs.Count == 0)
            {
                var longFormShortcut = PascalCaseNameSplitter(info.Name);
                if (!knownShortcuts.Any(x => x.Equals(longFormShortcut, StringComparison.OrdinalIgnoreCase)))
                {
                    knownShortcuts.Add(longFormShortcut);
                    ret.Add(longFormShortcut);
                }
            }
            ret.Reverse();
            return ret;
        }

        private static string PascalCaseNameSplitter(string name)
        {
            var regex = new System.Text.RegularExpressions.Regex(@"(?<=[A-Z])(?=[A-Z][a-z])|(?<=[^A-Z])(?=[A-Z])|(?<=[A-Za-z])(?=[^A-Za-z])");
            var convertedName = regex.Replace(name, "-");
            return convertedName.ToLower();
        }

        private void FindMatchingArgumentInRawParseData(ArgHook.HookContext context)
        {
            var match = from k in context.ParserData.ExplicitParameters.Keys where IsMatch(k) select k;

            if (match.Count() > 1)
            {
                throw new DuplicateArgException("Argument specified more than once: " + Aliases.First());
            }
            else if (match.Count() == 1)
            {
                var key = match.First();
                context.ArgumentValue = context.ParserData.ExplicitParameters[key];
                context.ParserData.ExplicitParameters.Remove(key);
            }
            else if (context.ParserData.ImplicitParameters.ContainsKey(Position))
            {
                var position = Position;
                context.ArgumentValue = context.ParserData.ImplicitParameters[position];
                context.ParserData.ImplicitParameters.Remove(position);
            }
            else
            {
                context.ArgumentValue = null;
            }
        }

        private static string GenerateShortcutAlias(string baseAlias, List<string> excluded, bool ignoreCase)
        {
            string shortcutVal = "";
            foreach (char c in baseAlias.Substring(0, baseAlias.Length - 1))
            {
                shortcutVal += c;
                if (excluded.Contains(shortcutVal, new CaseAwareStringComparer(ignoreCase)) == false)
                {
                    return shortcutVal;
                }
            }
            return null;
        }
    }
}
