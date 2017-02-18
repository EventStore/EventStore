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
    /// API for executinmg queries in the Event Store through C# code. Communicates
    /// with the Event Store over the RESTful API.
    /// </summary>
    public class QueryManager
    {
        private readonly TimeSpan _queryTimeout;
        private readonly ProjectionsManager _projectionsManager;

        /// <summary>
        /// Creates a new instance of <see cref="QueryManager"/>.
        /// </summary>
        /// <param name="log">An instance of <see cref="ILogger"/> to use for logging.</param>
        /// <param name="httpEndPoint">HTTP endpoint of an Event Store server.</param>
        /// <param name="projectionOperationTimeout">Timeout of projection API operations</param>
        /// <param name="queryTimeout">Timeout of query execution</param>
        public QueryManager(ILogger log, IPEndPoint httpEndPoint, TimeSpan projectionOperationTimeout, TimeSpan queryTimeout)
        {
            _queryTimeout = queryTimeout;
            _projectionsManager = new ProjectionsManager(log, httpEndPoint, projectionOperationTimeout);
        }

        /// <summary>
        /// Asynchronously executes a query
        /// </summary>
        /// <param name="name">A name for the query.</param>
        /// <param name="query">The JavaScript source code for the query.</param>
        /// <param name="userCredentials">Credentials for a user with permission to create a query.</param>
        /// <returns>String of JSON containing query result.</returns>
        public async Task<string> ExecuteAsync(string name, string query, UserCredentials userCredentials = null)
        {
            return await Task.Run(async () =>
            {
                await _projectionsManager.CreateTransientAsync(name, query, userCredentials);
                await WaitForCompletedAsync(name, userCredentials);
                return await _projectionsManager.GetStateAsync(name, userCredentials);
            }).WithTimeout(_queryTimeout).ConfigureAwait(false);
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
