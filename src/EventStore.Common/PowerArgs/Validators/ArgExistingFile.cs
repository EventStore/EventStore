using System;
using System.IO;

namespace PowerArgs
{
    /// <summary>
    /// Validates that if the user specifies a value for a property that the value represents a file that exists
    /// as determined by System.IO.File.Exists(file).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    public class ArgExistingFile : ArgValidator
    {
        // TODO P2 - Add a file extension filter

        /// <summary>
        /// Validates that the given file exists and cleans up the argument so that the application has access
        /// to the full path.
        /// </summary>
        /// <param name="name">the name of the property being populated.  This validator doesn't do anything with it.</param>
        /// <param name="arg">The value specified on the command line</param>
        public override void Validate(string name, ref string arg)
        {
            if (File.Exists(arg) == false)
            {
                throw new ValidationArgException("File not found - " + arg, new FileNotFoundException());
            }
            arg = Path.GetFullPath(arg);
        }
    }
}
