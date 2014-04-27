using System;
using System.Globalization;
using System.Linq;

namespace PowerArgs
{
    internal class EnumTabCompletionSource : ArgumentAwareTabCompletionSource
    {
        CycledTabCompletionManager manager;
        public EnumTabCompletionSource(CommandLineArgumentsDefinition definition)
            : base(definition)
        {
            manager = new CycledTabCompletionManager();
        }

        public override bool TryComplete(bool shift, CommandLineArgument context, string soFar, out string completion)
        {
            if (context.ArgumentType.IsEnum == false)
            {
                completion = null;
                return false;
            }

            return manager.Cycle(shift, ref soFar, () =>
            {
                var options = Enum.GetNames(context.ArgumentType).Union(context.ArgumentType.GetEnumShortcuts());
                options = options.Where(o => o.StartsWith(soFar, context.IgnoreCase, CultureInfo.CurrentCulture)).ToArray();

                return options.ToList();
            }, out completion);
        }
    }
}
