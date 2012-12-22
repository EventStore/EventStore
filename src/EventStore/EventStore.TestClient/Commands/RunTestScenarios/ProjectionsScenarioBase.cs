using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.Core.Services.Transport.Http.Codecs;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal abstract class ProjectionsScenarioBase : ScenarioBase
    {
        protected ProjectionsScenarioBase(Action<IPEndPoint, byte[]> directSendOverTcp, int maxConcurrentRequests, int connections, int streams, int eventsPerStream, int streamDeleteStep, string dbParentPath) : base(directSendOverTcp, maxConcurrentRequests, connections, streams, eventsPerStream, streamDeleteStep, dbParentPath)
        {
        }

        protected bool CheckProjectionState(string projectionName, string key, Func<string, bool> checkValue)
        {
            var state = GetProjectionState(projectionName);
            string value;
            return state != null && state.Count > 0 && state.TryGetValue(key, out value) && checkValue(value);
        }

        protected T GetProjectionStateValue<T>(string projectionName, string key, Func<string, T> convert, T defaultValue)
        {
            var result = defaultValue;

            var state = GetProjectionState(projectionName);
            string value;
            if (state != null && state.Count > 0 && state.TryGetValue(key, out value))
                result = convert(value);

            return result;
        }

        protected bool GetProjectionIsRunning(string projectionName)
        {
            var dic = GetProjectionStatistics(projectionName);

            string value;
            var isRunning = dic.TryGetValue("status", out value) && value == "Running";

            return isRunning;
        }

        protected long GetProjectionPosition(string projectionName)
        {
            var dic = GetProjectionStatistics(projectionName);

            long result = -1;

            string value;
            if (dic.TryGetValue("position", out value))
                result = long.Parse(value.Split(':')[1]);

            return result;
        }

        protected bool GetProjectionIsFaulted(string projectionName, out string reason)
        {
            var dic = GetProjectionStatistics(projectionName);

            string status;
            var isFaulted = dic.TryGetValue("status", out status) && status.StartsWith("Faulted");

            if (isFaulted)
                dic.TryGetValue("stateReason", out reason);
            else
                reason = null;

            return isFaulted;
        }

        private Dictionary<string, string> GetProjectionStatistics(string projectionName)
        {
            string rawState;
            try
            {
                rawState = GetProjectionsManager().GetStatistics(projectionName);
            }
            catch (Exception ex)
            {
                Log.InfoException(ex, "Failed to read projection statistics. Will continue.");
                rawState = null;
            }

            Log.Info("Raw {0} stats: {1}", projectionName, rawState);

            if (string.IsNullOrEmpty(rawState))
                return null;

            if (rawState == "*** UNKNOWN ***")
                return null;

            var start = rawState.IndexOf('[');
            var end = rawState.IndexOf(']');

            if (start == -1 || end == -1)
                return null;

            var statDic = rawState.Substring(start + 1, end - start - 1);

            var state = Codec.Json.From<Dictionary<string, string>>(statDic);
            return state;
        }

        private Dictionary<string, string> GetProjectionState(string projectionName)
        {
            var rawState = GetProjectionStateSafe(projectionName);

            Log.Info("Raw {0} state: {1}", projectionName, rawState);

            if (string.IsNullOrEmpty(rawState))
                return null;

            if (rawState == "*** UNKNOWN ***")
                return null;

            var state = Codec.Json.From<Dictionary<string, string>>(rawState);
            return state;
        }

        private string GetProjectionStateSafe(string projectionName)
        {
            string rawState;
            try
            {
                rawState = GetProjectionsManager().GetState(projectionName);
            }
            catch (Exception ex)
            {
                rawState = null;
                Log.InfoException(ex, "Failed to get projection state");
            }
            return rawState;
        }

        protected void EnableProjectionByCategory()
        {
            const string byCategoryProjection = "$by_category";

            var retryCount = 0;
            Exception exception = null;
            while (retryCount < 5)
            {
                Thread.Sleep(250 * (retryCount * retryCount));
                try
                {
                    var isRunning = GetProjectionIsRunning(byCategoryProjection);

                    if (!isRunning)
                    {
                        Log.Debug(string.Format("Enable *{0}* projection", byCategoryProjection));
                        GetProjectionsManager().Enable(byCategoryProjection);
                    }
                    else
                    {
                        Log.Debug(string.Format("Already enabled *{0}* projection", byCategoryProjection));
                    }

                    exception = null;
                    break;
                }
                catch (Exception ex)
                {
                    exception = new ApplicationException("Failed to enable by_category.", ex);
                    Log.ErrorException(ex, "Failed to enable *$by_category* projection, retry #{0}.", retryCount);
                }
                retryCount += 1;
            }

            if (exception != null)
                throw exception;
        }
    }
}