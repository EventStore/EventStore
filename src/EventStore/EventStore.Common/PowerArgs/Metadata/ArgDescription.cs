using System;

namespace PowerArgs
{
    /// <summary>
    /// Use this attribute to describe your argument property.  This will show up in the auto generated
    /// usage documentation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Parameter)]
    public class ArgDescription : Attribute, IGlobalArgMetadata
    {
        /// <summary>
        /// A brief description of your argument property.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Creates a new ArgDescription attribute.
        /// </summary>
        /// <param name="description">A brief description of your argument property.</param>
        public ArgDescription(string description)
        {
            this.Description = description;
        }
    }
}
