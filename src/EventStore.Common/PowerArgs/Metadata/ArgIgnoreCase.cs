using System;

namespace PowerArgs
{
    /// <summary>
    /// Use this argument on your class, property, or action method to enforce case sensitivity.  By default,
    /// case is ignored.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Parameter)]
    public class ArgIgnoreCase : Attribute, IGlobalArgMetadata
    {
        /// <summary>
        /// Flag to set whether or not case is ignored.
        /// </summary>
        public bool IgnoreCase { get; set; }

        /// <summary>
        /// Create a new ArgIgnoreCase attribute.
        /// </summary>
        /// <param name="ignore">Whether or not to ignore case</param>
        public ArgIgnoreCase(bool ignore = true)
        {
            IgnoreCase = ignore;
        }
    }

    /// <summary>
    /// Use this argument on your class or property to enforce case sensitivity.  By default,
    /// case is ignored.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    public class ArgEnforceCase : ArgIgnoreCase
    {
        /// <summary>
        /// Create a new ArgEnforceCase attribute.
        /// </summary>
        public ArgEnforceCase() : base(false) { }
    }
}
