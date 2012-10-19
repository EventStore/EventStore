using System;

namespace EventStore.ClientAPI.Common.Log
{
    internal class DefaultLogger : ILogger
    {
        public void Error(string format, params object[] args)
        {
            global::System.Diagnostics.Debug.WriteLine(format, args);
        }

        public void Error(Exception ex, string format, params object[] args)
        {
            global::System.Diagnostics.Debug.WriteLine("ERROR : {0}\n{1}", ex, string.Format(format, args));
        }

        public void Debug(string format, params object[] args)
        {
            global::System.Diagnostics.Debug.WriteLine(format, args);
        }

        public void Debug(Exception ex, string format, params object[] args)
        {
            global::System.Diagnostics.Debug.WriteLine("DEBUG : {0}\n{1}", ex, string.Format(format, args));
        }

        public void Info(Exception ex, string format, params object[] args)
        {
            global::System.Diagnostics.Debug.WriteLine("INFO : {0}\n{1}", ex, string.Format(format, args));
        }

        public void Info(string format, params object[] args)
        {
            global::System.Diagnostics.Debug.WriteLine(format, args);
        }
    }
}