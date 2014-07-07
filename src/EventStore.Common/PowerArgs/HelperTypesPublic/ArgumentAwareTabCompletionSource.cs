using System;
using System.Linq;

namespace PowerArgs
{
    // TODO P1 - Make public (will require adding some logic to TabCompletion to call a constructor
    // that accepts the definition if the completion source is of this type.
    internal abstract class ArgumentAwareTabCompletionSource : ITabCompletionSourceWithContext
    {
        CommandLineArgumentsDefinition definition;
        public ArgumentAwareTabCompletionSource(CommandLineArgumentsDefinition definition)
        {
            this.definition = definition;
        }

        public bool TryComplete(bool shift, string context, string soFar, out string completion)
        {
            if (context.StartsWith("-"))
            {
                context = context.Substring(1);
            }
            else if (context.StartsWith("/"))
            {
                context = context.Substring(1);
            }
            else
            {
                completion = null;
                return false;
            }

            var match = definition.Arguments.Where(arg => arg.IsMatch(context)).SingleOrDefault();
            if (match == null)
            {
                completion = null;
                return false;
            }

            return TryComplete(shift, match, soFar, out completion);
        }

        public abstract bool TryComplete(bool shift, CommandLineArgument context, string soFar, out string completion);

        public bool TryComplete(bool shift, string soFar, out string completion)
        {
            throw new NotImplementedException();
        }
    }
}
