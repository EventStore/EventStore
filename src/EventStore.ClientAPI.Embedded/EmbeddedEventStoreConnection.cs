using EventStore.Core;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;

namespace EventStore.ClientAPI.Embedded {
	/// <summary>
	/// Contains factory methods to build a connection to an in-process EventStore
	/// </summary>
	public static class EmbeddedEventStoreConnection {
		private static IEventStoreConnection Create(IPublisher queue, ISubscriber bus,
			IAuthenticationProvider authenticationProvider, ConnectionSettings connectionSettings,
			string connectionName = null) {
			return new EventStoreEmbeddedNodeConnection(connectionSettings, connectionName, queue, bus,
				authenticationProvider);
		}

		/// <summary>
		/// Creates a new embedded <see cref="IEventStoreConnection"/> to single node with default connection settings
		/// </summary>
		/// <param name="eventStore">The <see cref="ClusterVNode" /> to connect to. The node must already be running.</param>
		/// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
		/// <returns></returns>
		public static IEventStoreConnection Create(ClusterVNode eventStore, string connectionName = null) {
			return Create(eventStore, ConnectionSettings.Default, connectionName);
		}

		/// <summary>
		/// Creates a new embedded <see cref="IEventStoreConnection"/> to single node using specific <see cref="ConnectionSettings"/>
		/// </summary>
		/// <param name="eventStore">The <see cref="ClusterVNode" /> to connect to. The node must already be running.</param>
		/// <param name="connectionSettings">The <see cref="ConnectionSettings"/> to apply to the new connection</param>
		/// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
		/// <returns></returns>
		public static IEventStoreConnection Create(ClusterVNode eventStore, ConnectionSettings connectionSettings,
			string connectionName = null) {
			return Create(eventStore.MainQueue, eventStore.MainBus, eventStore.InternalAuthenticationProvider,
				connectionSettings, connectionName);
		}
	}
}
