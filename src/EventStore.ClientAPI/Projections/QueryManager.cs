using System;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Common.Utils.Threading;
using EventStore.ClientAPI.SystemData;
using Newtonsoft.Json.Linq;

namespace EventStore.ClientAPI.Projections
{
    /// <summary>
    /// TODO
    /// </summary>
    public class QueryManager
    {
        private readonly TimeSpan _queryResultTimeout;
        private readonly ProjectionsManager _projectionsManager;

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="log"></param>
        /// <param name="httpEndPoint"></param>
        /// <param name="operationTimeout"></param>
        /// <param name="queryResultTimeout"></param>
        public QueryManager(ILogger log, IPEndPoint httpEndPoint, TimeSpan operationTimeout, TimeSpan queryResultTimeout)
        {
            _queryResultTimeout = queryResultTimeout;
            _projectionsManager = new ProjectionsManager(log, httpEndPoint, operationTimeout);
        }

        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="name"></param>
        /// <param name="userCredentials"></param>
        /// <returns></returns>
        public async Task<string> GetResultAsync(string name, UserCredentials userCredentials = null)
        {
            return await Task.Run(async () =>
            {
                await WaitForCompletedAsync(name, userCredentials);
                return await _projectionsManager.GetStateAsync(name, userCredentials);
            }).WithTimeout(_queryResultTimeout).ConfigureAwait(false);
        }

        private async Task WaitForCompletedAsync(string name, UserCredentials userCredentials)
        {
            var attempts = 0;
            var status = await GetStatusAsync(name, userCredentials);

            while (!status.Contains("Completed"))
            {
                attempts++;

                await DelayPollingAsync(attempts);
                status = await GetStatusAsync(name, userCredentials);
            }
        }

        private static async Task DelayPollingAsync(int attempts)
        {
            const int initialDelayInMilliseconds = 100;
            const int maximumDelayInMilliseconds = 5000;

            var delayInMilliseconds = initialDelayInMilliseconds * (Math.Pow(2, attempts) - 1);
            delayInMilliseconds = Math.Min(delayInMilliseconds, maximumDelayInMilliseconds);

            await Task.Delay(TimeSpan.FromMilliseconds(delayInMilliseconds));
        }

        private async Task<string> GetStatusAsync(string name, UserCredentials userCredentials)
        {
            var projectionStatus = await _projectionsManager.GetStatusAsync(name, userCredentials);
            return projectionStatus.ParseJson<JObject>()["status"].ToString();
        }
    }
}
