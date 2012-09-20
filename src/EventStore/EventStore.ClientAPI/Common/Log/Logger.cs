using System;

namespace EventStore.ClientAPI.Common.Log
{
    interface ILogger
    {
        void Error(string format, params object[] args);
        void Error(Exception ex, string format, params object[] args);

        void Info(Exception ex, string format, params object[] args);
        void Info(string format, params object[] args);

        void Debug(string format, params object[] args);
        void Debug(Exception ex, string format, params object[] args);
    }
}
