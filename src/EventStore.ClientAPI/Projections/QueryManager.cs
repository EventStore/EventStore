using System;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Common.Utils.Threading;
using EventStore.ClientAPI.SystemData;
using Newtonsoft.Json.Linq;

namespace EventStore.ClientAPI.Projections {
	/// <summary>
	/// API for executing queries in the Event Store through C# code. Communicates
	/// with the Event Store over the RESTful API.
	/// </summary>
	public class QueryManager {
		private readonly TimeSpan _queryTimeout;
		private readonly ProjectionsManager _projectionsManager;

		/// <summary>
		/// Creates a new instance of <see cref="QueryManager"/>.
		/// </summary>
		/// <param name="log">An instance of <see cref="ILogger"/> to use for logging.</param>
		/// <param name="httpEndPoint">HTTP endpoint of an Event Store server.</param>
		/// <param name="projectionOperationTimeout">Timeout of projection API operations</param>
		/// <param name="queryTimeout">Timeout of query execution</param>
		public QueryManager(ILogger log, IPEndPoint httpEndPoint, TimeSpan projectionOperationTimeout,
			TimeSpan queryTimeout) {
			_queryTimeout = queryTimeout;
			_projectionsManager = new ProjectionsManager(log, httpEndPoint, projectionOperationTimeout);
		}

		/// <summary>
		/// Asynchronously executes a query.
		/// </summary>
		/// <remarks>
		/// Creates a new transient projection and polls its status until it is Completed.
		/// </remarks>
		/// <param name="name">A name for the query.</param>
		/// <param name="query">The JavaScript source code for the query.</param>
		/// <param name="initialPollingDelay">Initial time to wait between polling for projection status.</param>
		/// <param name="maximumPollingDelay">Maximum time to wait between polling for projection status.</param>
		/// <param name="userCredentials">Credentials for a user with permission to create a query.</param>
		/// <returns>String of JSON containing query result.</returns>
		public async Task<string> ExecuteAsync(string name, string query, TimeSpan initialPollingDelay,
			TimeSpan maximumPollingDelay, UserCredentials userCredentials = null) {
			return await Task.Run(async () => {
				await _projectionsManager.CreateTransientAsync(name, query, userCredentials).ConfigureAwait(false);
				await WaitForCompletedAsync(name, initialPollingDelay, maximumPollingDelay, userCredentials)
					.ConfigureAwait(false);
				return await _projectionsManager.GetStateAsync(name, userCredentials).ConfigureAwait(false);
			}).WithTimeout(_queryTimeout).ConfigureAwait(false);
		}

		private async Task WaitForCompletedAsync(string name, TimeSpan initialPollingDelay,
			TimeSpan maximumPollingDelay, UserCredentials userCredentials) {
			var attempts = 0;
			var status = await GetStatusAsync(name, userCredentials).ConfigureAwait(false);

			while (!status.Contains("Completed")) {
				attempts++;

				await DelayPollingAsync(attempts, initialPollingDelay, maximumPollingDelay).ConfigureAwait(false);
				status = await GetStatusAsync(name, userCredentials).ConfigureAwait(false);
			}
		}

		private static Task DelayPollingAsync(int attempts, TimeSpan initialPollingDelay,
			TimeSpan maximumPollingDelay) {
			var delayInMilliseconds = initialPollingDelay.TotalMilliseconds * (Math.Pow(2, attempts) - 1);
			delayInMilliseconds = Math.Min(delayInMilliseconds, maximumPollingDelay.TotalMilliseconds);

			return Task.Delay(TimeSpan.FromMilliseconds(delayInMilliseconds));
		}

		private async Task<string> GetStatusAsync(string name, UserCredentials userCredentials) {
			var projectionStatus =
				await _projectionsManager.GetStatusAsync(name, userCredentials).ConfigureAwait(false);
			return projectionStatus.ParseJson<JObject>()["status"].ToString();
		}
	}
}
