using System;
using EventStore.Common.Log;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    public class ClientApiLogger : ClientAPI.ILogger
    {
        private static readonly ILogger Log = LogManager.GetLogger("ClientApiLogger");

        public void Error(string format, params object[] args)
        {
            Log.Error(format, args);
        }

        public void Error(Exception ex, string format, params object[] args)
        {
            Log.ErrorException(ex, format, args);
        }

        public void Info(Exception ex, string format, params object[] args)
        {
            Log.InfoException(ex, format, args);
        }

        public void Info(string format, params object[] args)
        {
            Log.Info(format, args);
        }

        public void Debug(string format, params object[] args)
        {
            Log.Debug(format, args);
        }

        public void Debug(Exception ex, string format, params object[] args)
        {
            Log.DebugException(ex, format, args);
        }
    }
}