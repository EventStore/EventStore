using System;

namespace PowerArgs
{
    /// <summary>
    /// Use this attribute if you want users to be able to specify an argument without specifying the name, 
    /// but rather by it's position on the command line.  All positioned arguments must come before any named arguments.
    /// Zero '0' represents the first position.  If you are using the Action framework then subcommands must start at
    /// position 1.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ArgPosition : Attribute, ICommandLineArgumentMetadata
    {
        /// <summary>
        /// The expected position of this argument
        /// </summary>
        public int Position { get; private set; }

        /// <summary>
        /// Creates a new ArgPositionAttribute
        /// </summary>
        /// <param name="pos">The expected position of this argument</param>
        public ArgPosition(int pos)
        {
            if (pos < 0) throw new InvalidArgDefinitionException("Positioned args must be >= 0");
            this.Position = pos;
        }
    }
}
