using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace PowerArgs
{
    /// <summary>
    /// A class that represents command line actions that users can specify on the command line.  This is useful for programs like git
    /// where users first specify an action like 'push' and then the remaining arguments are either global or specific to 'push'.
    /// </summary>
    public class CommandLineAction
    {
        private readonly AttrOverride overrides;

        /// <summary>
        /// The values that the user can specify on the command line to specify this action.
        /// </summary>
        public AliasCollection Aliases { get; private set; }

        /// <summary>
        /// The action specific arguments that are applicable to the end user should they specify this action.
        /// </summary>
        public List<CommandLineArgument> Arguments { get; private set; }

        /// <summary>
        /// The description that will be shown in the auto generated usage.
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
        /// The method or property that was used to define this action.
        /// </summary>
        public object Source { get; set; }

        /// <summary>
        /// This will be set by the parser if the parse was successful and this was the action the user specified.
        /// </summary>
        public bool IsSpecifiedAction { get; internal set; }

        /// <summary>
        /// Indicates whether or not the parser should ignore case when matching a user string with this action.
        /// </summary>
        public bool IgnoreCase
        {
            get
            {
                return overrides.Get<ArgIgnoreCase, bool>(Metadata, i => i.IgnoreCase, true);
            }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            set
            {
                overrides.Set(value);
            }
        }

        /// <summary>
        /// The first alias or null if there are no aliases.
        /// </summary>
        public string DefaultAlias
        {
            get
            {
                return Aliases.FirstOrDefault();
            }
        }

        /// <summary>
        /// The list of metadata that can be used to inject behavior into the action
        /// </summary>
        public List<ICommandLineActionMetadata> Metadata { get; private set; }


        /// <summary>
        /// The implementation of the action that can be invoked by the parser if the user specifies this action.
        /// </summary>
        internal MethodInfo ActionMethod { get; private set; }

        /// <summary>
        /// Examples that show users how to use this action.
        /// </summary>
        internal ReadOnlyCollection<ArgExample> Examples
        {
            get
            {
                return Metadata.Metas<ArgExample>().AsReadOnly();
            }
        }

        /// <summary>
        /// Creates a new command line action given an implementation.
        /// </summary>
        /// <param name="actionHandler">The implementation of the aciton.</param>
        public CommandLineAction(Action<CommandLineArgumentsDefinition> actionHandler) : this()
        {
            ActionMethod = new ActionMethodInfo(actionHandler);
            Source = ActionMethod;
        }

        /// <summary>
        /// Gets a string representation of this action.
        /// </summary>
        /// <returns>a string representation of this action</returns>
        public override string ToString()
        {
            var ret = "";
            if (Aliases.Count > 0) ret += DefaultAlias;
            ret += "(Aliases=" + Aliases.Count + ")";
            ret += "(Arguments=" + Arguments.Count + ")";

            return ret;
        }

        protected bool Equals(CommandLineAction other)
        {
            return Equals(Source, other.Source);
        }

        public override int GetHashCode()
        {
            return (Source != null ? Source.GetHashCode() : 0);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CommandLineAction) obj);
        }

        internal CommandLineAction()
        {
            overrides = new AttrOverride();
            Aliases = new AliasCollection(() => { return Metadata.Metas<ArgShortcut>(); }, () => { return IgnoreCase; },stripLeadingArgInticatorsOnAttributeValues: false);
            PropertyInitializer.InitializeFields(this, 1);
            IgnoreCase = true;
        }

        internal static CommandLineAction Create(PropertyInfo actionProperty, List<string> knownAliases)
        {
            var ret = PropertyInitializer.CreateInstance<CommandLineAction>();
            ret.ActionMethod = ArgAction.ResolveMethod(actionProperty.DeclaringType, actionProperty);
            ret.Source = actionProperty;
            ret.Arguments.AddRange(new CommandLineArgumentsDefinition(actionProperty.PropertyType).Arguments);
            ret.IgnoreCase = true;

            if (actionProperty.DeclaringType.HasAttr<ArgIgnoreCase>() && actionProperty.DeclaringType.Attr<ArgIgnoreCase>().IgnoreCase == false)
            {
                ret.IgnoreCase = false;
            }

            if (actionProperty.HasAttr<ArgIgnoreCase>() && actionProperty.Attr<ArgIgnoreCase>().IgnoreCase == false)
            {
                ret.IgnoreCase = false;
            }

            ret.Metadata.AddRange(actionProperty.Attrs<IArgMetadata>().AssertAreAllInstanceOf<ICommandLineActionMetadata>());

            // This line only calls into CommandLineArgument because the code to strip 'Args' off the end of the
            // action property name lives here.  This is a pre 2.0 hack that's only left in place to support apps that
            // use the 'Args' suffix pattern.
            ret.Aliases.AddRange(CommandLineArgument.FindDefaultShortcuts(actionProperty, knownAliases, ret.IgnoreCase));

            return ret;
        }

        internal static CommandLineAction Create(MethodInfo actionMethod, List<string> knownAliases)
        {
            var ret = PropertyInitializer.CreateInstance<CommandLineAction>();
            ret.ActionMethod = actionMethod;

            ret.Source = actionMethod;
            ret.Aliases.Add(actionMethod.Name);

            ret.Metadata.AddRange(actionMethod.Attrs<IArgMetadata>().AssertAreAllInstanceOf<ICommandLineActionMetadata>());

            ret.IgnoreCase = true;

            if (actionMethod.DeclaringType.HasAttr<ArgIgnoreCase>() && actionMethod.DeclaringType.Attr<ArgIgnoreCase>().IgnoreCase == false)
            {
                ret.IgnoreCase = false;
            }

            if (actionMethod.HasAttr<ArgIgnoreCase>() && actionMethod.Attr<ArgIgnoreCase>().IgnoreCase == false)
            {
                ret.IgnoreCase = false;
            }

            if (actionMethod.GetParameters().Length == 1 && ArgRevivers.CanRevive(actionMethod.GetParameters()[0].ParameterType) == false)
            {
                ret.Arguments.AddRange(actionMethod.GetParameters()[0].ParameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => CommandLineArgument.IsArgument(p)).Select(p => CommandLineArgument.Create(p, knownAliases)));
            }
            else if (actionMethod.GetParameters().Length > 0 && actionMethod.GetParameters().Where(p => ArgRevivers.CanRevive(p.ParameterType) == false).Count() == 0)
            {
                ret.Arguments.AddRange(actionMethod.GetParameters().Where(p => CommandLineArgument.IsArgument(p)).Select(p => CommandLineArgument.Create(p)));
                foreach (var arg in (ret.Arguments).Where(a => a.Position >= 0))
                {
                    arg.Position++; // Since position 0 is reserved for the action specifier
                }
            }
            else if(actionMethod.GetParameters().Length > 0)
            {
                throw new InvalidArgDefinitionException("Your action method contains a parameter that cannot be revived on its own.  That is only valid if the non-revivable parameter is the only parameter.  In that case, the properties of that parameter type will be used.");
            }
            return ret;
        }

        internal object PopulateArguments(object parent, ref object[] parameters)
        {
            Type actionArgsType = null;

            if (Source is PropertyInfo)
            {
                actionArgsType = (Source as PropertyInfo).PropertyType;
            }
            else if (Source is MethodInfo && (Source as MethodInfo).GetParameters().Length > 0)
            {
                if ((Source as MethodInfo).GetParameters().Length > 1 || ArgRevivers.CanRevive((Source as MethodInfo).GetParameters()[0].ParameterType))
                {
                    parameters = Arguments.Select(a => a.RevivedValue).ToArray();
                    return null;
                }
                else
                {
                    actionArgsType = (Source as MethodInfo).GetParameters()[0].ParameterType;
                }
            }
            else
            {
                return null;
            }

            var ret = Activator.CreateInstance(actionArgsType);
            foreach (var argument in Arguments)
            {
                var argumentProperty = argument.Source as PropertyInfo;
                if (argumentProperty != null)
                {
                    argumentProperty.SetValue(ret, argument.RevivedValue, null);
                }
            }

            if (Source is PropertyInfo)
            {
                (Source as PropertyInfo).SetValue(parent, ret, null);
            }

            return ret;
        }

        internal bool IsMatch(string actionString)
        {
            var ret = Aliases.Where(a => a.Equals(actionString, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)).Count() > 0;
            return ret;
        }

        internal static bool IsActionImplementation(MethodInfo method)
        {
            return method.HasAttr<ArgActionMethod>();
        }

        internal static bool IsActionImplementation(PropertyInfo property)
        {
            return property.Name.EndsWith(Constants.ActionArgConventionSuffix) && 
                   property.HasAttr<ArgIgnoreAttribute>() == false &&
                ArgAction.GetActionProperty(property.DeclaringType) != null;
        }
    }
}
