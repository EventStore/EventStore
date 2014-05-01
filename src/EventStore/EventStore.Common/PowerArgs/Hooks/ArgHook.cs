using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PowerArgs
{
    /// <summary>
    /// An abstract class that you can implement if you want to hook into various parts of the
    /// parsing pipeline.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Parameter)]
    public abstract class ArgHook : Attribute, IGlobalArgMetadata
    {
        /// <summary>
        /// Context that is passed to your hook.  Different parts of the context will be available
        /// depending on which part of the pipeline you're hooking into.
        /// </summary>
        public class HookContext
        {
            [ThreadStatic]
            private static HookContext _current;

            /// <summary>
            /// Gets the context for the current parse operation happening on the current thread or
            /// null if no parse is happening on the current thread.
            /// </summary>
            public static HookContext Current
            {
                get
                {
                    return _current;
                }
                internal set
                {
                    _current = value;
                }
            }

            /// <summary>
            /// The current property being operating on.  This is not available during BeforePopulateProperties or
            /// AfterPopulateProperties.
            /// </summary>
            //[Obsolete("You should use CurrentArgument instead of Property since it offers more metadata.  It also exposes the PropertyInfo via CommandLineArgument.Source if the argument was created from a PropertyInfo.")]
            public PropertyInfo Property { get; set; }

            /// <summary>
            /// The current argument being operating on. 
            /// AfterPopulateProperties.
            /// </summary>
            public CommandLineArgument CurrentArgument { get; set; }

            /// <summary>
            /// Gets the action that was specified on the command line or null if no action was specified or if the definition exposes no actions.
            /// </summary>
            public CommandLineAction SpecifiedAction
            {
                get
                {
                    return Definition.SpecifiedAction;
                }
                internal set
                {
                    Definition.SpecifiedAction = value;
                }
            }

            /// <summary>
            /// The command line arguments that were passed to the Args class.  This is always available and you
            /// can modify it in the BeforeParse hook at your own risk.
            /// </summary>
            public string[] CmdLineArgs;

            /// <summary>
            /// The string value that was specified for the current argument.  This will align with the value of ArgHook.CurrentArgument.
            /// 
            /// This is not available during BeforePopulateProperties or
            /// AfterPopulateProperties.
            /// 
            /// </summary>
            public string ArgumentValue;

            /// <summary>
            /// This is the instance of your argument class.  The amount that it is populated will depend on how far along in the pipeline
            /// the parser is.
            /// </summary>
            public object Args { get; set; }

            /// <summary>
            /// The definition that's being used throughout the parsing process
            /// </summary>
            public CommandLineArgumentsDefinition Definition { get; set; }

            /// <summary>
            /// This is the value of the current property after it has been converted into its proper .NET type.  It is only available
            /// to the AfterPopulateProperty hook.
            /// </summary>
            public object RevivedProperty;

            /// <summary>
            /// The raw parser data.  This is not available to the BeforeParse hook.  It may be useful for you to look at, but you should rarely have to change the values.  
            /// Modify the content of this at your own risk.
            /// </summary>
            public ParseResult ParserData { get; set; }

            /// <summary>
            /// Get a value from the context's property bag.
            /// </summary>
            /// <typeparam name="T">The type of value you are expecting</typeparam>
            /// <param name="key">The key for the property you want to get</param>
            /// <returns>The value or default(T) if no value was found.</returns>
            public T GetProperty<T>(string key)
            {
                var val = this[key];
                if (val == null)
                {
                    if (typeof(T).IsClass) return default(T);
                    else throw new KeyNotFoundException("There is no property named '" + key + "'");
                }
                else return (T)val;
            }

            /// <summary>
            /// Set a value in the context's property bag.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="key">The key for the property you want to set</param>
            /// <param name="value">The value of the property to set</param>
            public void SetProperty<T>(string key, T value)
            {
                this[key] = value;
            }

            /// <summary>
            /// Returns true if the context has a value for the given property.
            /// </summary>
            /// <param name="key">The key to check</param>
            /// <returns>true if the context has a value for the given property, false otherwise</returns>
            public bool HasProperty(string key)
            {
                return _properties.ContainsKey(key);
            }

            /// <summary>
            /// Clear a value in the context's property bag.
            /// </summary>
            /// <param name="key">The key for the property you want to clear.</param>
            public void ClearProperty(string key)
            {
                this[key] = null;
            }

            /// <summary>
            /// Stops all argument processing, hooks, and action invocation as soon as is feasable.  You
            /// can implement an ArgHook that receives an event when this is called.
            /// </summary>
            public void CancelAllProcessing()
            {
                this.RunAfterCancel();
                throw new ArgCancelProcessingException();
            }

            private Dictionary<string, object> _properties = new Dictionary<string, object>();
            private object this[string key]
            {
                get
                {
                    object ret;
                    if (_properties.TryGetValue(key, out ret))
                    {
                        return ret;
                    }
                    return null;
                }
                set
                {
                    if (_properties.ContainsKey(key))
                    {
                        if (value != null)
                        {
                            _properties[key] = value;
                        }
                        else
                        {
                            _properties.Remove(key);
                        }
                    }
                    else
                    {
                        if (value != null)
                        {
                            _properties.Add(key, value);
                        }
                    }
                }
            }

            internal void RunHook(Func<ArgHook, int> orderby, Action<ArgHook> hookAction)
            {
                var seen = new List<PropertyInfo>();

                foreach (var hook in Definition.Hooks.OrderByDescending(orderby))
                {
                    hookAction(hook);
                }

                foreach (var argument in Definition.Arguments)
                {
                    CurrentArgument = argument;
                    Property = argument.Source as PropertyInfo;
                    if (Property != null) seen.Add(Property);

                    foreach (var hook in argument.Hooks.OrderByDescending(orderby))
                    {
                        hookAction(hook);
                    }

                    CurrentArgument = null;
                    Property = null;
                }

                if (Definition.ArgumentScaffoldType != null)
                {
                    foreach (var property in Definition.ArgumentScaffoldType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => seen.Contains(p) == false))
                    {
                        foreach (var hook in property.Attrs<ArgHook>().OrderByDescending(orderby))
                        {
                            Property = property;
                            hookAction(hook);
                        }
                    }
                }

                foreach (var action in Definition.Actions)
                {
                    foreach (var argument in action.Arguments)
                    {
                        CurrentArgument = argument;
                        Property = argument.Source as PropertyInfo;

                        foreach (var hook in argument.Hooks.OrderByDescending(orderby))
                        {
                            hookAction(hook);
                        }

                        CurrentArgument = null;
                        Property = null;
                    }
                }
            }

            internal void RunBeforeParse()
            {
                RunHook(h => h.BeforeParsePriority, (h) => { h.BeforeParse(this); });
            }

            internal void RunBeforePopulateProperties()
            {
                RunHook(h => h.BeforePopulatePropertiesPriority, (h) => { h.BeforePopulateProperties(this); });
            }

            internal void RunAfterPopulateProperties()
            {
                RunHook(h => h.AfterPopulatePropertiesPriority, (h) => { h.AfterPopulateProperties(this); });
            }

            internal void RunBeforeInvoke()
            {
                RunHook(h => h.BeforeInvokePriority, (h) => { h.BeforeInvoke(this); });
            }

            internal void RunAfterInvoke()
            {
                RunHook(h => h.AfterInvokePriority, (h) => { h.AfterInvoke(this); });
            }

            internal void RunAfterCancel()
            {
                RunHook(h => h.AfterCancelPriority, (h) => { h.AfterCancel(this); });
            }
        }

        /// <summary>
        /// The priority of the BeforeParse hook.  Higher numbers execute first.
        /// </summary>
        public int BeforeParsePriority { get; set; }

        /// <summary>
        /// The priority of the BeforePopulateProperties hook.  Higher numbers execute first.
        /// </summary>
        public int BeforePopulatePropertiesPriority { get; set; }

        /// <summary>
        /// The priority of the BeforePopulateProperty hook.  Higher numbers execute first.
        /// </summary>
        public int BeforePopulatePropertyPriority { get; set; }

        /// <summary>
        /// The priority of the AfterPopulateProperty hook.  Higher numbers execute first.
        /// </summary>
        public int AfterPopulatePropertyPriority { get; set; }

        /// <summary>
        /// The priority of the AfterPopulateProperties hook.  Higher numbers execute first.
        /// </summary>
        public int AfterPopulatePropertiesPriority { get; set; }

        /// <summary>
        /// The priority of the BeforeInvoke hook.  Higher numbers execute first.
        /// </summary>
        public int BeforeInvokePriority { get; set; }

        /// <summary>
        /// The priority of the AfterInvoke hook.  Higher numbers execute first.
        /// </summary>
        public int AfterInvokePriority { get; set; }

        /// <summary>
        /// The priority of the AfterCancel hook.  Higher numbers execute first.
        /// </summary>
        public int AfterCancelPriority { get; set; }

        /// <summary>
        /// This hook is called before the parser ever looks at the command line.  You can do some preprocessing of the 
        /// raw string arguments here.
        /// </summary>
        /// <param name="context">An object that has useful context.  See the documentation of each property for information about when those properties are populated.</param>
        public virtual void BeforeParse(HookContext context) { }

        /// <summary>
        /// This hook is called before the arguments defined in a class are populated.  For actions (or sub commands) this hook will
        /// get called once for the main class and once for the specified action.
        /// </summary>
        /// <param name="context">An object that has useful context.  See the documentation of each property for information about when those properties are populated.</param>
        public virtual void BeforePopulateProperties(HookContext context) { }

        /// <summary>
        /// This hook is called before an argument is transformed from a string into a proper type and validated.
        /// </summary>
        /// <param name="context">An object that has useful context.  See the documentation of each property for information about when those properties are populated.</param>
        public virtual void BeforePopulateProperty(HookContext context) { }

        /// <summary>
        /// This hook is called after an argument is transformed from a string into a proper type and validated.
        /// </summary>
        /// <param name="context">An object that has useful context.  See the documentation of each property for information about when those properties are populated.</param>
        public virtual void AfterPopulateProperty(HookContext context) { }


        /// <summary>
        /// This hook is called after the arguments defined in a class are populated.  For actions (or sub commands) this hook will
        /// get called once for the main class and once for the specified action.
        /// </summary>
        /// <param name="context">An object that has useful context.  See the documentation of each property for information about when those properties are populated.</param>
        public virtual void AfterPopulateProperties(HookContext context) { }

        /// <summary>
        /// This hook is called after parsing is complete, but before any Action or Main method is invoked.
        /// </summary>
        /// <param name="context">An object that has useful context.  See the documentation of each property for information about when those properties are populated.</param>
        public virtual void BeforeInvoke(HookContext context) { }

        /// <summary>
        /// This hook is called after any Action or Main method is invoked.
        /// </summary>
        /// <param name="context">An object that has useful context.  See the documentation of each property for information about when those properties are populated.</param>
        public virtual void AfterInvoke(HookContext context) { }

        /// <summary>
        /// This hook is called if CancelAllProcessing() is called on a HookContext object.
        /// </summary>
        /// <param name="context">An object that has useful context.  See the documentation of each property for information about when those properties are populated.</param>
        public virtual void AfterCancel(HookContext context) { }
    }
}
