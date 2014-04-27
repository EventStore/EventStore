using System;
using System.Reflection;

namespace PowerArgs
{
    /// <summary>
    /// An abstract class that all validators should extend to validate user input from the command line.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    public abstract class ArgValidator : Attribute, ICommandLineArgumentMetadata
    {
        /// <summary>
        /// Determines the order in which validators are executed.  Higher numbers execute first.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// If implemented in a derived class then ValidateAlways will be called for each property,
        /// even if that property wasn't specified by the user on the command line.  In this case the value
        /// will always be null.  This is useful for implementing validators such as [ArgRequired].
        /// 
        /// By default, the Validate(string,ref string) method is called unless a validator opts into ValidateAlways
        /// </summary>
        public virtual bool ImplementsValidateAlways { get { return false; } }

        /// <summary>
        /// Most validators should just override this method. It ONLY gets called if the user specified the 
        /// given argument on the command line, meaning you will never get a null for 'arg'.
        /// 
        /// If you want your validator to run even if the user did not specify the argument on the command line
        /// (for example if you were building something like [ArgRequired] then you should do 3 things.
        /// 
        /// 1 - Override the boolean ImplementsValidateAlways property so that it returns true
        /// 2 - Override the ValidateAlways() method instead
        /// 3 - Don't override the Validate() method since it will no longer be called
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="arg">The value specified on the command line.  If the user specified the property name, but not a value then arg will equal string.Empty.  The value will never be null.</param>
        public virtual void Validate(string name, ref string arg) { }

        /// <summary>
        /// Always validates the given property, even if it was not specified by the user (arg will be null in this case).
        /// If you override this method then you should also override ImplementsValidateAlways so it returns true.
        ///</summary>
        /// <param name="property">The property that the attribute was placed on.</param>
        /// <param name="arg">The value specified on the command line or null if the user didn't actually specify a value for the property.  If the user specified the property name, but not a value then arg will equal string.Empty</param>
        [Obsolete("Validators should implement the overload of ValidateAlways that accepts a CommandLineArgument as the first parameter.  That overload provides a superset of data compared to this method.")]
        public virtual void ValidateAlways(PropertyInfo property, ref string arg) { throw new NotImplementedException(); }


        /// <summary>
        /// Always validates the given argument, even if it was not specified by the user (arg will be null in this case).
        /// If you override this method then you should also override ImplementsValidateAlways so it returns true.
        ///</summary>
        /// <param name="argument">The argument that the attribute was placed on.</param>
        /// <param name="arg">The value specified on the command line or null if the user didn't actually specify a value for the argument.  If the user specified the argument name, but not a value then arg will equal string.Empty</param>
        public virtual void ValidateAlways(CommandLineArgument argument, ref string arg) { throw new NotImplementedException(); }
    }
}
