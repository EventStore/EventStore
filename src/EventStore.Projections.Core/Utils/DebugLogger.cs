using System;
using System.Diagnostics;

namespace EventStore.Projections.Core.Utils
{
    public static class DebugLogger
    {
        [Conditional("PROJECTIONS_DEBUG")]
        public static void Log(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        [Conditional("PROJECTIONS_DEBUG")]
        public static void Log()
        {
            Console.WriteLine();
        }
    }
}
