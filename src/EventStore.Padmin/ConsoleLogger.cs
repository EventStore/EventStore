using System;
using System.Text;
using System.Threading;
using EventStore.ClientAPI;

namespace EventStore.Padmin
{
    internal class ConsoleLogger : ILogger
    {
        public void Error(string format, params object[] args)
        {
            Console.WriteLine(Log("ERROR", format, args));
        }

        public void Error(Exception ex, string format, params object[] args)
        {
            Console.WriteLine(Log("ERROR", ex, format, args));
        }

        public void Debug(string format, params object[] args)
        {
            Console.WriteLine(Log("DEBUG", format, args));
        }

        public void Debug(Exception ex, string format, params object[] args)
        {
            Console.WriteLine(Log("DEBUG", ex, format, args));
        }

        public void Info(string format, params object[] args)
        {
            Console.WriteLine(Log("INFO", format, args));
        }

        public void Info(Exception ex, string format, params object[] args)
        {
            Console.WriteLine(Log("INFO", ex, format, args));
        }

        private string Log(string level, string format, params object[] args)
        {
            return string.Format("[{0:00},{1:HH:mm:ss.fff},{2}] {3}",
                Thread.CurrentThread.ManagedThreadId,
                DateTime.UtcNow,
                level,
                args.Length == 0 ? format : string.Format(format, args));
        }

        private string Log(string level, Exception exc, string format, params object[] args)
        {
            var sb = new StringBuilder();
            while (exc != null)
            {
                sb.AppendLine();
                sb.AppendLine(exc.ToString());
                exc = exc.InnerException;
            }

            return string.Format("[{0:00},{1:HH:mm:ss.fff},{2}] {3}\nEXCEPTION(S) OCCURRED:{4}",
                Thread.CurrentThread.ManagedThreadId,
                DateTime.UtcNow,
                level,
                args.Length == 0 ? format : string.Format(format, args),
                sb);
        }
    }
}