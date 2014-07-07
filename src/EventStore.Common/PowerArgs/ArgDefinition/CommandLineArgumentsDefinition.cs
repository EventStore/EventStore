using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace PowerArgs
{
    /// <summary>
    /// This is the root class used to define a program's command line arguments.  You can start with an empty definition and 
    /// programatically add arguments or you can start from a Type that you have defined and have the definition inferred from it.
    /// </summary>
    public class CommandLineArgumentsDefinition
    {
        /// <summary>
        /// The type that was used to generate this definition.  This will only be populated if you use the constructor that takes in a type and the definition is inferred.
        /// </summary>
        public Type ArgumentScaffoldType { get; private set; }

        /// <summary>
        /// The command line arguments that are global to this definition.
        /// </summary>
        public List<CommandLineArgument> Arguments { get; private set; }

        /// <summary>
        /// Global hooks that can execute all hook override methods except those that target a particular argument.
        /// </summary>
        public ReadOnlyCollection<ArgHook> Hooks
        {
            get
            {
                return Metadata.Metas<ArgHook>().AsReadOnly();
            }
        }
        
        /// <summary>
        /// Actions that are defined for this definition.  If you have at least one action then the end user must specify the action as the first argument to your program.
        /// </summary>
        public List<CommandLineAction> Actions { get; private set; }

        /// <summary>
        /// Arbitrary metadata that has been added to the definition
        /// </summary>
        public List<ICommandLineArgumentsDefinitionMetadata> Metadata { get; private set; }

        /// <summary>
        /// Examples that show users how to use your program.
        /// </summary>
        public ReadOnlyCollection<ArgExample> Examples
        {
            get
            {
                return Metadata.Metas<ArgExample>().AsReadOnly();
            }
        }

        /// <summary>
        /// Determines how end user errors should be handled by the parser.  By default all exceptions flow through to your program.
        /// </summary>
        public ArgExceptionBehavior ExceptionBehavior { get; set; }

        /// <summary>
        /// If your definition declares actions and has been successfully parsed then this property will be populated
        /// with the action that the end user specified.
        /// </summary>
        public CommandLineAction SpecifiedAction
        {
            get
            {
                var ret = Actions.Where(a => a.IsSpecifiedAction).SingleOrDefault();
                return ret;
            }
            internal set
            {
                foreach (var action in Actions)
                {
                    action.IsSpecifiedAction = false;
                }
                value.IsSpecifiedAction = true;
            }
        }

        /// <summary>
        /// Creates an empty command line arguments definition.
        /// </summary>
        public CommandLineArgumentsDefinition()
        {
            PropertyInitializer.InitializeFields(this, 1);
            ExceptionBehavior = new ArgExceptionBehavior();
        }

        /// <summary>
        /// Creates a command line arguments definition and infers things like Arguments, Actions, etc. from the type's metadata.
        /// </summary>
        /// <param name="t">The argument scaffold type used to infer the definition</param>
        public CommandLineArgumentsDefinition (Type t) : this()
        {
            ArgumentScaffoldType = t;
            ExceptionBehavior = t.HasAttr<ArgExceptionBehavior>() ? t.Attr<ArgExceptionBehavior>() : new ArgExceptionBehavior();
            Arguments.AddRange(FindCommandLineArguments(t));
            Actions.AddRange(FindCommandLineActions(t));
            Metadata.AddRange(t.Attrs<IArgMetadata>().AssertAreAllInstanceOf<ICommandLineArgumentsDefinitionMetadata>());
        }

        /// <summary>
        /// Finds the first CommandLineArgument that matches the given key.
        /// </summary>
        /// <param name="key">The key as if it was typed in on the command line.  This can also be an alias. </param>
        /// <param name="throwIfMoreThanOneMatch">If set to true then this method will throw and InvalidArgDeginitionException if more than 1 match is found</param>
        /// <returns>The first argument that matches the key.</returns>
        public CommandLineArgument FindMatchingArgument(string key, bool throwIfMoreThanOneMatch = false)
        {
            var match = from a in Arguments where a.IsMatch(key) select a;
            if (match.Count() > 1 && throwIfMoreThanOneMatch)
            {
                throw new InvalidArgDefinitionException("The key '" + key + "' matches more than one argument");
            }

            return match.FirstOrDefault();
        }

        /// <summary>
        /// Finds the first CommandLineAction that matches the given key
        /// </summary>
        /// <param name="key">The key as if it was typed in on the command line.  This can also be an alias. </param>
        /// <param name="throwIfMoreThanOneMatch">If set to true then this method will throw and InvalidArgDeginitionException if more than 1 match is found</param>
        /// <returns>The first action that matches the key.</returns>
        public CommandLineAction FindMatchingAction(string key, bool throwIfMoreThanOneMatch = false)
        {
            var match = from a in Actions where a.IsMatch(key) select a;
            if (match.Count() > 1 && throwIfMoreThanOneMatch)
            {
                throw new InvalidArgDefinitionException("The key '" + key + "' matches more than one action");
            }

            return match.FirstOrDefault();
        }

        /// <summary>
        /// Gets a basic string representation of the definition.
        /// </summary>
        /// <returns>a basic string representation of the definition</returns>
        public override string ToString()
        {
            var ret = "";

            if (ArgumentScaffoldType != null) ret += ArgumentScaffoldType.Name;
            ret += "(Arguments=" + Arguments.Count + ")";
            ret += "(Actions=" + Actions.Count + ")";
            ret += "(Hooks=" + Hooks.Count() + ")";

            return ret;
        }

        internal void SetPropertyValues(object o)
        {
            foreach (var argument in Arguments)
            {
                var property = argument.Source as PropertyInfo;
                if (property == null) return;
                if (argument.RevivedValue == null) continue;
                property.SetValue(o, argument.RevivedValue, null);
            }
        }

        internal void Validate()
        {
            ValidateArguments(Arguments);

            foreach (var action in Actions)
            {
                if (action.Aliases.Count == 0) throw new InvalidArgDefinitionException("One of your actions has no aliases");
                ValidateArguments(Arguments.Union(action.Arguments));
                if (action.ActionMethod == null) throw new InvalidArgDefinitionException("The action '"+action.DefaultAlias+"' has no ActionMethod defined");
            }
        }

        private List<CommandLineAction> FindCommandLineActions(Type t)
        {
            var knownAliases = new List<string>();
            foreach (var argument in Arguments) knownAliases.AddRange(argument.Aliases);

            BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public;

            var actions = (from p in t.GetProperties(flags)
                           where  CommandLineAction.IsActionImplementation(p)
                           select CommandLineAction.Create(p, knownAliases)).ToList();

            if (t.HasAttr<ArgActionType>())
            {
                t = t.Attr<ArgActionType>().ActionType;
            }

            foreach (var action in t.GetMethods(flags).Where(m => CommandLineAction.IsActionImplementation(m)).Select(m => CommandLineAction.Create(m, knownAliases.ToList())))
            {
                var matchingPropertyBasedAction = actions.Where(a => a.Aliases.First() == action.Aliases.First()).SingleOrDefault();
                if (matchingPropertyBasedAction != null) continue;
                actions.Add(action);
            }

            return actions;
        }

        private static List<CommandLineArgument> FindCommandLineArguments(Type t)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

            var knownAliases = new List<string>();

            foreach (var prop in t.GetProperties(flags))
            {
                // This makes sure that explicit aliases get put into the known aliases before any auto generated aliases
                knownAliases.AddRange(prop.Attrs<ArgShortcut>().SelectMany(s => s.Shortcut.Split('|')));
            }

            var ret = from p in t.GetProperties(flags) 
                      where  CommandLineArgument.IsArgument(p) 
                      select CommandLineArgument.Create(p, knownAliases);
            return ret.ToList();
        }

        private static void ValidateArguments(IEnumerable<CommandLineArgument> arguments)
        {
            List<string> knownAliases = new List<string>();

            foreach (var argument in arguments)
            {
                foreach (var alias in argument.Aliases)
                {
                    if (knownAliases.Contains(alias, new CaseAwareStringComparer(argument.IgnoreCase))) throw new InvalidArgDefinitionException("Duplicate alias '" + alias + "' on argument '" + argument.Aliases.First() + "'");
                    knownAliases.Add(alias);
                }
            }

            foreach (var argument in arguments)
            {
                if (argument.ArgumentType == null)
                {
                    throw new InvalidArgDefinitionException("Argument '" + argument.DefaultAlias + "' has a null ArgumentType");
                }

                if (ArgRevivers.CanRevive(argument.ArgumentType) == false)
                {
                    throw new InvalidArgDefinitionException("There is no reviver for type '" + argument.ArgumentType.Name + '"');
                }

                if (argument.ArgumentType.IsEnum)
                {
                    argument.ArgumentType.ValidateNoDuplicateEnumShortcuts(argument.IgnoreCase);
                }

                try
                {
                    foreach (var property in argument.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        // Getting each property will result in all AttrOverrides being validated
                        property.GetValue(argument, null);
                    }
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException is InvalidArgDefinitionException)
                    {
                        throw ex.InnerException;
                    }
                }
            }
        }
    }
}
