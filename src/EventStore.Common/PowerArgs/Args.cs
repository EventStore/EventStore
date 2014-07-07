using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;

namespace PowerArgs
{
    /// <summary>
    /// The main entry point for PowerArgs that includes the public parsing functions such as Parse, ParseAction, and InvokeAction.
    /// </summary>
    public class Args
    {
        [ThreadStatic]
        private static Dictionary<Type, object> _ambientArgs;

        private static Dictionary<Type, object> AmbientArgs
        {
            get
            {
                if (_ambientArgs == null) _ambientArgs = new Dictionary<Type, object>();
                return _ambientArgs;
            }
        }

        private Args() { }

        /// <summary>
        /// PowerArgs will manually search the assembly you provide for any custom type revivers.  If you don't specify an
        /// assembly then the assembly that calls this function will automatically be searched.
        /// </summary>
        /// <param name="a">The assembly to search or null if you want PowerArgs to search the assembly that's calling into this function.</param>
        public static void SearchAssemblyForRevivers(Assembly a = null)
        {
            a = a ?? Assembly.GetCallingAssembly();
            ArgRevivers.SearchAssemblyForRevivers(a);
        }

        /// <summary>
        /// Converts a single string that represents a command line to be executed into a string[], 
        /// accounting for quoted arguments that may or may not contain spaces.
        /// </summary>
        /// <param name="commandLine">The raw arguments as a single string</param>
        /// <returns>a converted string array with the arguments properly broken up</returns>
        public static string[] Convert(string commandLine)
        {
            List<string> ret = new List<string>();
            string currentArg = string.Empty;
            bool insideDoubleQuotes = false;

            for(int i = 0; i < commandLine.Length;i++)
            {
                var c = commandLine[i];

                if (insideDoubleQuotes && c == '"')
                {
                    ret.Add(currentArg);
                    currentArg = string.Empty;
                    insideDoubleQuotes = !insideDoubleQuotes;
                }
                else if (!insideDoubleQuotes && c == ' ')
                {
                    if (currentArg.Length > 0)
                    {
                        ret.Add(currentArg);
                        currentArg = string.Empty;
                    }
                }
                else if (c == '"')
                {
                    insideDoubleQuotes = !insideDoubleQuotes;
                }
                else if (c == '\\' && i < commandLine.Length - 1 && commandLine[i + 1] == '"')
                {
                    currentArg += '"';
                }
                else
                {
                    currentArg += c;
                }
            }

            if (currentArg.Length > 0)
            {
                ret.Add(currentArg);
            }

            return ret.ToArray();
        }

        /// <summary>
        /// Gets the last instance of this type of argument that was parsed on the current thread
        /// or null if PowerArgs did not parse an object of this type.
        /// </summary>
        /// <typeparam name="T">The scaffold type for your arguments</typeparam>
        /// <returns>the last instance of this type of argument that was parsed on the current thread</returns>
        public static T GetAmbientArgs<T>() where T : class
        {
            return (T)GetAmbientArgs(typeof(T));
        }

