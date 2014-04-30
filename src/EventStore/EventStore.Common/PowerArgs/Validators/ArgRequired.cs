using System;

namespace PowerArgs
{
    /// <summary>
    /// Validates that the user actually provided a value for the given property on the command line.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
    public class ArgRequired : ArgValidator
    {
        /// <summary>
        /// Determines whether or not the validator should run even if the user doesn't specify a value on the command line.
        /// This value is always true for this validator.
        /// </summary>
        public override bool ImplementsValidateAlways
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Creates a new ArgRequired attribute.
        /// </summary>
        public ArgRequired()
        {
            Priority = 100;
        }

        /// <summary>
        /// If you set this to true and the user didn't specify a value then the command line will prompt the user for the value.
        /// </summary>
        public bool PromptIfMissing { get; set; }

        /// <summary>
        /// Validates that the user actually specified a value and optionally prompts them when it is missing.
        /// </summary>
        /// <param name="argument">The argument being populated.  This validator doesn't do anything with it.</param>
        /// <param name="arg">The value specified on the command line or null if it wasn't specified</param>
        public override void ValidateAlways(CommandLineArgument argument, ref string arg)
        {
            if (arg == null && PromptIfMissing)
            {
                var value = "";
                while (string.IsNullOrWhiteSpace(value))
                {
                    Console.Write("Enter value for " + argument.DefaultAlias + ": ");
                    value = Console.ReadLine();
                }

                arg = value;
            }
            if (arg == null)
            {
                throw new MissingArgException("The argument '" + argument.DefaultAlias + "' is required", new ArgumentNullException(argument.DefaultAlias));
            }
        }
    }
}
