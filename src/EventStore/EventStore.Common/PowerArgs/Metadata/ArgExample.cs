using System;

namespace PowerArgs
{
    /// <summary>
    /// Use this attribute to provide an example of how to use your program.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
    public class ArgExample : Attribute, IGlobalArgMetadata
    {
        /// <summary>
        /// The example command line.
        /// </summary>
        public string Example { get; private set; }

        /// <summary>
        /// A brief description of what this example does.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Creates a new ArgExample class
        /// </summary>
        /// <param name="example">The example command line.</param>
        /// <param name="description">A brief description of what this example does.</param>
        public ArgExample(string example, string description)
        {
            this.Example = example;
            this.Description = description;
        }
    } 
}
