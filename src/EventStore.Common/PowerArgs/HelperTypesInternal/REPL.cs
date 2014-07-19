using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PowerArgs
{
    internal static class REPL
    {
        internal static T DriveREPL<T>(TabCompletion t, Func<string[], T> eval, string[] args) where T : class
        {
            T ret = null;

            bool first = true;
            do
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    args = string.IsNullOrWhiteSpace(t.Indicator) ? new string[0] : new string[] { t.Indicator };
                }

                try
                {
                    ret = eval(args);
                }
                catch (REPLExitException)
                {
                    return ret;
                }
                catch (REPLContinueException)
                {

                }
            }
            while (t != null && t.REPL);

            return ret;
        }
    }
}