        /// <summary>
        /// Gets the last instance of this type of argument that was parsed on the current thread
        /// or null if PowerArgs did not parse an object of this type.
        /// </summary>
        /// <param name="t">The scaffold type for your arguments</param>
        /// <returns>the last instance of this type of argument that was parsed on the current thread</returns>
        public static object GetAmbientArgs(Type t)
        {
            object ret;
            if (AmbientArgs.TryGetValue(t, out ret))
            {
                return ret;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a new instance of T and populates it's properties based on the given arguments.
        /// If T correctly implements the heuristics for Actions (or sub commands) then the complex property
        /// that represents the options of a sub command are also populated.
        /// </summary>
        /// <typeparam name="T">The argument scaffold type.</typeparam>
        /// <param name="args">The command line arguments to parse</param>
        /// <returns>The raw result of the parse with metadata about the specified action.</returns>
        public static ArgAction<T> ParseAction<T>(params string[] args)
        {
            ArgAction<T> ret = Execute<ArgAction<T>>(() =>
            {
            Args instance = new Args();
            return instance.ParseInternal<T>(args);
            });
            return ret;
        }

        /// <summary>
        /// Creates a new instance of the given type and populates it's properties based on the given arguments.
        /// If the type correctly implements the heuristics for Actions (or sub commands) then the complex property
        /// that represents the options of a sub command are also populated.
        /// </summary>
        /// <param name="t">The argument scaffold type.</param>
        /// <param name="args">The command line arguments to parse</param>
        /// <returns>The raw result of the parse with metadata about the specified action.</returns>
        public static ArgAction ParseAction(Type t, params string[] args)
        {
            return ParseAction(new CommandLineArgumentsDefinition(t), args);
        }

        /// <summary>
        /// Parses the given arguments using a command line arguments definition.  
        /// </summary>
        /// <param name="definition">The definition that defines a set of command line arguments and/or actions.</param>
        /// <param name="args">The command line arguments to parse</param>
        /// <returns></returns>
        public static ArgAction ParseAction(CommandLineArgumentsDefinition definition, params string[] args)
        {
            ArgAction ret = Execute(() =>
            {
            Args instance = new Args();
            return instance.ParseInternal(definition, args);
            });

            return ret;
        }

        /// <summary>
        /// Parses the args for the given scaffold type and then calls the Main() method defined by the type.
        /// </summary>
        /// <param name="t">The argument scaffold type.</param>
        /// <param name="args">The command line arguments to parse</param>
        /// <returns>The raw result of the parse with metadata about the specified action.</returns>
        public static ArgAction InvokeMain(Type t, params string[] args)
        {
            ArgAction ret = Execute(() =>
            {
                return REPL.DriveREPL<ArgAction>(t.Attr<TabCompletion>(), (a) =>
                {
                var result = ParseAction(t, a);
                    if (result.HandledException == null)
                    {
                        result.Context.RunBeforeInvoke();
                        result.Value.InvokeMainMethod();
                        result.Context.RunAfterInvoke();
                    }
                return result;
            }
            , args);
            });

            return ret;
        }

        /// <summary>
        /// Parses the args for the given scaffold type and then calls the Main() method defined by the type.
        /// </summary>
        /// <typeparam name="T">The argument scaffold type.</typeparam>
        /// <param name="args">The command line arguments to parse</param>
        /// <returns>The raw result of the parse with metadata about the specified action.</returns>
        public static ArgAction<T> InvokeMain<T>(params string[] args)
        {
            ArgAction<T> ret = Execute(() =>
            {
                return REPL.DriveREPL<ArgAction<T>>(typeof(T).Attr<TabCompletion>(), (a) =>
                {
                var result = ParseAction<T>(a);
                    if (result.HandledException == null)
                    {
                        result.Context.RunBeforeInvoke();
                        result.Value.InvokeMainMethod();
                        result.Context.RunAfterInvoke();
                    }
                return result;
            }
            , args);
            });
            return ret;
        }

        /// <summary>
        /// Creates a new instance of T and populates it's properties based on the given arguments. T must correctly
        /// implement the heuristics for Actions (or sub commands) because this method will not only detect the action
        /// specified on the command line, but will also find and execute the method that implements the action.
        /// </summary>
        /// <typeparam name="T">The argument scaffold type that must properly implement at least one action.</typeparam>
        /// <param name="args">The command line arguments to parse</param>
        /// <returns>The raw result of the parse with metadata about the specified action.  The action is executed before returning.</returns>
        public static ArgAction<T> InvokeAction<T>(params string[] args)
        {
            ArgAction<T> ret = Execute<ArgAction<T>>(() =>
            {
                return REPL.DriveREPL<ArgAction<T>>(typeof(T).Attr<TabCompletion>(), (a) =>
                {
                var result = ParseAction<T>(a);
                    if (result.HandledException == null)
                    {
                        result.Context.RunBeforeInvoke();
                        result.Invoke();
                        result.Context.RunAfterInvoke();
                    }
                return result;
            }
            , args);
            });
            return ret;
        }

        /// <summary>
        /// Parses the given arguments using a command line arguments definition.  Then, invokes the action
        /// that was specified.  
        /// </summary>
        /// <param name="definition">The definition that defines a set of command line arguments and actions.</param>
        /// <param name="args"></param>
        /// <returns>The raw result of the parse with metadata about the specified action.  The action is executed before returning.</returns>
        public static ArgAction InvokeAction(CommandLineArgumentsDefinition definition, params string[] args)
        {
            ArgAction ret = Execute(() =>
            {
                return REPL.DriveREPL<ArgAction>(definition.Hooks.Where(h => h is TabCompletion).Select(h => h as TabCompletion).SingleOrDefault(), (a) =>
                {
                var result = ParseAction(definition, a);
                    if (result.HandledException == null)
                    {
                        result.Context.RunBeforeInvoke();
                        result.Invoke();
                        result.Context.RunAfterInvoke();
                    }
                return result;
            }
            , args);
            });
            return ret;
        }

        /// <summary>
        /// Creates a new instance of T and populates it's properties based on the given arguments.
        /// </summary>
        /// <typeparam name="T">The argument scaffold type.</typeparam>
        /// <param name="args">The command line arguments to parse</param>
        /// <returns>A new instance of T with all of the properties correctly populated</returns>
        public static T Parse<T>(params string[] args) where T : class
        {
            T ret = Execute(() =>
            {
            return ParseAction<T>(args).Args;
            });
            return ret;
        }

        /// <summary>
        /// Creates a new instance of the given type and populates it's properties based on the given arguments.
        /// </summary>
        /// <param name="t">The argument scaffold type</param>
        /// <param name="args">The command line arguments to parse</param>
        /// <returns>A new instance of the given type with all of the properties correctly populated</returns>
        public static object Parse(Type t, params string[] args)
        {
            object ret = Execute(() =>
            {
            return ParseAction(t, args).Value;
            });
            return ret;
        }

        /// <summary>
        /// Parses the given arguments using a command line arguments definition. The values will be populated within
        /// the definition.
        /// </summary>
        /// <param name="definition">The definition that defines a set of command line arguments and/or actions.</param>
        /// <param name="args">The command line arguments to parse</param>
        public static ArgAction Parse(CommandLineArgumentsDefinition definition, params string[] args)
        {
            ArgAction ret = Execute(() =>
            {
                return ParseAction(definition, args);
            });
            return ret;
        }

        [ThreadStatic]
        private static int executeRecursionCounter = 0;

        private static T Execute<T>(Func<T> argsProcessingCode) where T : class
        {
            executeRecursionCounter++;
            ArgHook.HookContext.Current = new ArgHook.HookContext();

            try
            {
                return argsProcessingCode();
            }
            catch (ArgCancelProcessingException)
            {
                if (executeRecursionCounter > 1)
                {
                    throw;
                }

                return CreateEmptyResult<T>(ArgHook.HookContext.Current, cancelled: true);
            }
            catch (ArgException ex)
            {
                ex.Context = ArgHook.HookContext.Current;
                if (executeRecursionCounter > 1)
                {
                    throw;
                }

                var context = ArgHook.HookContext.Current;
                var definition = context.Definition;
                if (definition.ExceptionBehavior.Policy == ArgExceptionPolicy.StandardExceptionHandling)
                {
                    return DoStandardExceptionHandling<T>(ex, context, definition);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                executeRecursionCounter--;
                if (executeRecursionCounter == 0)
                {
                    ArgHook.HookContext.Current = null;
                }
            }
        }

        private static T DoStandardExceptionHandling<T>(ArgException ex, ArgHook.HookContext context, CommandLineArgumentsDefinition definition) where T : class
        {
            Console.WriteLine(ex.Message);

            ArgUsage.GetStyledUsage(definition, definition.ExceptionBehavior.ExeName, new ArgUsageOptions
            {
                ShowPosition = definition.ExceptionBehavior.ShowPositionColumn,
                ShowType = definition.ExceptionBehavior.ShowTypeColumn,
                ShowPossibleValues = definition.ExceptionBehavior.ShowPossibleValues,
            }).Write();

            return CreateEmptyResult<T>(context, ex);
        }

        private static T CreateEmptyResult<T>(ArgHook.HookContext context, ArgException ex = null, bool cancelled = false)
        {
            ArgAction ret = new ArgAction();

            if (typeof(T) == typeof(ArgAction))
            {
                ret = new ArgAction();
            }
            else if (typeof(T).IsSubclassOf(typeof(ArgAction)))
            {
                ret = Activator.CreateInstance(typeof(T)) as ArgAction;
            }
            else
            {
                return default(T);
            }

            ret.HandledException = ex;
            ret.Definition = context.Definition;
            ret.Context = context;
            ret.Cancelled = cancelled;
            return (T)((object)ret);
        }

        private ArgAction<T> ParseInternal<T>(string[] input)
        {
            var weak = ParseInternal(new CommandLineArgumentsDefinition(typeof(T)), input);
            return new ArgAction<T>()
            {
                Args = (T)weak.Value,
                ActionArgs = weak.ActionArgs,
                ActionArgsProperty = weak.ActionArgsProperty,
                ActionParameters = weak.ActionParameters,
                ActionArgsMethod = weak.ActionArgsMethod,
                HandledException = weak.HandledException,
                Definition = weak.Definition,
                Context = weak.Context,
            };
        }

        private ArgAction ParseInternal(CommandLineArgumentsDefinition definition, string[] input)
        {
            // TODO - Validation should be consistently done against the definition, not against the raw type
            if (definition.ArgumentScaffoldType != null) ValidateArgScaffold(definition.ArgumentScaffoldType);
            definition.Validate();

            var context = ArgHook.HookContext.Current;
            context.Definition = definition;
            if (definition.ArgumentScaffoldType != null) context.Args = Activator.CreateInstance(definition.ArgumentScaffoldType);
            context.CmdLineArgs = input;

            context.RunBeforeParse();
            context.ParserData = ArgParser.Parse(context);

            context.RunBeforePopulateProperties();
            CommandLineArgument.PopulateArguments(context.Definition.Arguments, context);
            context.Definition.SetPropertyValues(context.Args);

            object actionArgs = null;
            object[] actionParameters = null;
            var specifiedAction = context.Definition.Actions.Where(a => a.IsMatch(context.CmdLineArgs.FirstOrDefault())).SingleOrDefault();
            if (specifiedAction == null && context.Definition.Actions.Count > 0)
            {
                if (context.CmdLineArgs.FirstOrDefault() == null)
                {
                    throw new MissingArgException("No action was specified");
                }
                else
                {
                    throw new UnknownActionArgException(string.Format("Unknown action: '{0}'", context.CmdLineArgs.FirstOrDefault()));
                }
            }
            else if (specifiedAction != null)
            {
                context.SpecifiedAction = specifiedAction;
                    
                PropertyInfo actionProp = null;
                if (context.Definition.ArgumentScaffoldType != null)
                {
                    actionProp = ArgAction.GetActionProperty(context.Definition.ArgumentScaffoldType);
                }

                if (actionProp != null)
                {
                    actionProp.SetValue(context.Args, specifiedAction.Aliases.First(), null);
                }

                context.ParserData.ImplicitParameters.Remove(0);
                CommandLineArgument.PopulateArguments(specifiedAction.Arguments, context);
                actionArgs = specifiedAction.PopulateArguments(context.Args, ref actionParameters);
            }

            context.RunAfterPopulateProperties();

            if (context.ParserData.ImplicitParameters.Count > 0)
            {
                throw new UnexpectedArgException("Unexpected unnamed argument: " + context.ParserData.ImplicitParameters.First().Value);
            }

            if (context.ParserData.ExplicitParameters.Count > 0)
            {
                throw new UnexpectedArgException("Unexpected named argument: " + context.ParserData.ExplicitParameters.First().Key);
            }

            if (definition.ArgumentScaffoldType != null)
            {
                if (AmbientArgs.ContainsKey(definition.ArgumentScaffoldType))
                {
                    AmbientArgs[definition.ArgumentScaffoldType] = context.Args;
                }
                else
                {
                    AmbientArgs.Add(definition.ArgumentScaffoldType, context.Args);
                }
            }

            PropertyInfo actionArgsPropertyInfo = null;

            if(specifiedAction != null)
            {
                if(specifiedAction.Source is PropertyInfo) actionArgsPropertyInfo = specifiedAction.Source as PropertyInfo;
                else if(specifiedAction.Source is MethodInfo) actionArgsPropertyInfo = new ArgActionMethodVirtualProperty(specifiedAction.Source as MethodInfo);
            }

            return new ArgAction()
            {
                Value = context.Args,
                ActionArgs = actionArgs,
                ActionParameters = actionParameters,
                ActionArgsProperty = actionArgsPropertyInfo,
                ActionArgsMethod = specifiedAction != null ? specifiedAction.ActionMethod : null,
                Definition = context.Definition,
                Context = context,
            };
        }

        private void ValidateArgScaffold(Type t, List<string> shortcuts = null, Type parentType = null)
        {
            /*
             * Today, this validates the following:
             * 
             *     - IgnoreCase can't be different on parent and child scaffolds.
             *     - No collisions on shortcut values for properties and enum values
             *     - No reviver for type
             * 
             */

            if (parentType != null)
            {
                if(parentType.HasAttr<ArgIgnoreCase>() ^ t.HasAttr<ArgIgnoreCase>())
                {
                    throw new InvalidArgDefinitionException("If you specify the " + typeof(ArgIgnoreCase).Name + " attribute on your base type then you must also specify it on each action type.");
                }
                else if (parentType.HasAttr<ArgIgnoreCase>() && parentType.Attr<ArgIgnoreCase>().IgnoreCase != t.Attr<ArgIgnoreCase>().IgnoreCase)
                {
                    throw new InvalidArgDefinitionException("If you specify the " + typeof(ArgIgnoreCase).Name + " attribute on your base and acton types then they must be configured to use the same value for IgnoreCase.");
                }
            }

            if (t.Attrs<ArgIgnoreCase>().Count > 1) throw new InvalidArgDefinitionException("An attribute that is or derives from " + typeof(ArgIgnoreCase).Name+" was specified on your type more than once");


            var actionProp = ArgAction.GetActionProperty(t);
            shortcuts = shortcuts ?? new List<string>();
            bool ignoreCase = true;
            if (t.HasAttr<ArgIgnoreCase>() && t.Attr<ArgIgnoreCase>().IgnoreCase == false) ignoreCase = false;

            foreach (PropertyInfo prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (prop.Attr<ArgIgnoreAttribute>() != null) continue;
                if (CommandLineAction.IsActionImplementation(prop)) continue;

                if (ArgRevivers.CanRevive(prop.PropertyType) == false)
                {
                    throw new InvalidArgDefinitionException("There is no reviver for type " + prop.PropertyType.Name + ". Offending Property: " + prop.DeclaringType.Name + "." + prop.Name);
                }

                if (prop.PropertyType.IsEnum)
                {
                    prop.PropertyType.ValidateNoDuplicateEnumShortcuts(ignoreCase);
                }

                var attrs = prop.Attrs<ArgShortcut>();
                var noShortcutsAllowed = attrs.Where(a => a.Policy == ArgShortcutPolicy.NoShortcut).Count() != 0;
                var shortcutsOnly = attrs.Where(a => a.Policy == ArgShortcutPolicy.ShortcutsOnly).Count() != 0;
                var actualShortcutValues = attrs.Where(a => a.Policy == ArgShortcutPolicy.Default && a.Shortcut != null).Count() != 0;

                if (noShortcutsAllowed && shortcutsOnly) throw new InvalidArgDefinitionException("You cannot specify a policy of NoShortcut and another policy of ShortcutsOnly.");
                if (noShortcutsAllowed && actualShortcutValues) throw new InvalidArgDefinitionException("You cannot specify a policy of NoShortcut and then also specify shortcut values via another attribute.");
                if (shortcutsOnly && actualShortcutValues == false) throw new InvalidArgDefinitionException("You specified a policy of ShortcutsOnly, but did not specify any shortcuts by adding another ArgShortcut attrivute.");
            }

            if (actionProp != null)
            {
                foreach (PropertyInfo prop in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (CommandLineAction.IsActionImplementation(prop))
                    {
                        ArgAction.ResolveMethod(t,prop);
                        ValidateArgScaffold(prop.PropertyType, shortcuts.ToArray().ToList(), t);
                    }
                }
            }

            foreach (var actionMethod in t.GetActionMethods())
            {
                if(actionMethod.GetParameters().Length == 0)continue;

                ValidateArgScaffold(actionMethod.GetParameters()[0].ParameterType, shortcuts.ToArray().ToList(), t);
            }
        }
    }
}
