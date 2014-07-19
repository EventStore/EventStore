using System.Reflection;
using System;
using System.Linq;

namespace PowerArgs
{
    

    /// <summary>
    /// This is the more complex version of the public result that is produced by the parser.
    /// </summary>
    /// <typeparam name="T">Represents the custom argument scaffold type that was passed to the parser.</typeparam>
    public class ArgAction<T> : ArgAction
    {
        /// <summary>
        /// The instance of your custom scaffold type that the parser generated and parsed.
        /// </summary>
        public T Args
        {
            get { return (T)Value; }
            set { Value = value; }
        }
    }

    /// <summary>
    /// This is the weakly typed, more complex version of the public result that is produced by the parser.
    /// </summary>
    public class ArgAction
    {
        // TODO - Consider deprecating some of these fields since most of the data you want lives in the definition

        /// <summary>
        /// The definition or model that was used to parse the arguments
        /// </summary>
        public CommandLineArgumentsDefinition Definition { get; internal set; }

        /// <summary>
        /// The instance of your custom scaffold type that the parser generated and parsed.
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// If you used the action framework then this will represent the instance of the action specific arguments
        /// that were parsed.
        /// </summary>
        public object ActionArgs { get; set; }

        /// <summary>
        /// If you used the action framework then this will map to the property that the user specified as the first
        /// parameter on the command line.
        /// </summary>
        public PropertyInfo ActionArgsProperty { get; set; }

        /// <summary>
        /// This is set if you defined your action via a method with simple parameters
        /// </summary>
        public object[] ActionParameters { get; set; }


        /// <summary>
        /// If an exception was handled by the parser then this property will be populated and others will not be.
        /// </summary>
        public ArgException HandledException { get; internal set; }

        /// <summary>
        /// If processing was cancelled then this property will be set to true.
        /// </summary>
        public bool Cancelled { get; internal set; }


        internal MethodInfo ActionArgsMethod { get; set; }

        internal PowerArgs.ArgHook.HookContext Context { get; set; }

        /// <summary>
        /// This will find the implementation method for your action and invoke it, passing the action specific
        /// arguments as a parameter.
        /// </summary>
        public void Invoke()
        {
            if (ActionArgsMethod == null && ActionArgsProperty == null) throw new MissingArgException("No action was specified");
            var resolved = ActionArgsMethod ?? ResolveMethod(ActionArgsProperty.DeclaringType, ActionArgsProperty);

            object[] parameters;

            if (resolved.GetParameters().Length == 1 && resolved.GetParameters()[0].ParameterType == typeof(CommandLineArgumentsDefinition))
            {
                parameters = new object[] { Definition };
            }
            else if (ActionParameters == null)
            {
                parameters = resolved.GetParameters().Length == 0 ? new object[0] : new object[] { ActionArgs };
            }
            else
            {
                parameters = ActionParameters;
            }

            try
            {
                if (resolved.IsStatic)
                {
                    resolved.Invoke(null, parameters);
                }
                else
                {
                    resolved.Invoke(Value, parameters);
                }
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException is ArgException)
                {
                    (ex.InnerException as ArgException).Context = Context;
                    throw ex.InnerException;
                }
                else
                {
                    throw;
                }
            }
        }

        internal static PropertyInfo GetActionProperty(Type t)
        {
            var actionProperty = (from p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                  where p.Name == Constants.ActionPropertyConventionName &&
                                        p.Attr<ArgPosition>() != null && p.Attr<ArgPosition>().Position == 0 &&
                                        p.HasAttr<ArgRequired>()
                                  select p).SingleOrDefault();
            return actionProperty;
        }

        internal static MethodInfo ResolveMethod(Type t, PropertyInfo actionProperty)
        {
            string methodName = actionProperty.Name;
            int end = methodName.LastIndexOf(Constants.ActionArgConventionSuffix);
            if (end < 1) throw new InvalidArgDefinitionException("Could not resolve action method from property name: " + actionProperty.Name);
            methodName = methodName.Substring(0, end);

            var actionType = t.HasAttr<ArgActionType>() ? t.Attr<ArgActionType>().ActionType : t;
            var method = actionType.GetMethod(methodName);
            if (method == null) throw new InvalidArgDefinitionException("Could not find action method '" + methodName + "'");

            if (method.IsStatic == false && actionType != t) throw new InvalidArgDefinitionException("PowerArg action methods must be static if defined via the ArgActionType attribute");

            if (actionProperty.PropertyType != typeof(void))
            {
                if (method.GetParameters().Length != 1) throw new InvalidArgDefinitionException("PowerArg action methods must take one parameter that matches the property type for the attribute");
                if (method.GetParameters()[0].ParameterType != actionProperty.PropertyType) throw new InvalidArgDefinitionException(string.Format("Argument of type '{0}' does not match expected type '{1}'", method.GetParameters()[0].ParameterType.Name, actionProperty.PropertyType.Name));
            }

            return method;
        }
    }
}
