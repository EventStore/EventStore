using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PowerArgs
{
    /// <summary>
    /// An enum to represent argument shortcut policies
    /// </summary>
    public enum ArgShortcutPolicy
    {
        /// <summary>
        /// No special behavior.
        /// </summary>
        Default,
        /// <summary>
        /// Pass this value to the ArgShortcut attribute's constructor to indicate that the given property
        /// does not support a shortcut.
        /// </summary>
        NoShortcut,
        /// <summary>
        /// This indicates that the .NET property named should not be used as an indicator.  Instead,
        /// only the values in the other ArgShortcut attributes should be used.
        /// </summary>
        ShortcutsOnly,
    }

    /// <summary>
    /// Use this attribute to override the shortcut that PowerArgs automatically assigns to each property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = true)]
    public class ArgShortcut : Attribute, IArgumentOrActionMetadata
    {
        /// <summary>
        /// The shortcut for the given property
        /// </summary>
        public string Shortcut { get; private set; }

        /// <summary>
        /// Get the ShortcutPolicy for this attribute.
        /// </summary>
        public ArgShortcutPolicy Policy { get; private set; }

        /// <summary>
        /// Creates a new ArgShortcut attribute with a specified value.
        /// </summary>
        /// <param name="shortcut">The value of the new shortcut.</param>
        public ArgShortcut(string shortcut)
        {
            this.Shortcut = shortcut;
            this.Policy = ArgShortcutPolicy.Default;
        }

        /// <summary>
        /// Creates a new ArgShortcut using the given policy
        /// </summary>
        /// <param name="policy"></param>
        public ArgShortcut(ArgShortcutPolicy policy)
        {
            this.Policy = policy;
        }
    }

    /// <summary>
    /// An attribute used to define long form aliases for argument
    /// names.  For example, --log-level instead of -log.
    /// It also supports an alternate syntax for providing the values.
    /// For example: --log-level=error instead of -log error or /log:error.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    [Obsolete("ArgShortcut has been refactored to support multipe shortcuts, including those that start with --.  Use [ArgShortcut(\"--my-shortcut\")]")]
    public class ArgLongForm : ArgShortcut
    {
        /// <summary>
        /// Creates a new instance of an ArgLongForm attribute given the shortcut value.
        /// </summary>
        /// <param name="value">The shortcut value</param>
        public ArgLongForm(string value) : base(Clean(value)) { }

        private static string Clean(string value)
        {
            if (value == null) return null;
            else if (value.StartsWith("--")) return value;
            else if (value.StartsWith("-")) throw new InvalidArgDefinitionException("Long form shortcuts cannot start with a single dash");
            else return "--" + value;
        }
    }
}
