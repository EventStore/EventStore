// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using EventStore.Transport.Http.Codecs;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal abstract class ProjectionsScenarioBase : ScenarioBase
    {
        protected ProjectionsScenarioBase(Action<IPEndPoint, byte[]> directSendOverTcp, int maxConcurrentRequests, int connections, int streams, int eventsPerStream, int streamDeleteStep, string dbParentPath, NodeConnectionInfo customNode)
            : base(directSendOverTcp, maxConcurrentRequests, connections, streams, eventsPerStream, streamDeleteStep, dbParentPath, customNode)
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
            var isRunning = dic != null && dic.TryGetValue("status", out value) && value == "Running";

            return isRunning;
        }

        protected long GetProjectionPosition(string projectionName)
        {
            var dic = GetProjectionStatistics(projectionName);

            long result = -1;

            string value;
            if (dic != null && dic.TryGetValue("position", out value))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    var subpositions = value.Split(':');
                    var positionString = subpositions.Length == 2 ? subpositions[1] : value;
                    result = long.Parse(positionString);
                }
                else
                {
                    result = -1;
                }
                return result;
            }

            return result;
        }

        protected bool GetProjectionIsFaulted(string projectionName, out string reason)
        {
            var dic = GetProjectionStatistics(projectionName);

            string status;
            var isFaulted = dic != null && dic.TryGetValue("status", out status) && status.StartsWith("Faulted");

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
                rawState = GetProjectionsManager().GetStatistics(projectionName, AdminCredentials);
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
                rawState = GetProjectionsManager().GetState(projectionName, AdminCredentials);
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
                        GetProjectionsManager().Enable(byCategoryProjection, AdminCredentials);
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