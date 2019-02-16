using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;

namespace EventStore.ClientAPI.PersistentSubscriptions {
	/// <summary>
	/// API for managing persistent subscriptions in Event Store through C# code. Communicates
	/// with Event Store over the RESTful API.
	/// </summary>
	public class PersistentSubscriptionsManager {
		private readonly PersistentSubscriptionsClient _client;
		private readonly EndPoint _httpEndPoint;
		private readonly string _httpSchema;

		/// <summary>
		/// Creates a new instance of <see cref="PersistentSubscriptionsManager"/>.
		/// </summary>
		/// <param name="log">An instance of <see cref="ILogger"/> to use for logging.</param>
		/// <param name="httpEndPoint">HTTP endpoint of an Event Store server.</param>
		/// <param name="httpSchema">HTTP endpoint schema http|https.</param>
		/// <param name="operationTimeout"></param>
		public PersistentSubscriptionsManager(ILogger log, EndPoint httpEndPoint, TimeSpan operationTimeout,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			Ensure.NotNull(log, nameof(log));
			Ensure.NotNull(httpEndPoint, nameof(httpEndPoint));
			this._client = new PersistentSubscriptionsClient(log, operationTimeout);
			this._httpEndPoint = httpEndPoint;
			this._httpSchema = httpSchema;
		}


		/// <summary>
		/// Gets the details of the persistent subscription <paramref name="subscriptionName"/> on <paramref name="stream"/>.
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="subscriptionName"></param>
		/// <param name="userCredentials">Credentials for a user with permission to read from the subscriptions endpoint</param>.
		/// <returns>A PersistentSubscriptionDetails object representing the persistent subscription <paramref name="subscriptionName"/> on <paramref name="stream"/>.</returns>
		public Task<PersistentSubscriptionDetails> Describe(string stream, string subscriptionName,
			UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, "stream");
			Ensure.NotNullOrEmpty(subscriptionName, "subscriptionName");
			return _client.Describe(_httpEndPoint, stream, subscriptionName, userCredentials, _httpSchema);
		}

		/// <summary>
		/// Replays all parked messages for a particular persistent subscription <paramref name="subscriptionName"/> on <paramref name="stream"/>. 
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="subscriptionName"></param>
		/// <param name="userCredentials">Credentials for a user with permission to replay parked messages.</param>
		/// <returns>A task representing the operation.</returns>
		public Task ReplayParkedMessages(string stream, string subscriptionName,
			UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, "stream");
			Ensure.NotNullOrEmpty(subscriptionName, "subscriptionName");
			return _client.ReplayParkedMessages(_httpEndPoint, stream, subscriptionName, userCredentials, _httpSchema);
		}

		/// <summary>
		/// Asynchronously lists all persistent subscriptions subscribed to <paramref name="stream"/>.
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="userCredentials">Credentials for the operation.</param>
		/// <returns>List of all the PersistentSubscriptionDetails items containing persistent subscription details on <paramref name="stream"/>.</returns>
		public Task<List<PersistentSubscriptionDetails>> List(string stream,
			UserCredentials userCredentials = null) {
			Ensure.NotNullOrEmpty(stream, "stream");
			return _client.List(_httpEndPoint, stream, userCredentials, _httpSchema);
		}

		/// <summary>
		/// Asynchronously lists all persistent subscriptions.
		/// </summary>
		/// <param name="userCredentials">Credentials for the operation.</param>
		/// <returns>List of all the PersistentSubscriptionDetails items containing persistent subscription details.</returns>
		public Task<List<PersistentSubscriptionDetails>> List(UserCredentials userCredentials = null) {
			return _client.List(_httpEndPoint, userCredentials, _httpSchema);
		}
	}
}
