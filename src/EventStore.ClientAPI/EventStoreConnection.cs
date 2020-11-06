using System.Net;
using System.Linq;
using System.Net.Sockets;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Internal;
using System;
using System.Runtime.ExceptionServices;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Contains factory methods for building connections to an Event Store server.
	/// </summary>
	public static class EventStoreConnection {
		private static readonly ExceptionDispatchInfo SerializationInitializationError =
			ClientMessage.InitializeSerializers();

		/// <summary>
		/// Creates a new <see cref="IEventStoreConnection"/> to single node using default <see cref="ConnectionSettings"/>
		/// </summary>
		/// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
		/// <param name="uri">The Uri to connect to. It can be tcp:// to point to a single node or discover:// to discover nodes</param>
		/// <returns>a new <see cref="IEventStoreConnection"/></returns>
		public static IEventStoreConnection Create(Uri uri, string connectionName = null) {
			return Create(ConnectionSettings.Default, uri, connectionName);
		}

		/// <summary>
		/// Creates a new <see cref="IEventStoreConnection"/> to single node using default <see cref="ConnectionSettings"/> provided via a connectionstring
		/// </summary>
		/// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
		/// <param name="connectionString">The connection string to for this connection.</param>
		/// <returns>a new <see cref="IEventStoreConnection"/></returns>
		public static IEventStoreConnection Create(string connectionString, string connectionName = null) {
			return Create(connectionString, null, connectionName);
		}

		/// <summary>
		/// Creates a new <see cref="IEventStoreConnection"/> to single node using default <see cref="ConnectionSettings"/> provided via a connectionstring
		/// </summary>
		/// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
		/// <param name="builder">Pre-populated settings builder, optional. If not specified, a new builder will be created.</param>
		/// <param name="connectionString">The connection string to for this connection.</param>
		/// <returns>a new <see cref="IEventStoreConnection"/></returns>
		public static IEventStoreConnection Create(string connectionString, ConnectionSettingsBuilder builder,
			string connectionName = null) {
			var settings = ConnectionString.GetConnectionSettings(connectionString, builder);
			var uri = GetUriFromConnectionString(connectionString);
			if (uri == null && (settings.GossipSeeds == null || settings.GossipSeeds.Length == 0)) {
				throw new Exception(string.Format(
					"Did not find ConnectTo or GossipSeeds in the connection string.\n'{0}'", connectionString));
			}

			if (uri != null && settings.GossipSeeds != null && settings.GossipSeeds.Length > 0) {
				throw new NotSupportedException(string.Format(
					"Setting ConnectTo as well as GossipSeeds on the connection string is currently not supported.\n{0}",
					connectionString));
			}

			return Create(settings, uri, connectionName);
		}

		/// <summary>
		/// Creates a new <see cref="IEventStoreConnection"/> using the gossip seeds specified in the <paramref name="connectionSettings"/>
		/// </summary>
		/// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
		/// <param name="connectionSettings">The <see cref="ConnectionSettings"/> to apply to the new connection</param>
		/// <returns>a new <see cref="IEventStoreConnection"/></returns>
		public static IEventStoreConnection
			Create(ConnectionSettings connectionSettings, string connectionName = null) {
			if (connectionSettings.GossipSeeds == null || connectionSettings.GossipSeeds.Length == 0)
				throw new ArgumentException("No gossip seeds specified", nameof(connectionSettings));
			return Create(connectionSettings, (Uri)null, connectionName);
		}

		/// <summary>
		/// Creates a new <see cref="IEventStoreConnection"/>
		/// </summary>
		/// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
		/// <param name="connectionSettings">The <see cref="ConnectionSettings"/> to apply to the new connection. If null the default settings will be used and the <paramref name="uri"/> must not be null</param>
		/// <param name="uri">The Uri to connect to. It can be tcp:// to point to a single node or discover:// to discover nodes via dns or null to connect using the gossip seeds from the <paramref name="connectionSettings"/></param>
		/// <returns>a new <see cref="IEventStoreConnection"/></returns>
		/// <remarks>You must pass a uri or set gossip seeds in the connection settings.</remarks>
		public static IEventStoreConnection Create(ConnectionSettings connectionSettings, Uri uri,
			string connectionName = null) {
			SerializationInitializationError?.Throw();
			connectionSettings = connectionSettings ?? ConnectionSettings.Default;
			if (uri != null) {
				var scheme = uri.Scheme.ToLower();
				var credential = GetCredentialFromUri(uri);
				if (credential != null) {
					connectionSettings = new ConnectionSettings(connectionSettings.Log,
						connectionSettings.VerboseLogging,
						connectionSettings.MaxQueueSize, connectionSettings.MaxConcurrentItems,
						connectionSettings.MaxRetries, connectionSettings.MaxReconnections,
						connectionSettings.RequireLeader, connectionSettings.ReconnectionDelay,
						connectionSettings.QueueTimeout, connectionSettings.OperationTimeout,
						connectionSettings.OperationTimeoutCheckPeriod, credential, connectionSettings.UseSslConnection,
						connectionSettings.ValidateServer, connectionSettings.FailOnNoServerResponse,
						connectionSettings.HeartbeatInterval, connectionSettings.HeartbeatTimeout,
						connectionSettings.ClientConnectionTimeout, connectionSettings.ClusterDns,
						connectionSettings.GossipSeeds, connectionSettings.MaxDiscoverAttempts,
						connectionSettings.InsecureDiscoverGossip,
						connectionSettings.GossipPort, connectionSettings.GossipTimeout,
						connectionSettings.NodePreference, connectionSettings.CustomHttpMessageHandler);
				}

				if (scheme == "discover") {
					var clusterSettings = new ClusterSettings(uri.Host, connectionSettings.MaxDiscoverAttempts,
						uri.Port,
						connectionSettings.GossipTimeout, connectionSettings.NodePreference);
					return Create(connectionSettings, clusterSettings, connectionName);
				}

				if (scheme == "tcp") {
					var tcpEndPoint = GetSingleNodeEndPointFrom(uri);
					return new EventStoreNodeConnection(connectionSettings, null,
						new StaticEndPointDiscoverer(tcpEndPoint, connectionSettings.UseSslConnection), connectionName);
				}

				throw new Exception(string.Format("Unknown scheme for connection '{0}'", scheme));
			}

			if (connectionSettings.GossipSeeds != null && connectionSettings.GossipSeeds.Length > 0) {
				var clusterSettings = new ClusterSettings(connectionSettings.GossipSeeds,
					connectionSettings.MaxDiscoverAttempts,
					connectionSettings.GossipTimeout,
					connectionSettings.NodePreference);
				return Create(connectionSettings, clusterSettings, connectionName);
			}

			throw new Exception($"Must specify uri or gossip seeds");
		}

		private static EndPoint GetSingleNodeEndPointFrom(Uri uri) {
			var host = uri.Host;
			var port = uri.IsDefaultPort ? 2113 : uri.Port;
			if (IPAddress.TryParse(host, out IPAddress ip))
				return new IPEndPoint(ip, port);
			return new DnsEndPoint(host, port);
		}

		private static UserCredentials GetCredentialFromUri(Uri uri) {
			if (uri == null || string.IsNullOrEmpty(uri.UserInfo)) return null;
			var pieces = uri.UserInfo.Split(':');
			if (pieces.Length != 2)
				throw new Exception(string.Format("Unable to parse user information '{0}'", uri.UserInfo));
			return new UserCredentials(pieces[0], pieces[1]);
		}

		private static Uri GetUriFromConnectionString(string connectionString) {
			var connto = ConnectionString.GetConnectionStringInfo(connectionString)
				.FirstOrDefault(x => x.Key.ToUpperInvariant() == "CONNECTTO").Value;
			return connto == null ? null : new Uri(connto);
		}

		/// <summary>
		/// Creates a new <see cref="IEventStoreConnection"/> to single node using default <see cref="ConnectionSettings"/>
		/// </summary>
		/// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
		/// <param name="tcpEndPoint">The <see cref="EndPoint"/> to connect to.</param>
		/// <returns>a new <see cref="IEventStoreConnection"/></returns>
		public static IEventStoreConnection Create(EndPoint tcpEndPoint, string connectionName = null) {
			return Create(ConnectionSettings.Default, tcpEndPoint, connectionName);
		}

		/// <summary>
		/// Creates a new <see cref="IEventStoreConnection"/> to single node using specific <see cref="ConnectionSettings"/>
		/// </summary>
		/// <param name="connectionSettings">The <see cref="ConnectionSettings"/> to apply to the new connection</param>
		/// <param name="tcpEndPoint">The <see cref="EndPoint"/> to connect to.</param>
		/// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
		/// <returns>a new <see cref="IEventStoreConnection"/></returns>
		public static IEventStoreConnection Create(ConnectionSettings connectionSettings, EndPoint tcpEndPoint,
			string connectionName = null) {
			Ensure.NotNull(connectionSettings, "settings");
			Ensure.NotNull(tcpEndPoint, "tcpEndPoint");
			return new EventStoreNodeConnection(connectionSettings, null,
				new StaticEndPointDiscoverer(tcpEndPoint, connectionSettings.UseSslConnection), connectionName);
		}

		/// <summary>
		/// Creates a new <see cref="IEventStoreConnection"/> to EventStore cluster
		/// using specific <see cref="ConnectionSettings"/> and <see cref="ClusterSettings"/>
		/// </summary>
		/// <param name="connectionSettings">The <see cref="ConnectionSettings"/> to apply to the new connection</param>
		/// <param name="clusterSettings">The <see cref="ClusterSettings"/> that determine cluster behavior.</param>
		/// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
		/// <returns>a new <see cref="IEventStoreConnection"/></returns>
		public static IEventStoreConnection Create(ConnectionSettings connectionSettings,
			ClusterSettings clusterSettings, string connectionName = null) {
			Ensure.NotNull(connectionSettings, "connectionSettings");
			Ensure.NotNull(clusterSettings, "clusterSettings");

			var endPointDiscoverer = new ClusterDnsEndPointDiscoverer(connectionSettings.Log,
				clusterSettings.ClusterDns,
				clusterSettings.MaxDiscoverAttempts,
				clusterSettings.HttpPort,
				clusterSettings.GossipSeeds,
				clusterSettings.GossipTimeout,
				clusterSettings.NodePreference,
				connectionSettings.InsecureDiscoverGossip,
				connectionSettings.CustomHttpMessageHandler);

			return new EventStoreNodeConnection(connectionSettings, clusterSettings, endPointDiscoverer,
				connectionName);
		}

		/// <summary>
		/// Creates a new <see cref="IEventStoreConnection"/> using specific <see cref="ConnectionSettings"/> and a custom-defined <see cref="IEndPointDiscoverer"/>
		/// </summary>
		/// <param name="connectionSettings">The <see cref="ConnectionSettings"/> to apply to the new connection</param>
		/// <param name="endPointDiscoverer">The custom-defined <see cref="IEndPointDiscoverer"/> to use for node discovery</param>
		/// <param name="connectionName">Optional name of connection (will be generated automatically, if not provided)</param>
		/// <returns>a new <see cref="IEventStoreConnection"/></returns>
		public static IEventStoreConnection Create(ConnectionSettings connectionSettings,
			IEndPointDiscoverer endPointDiscoverer, string connectionName = null) {
			Ensure.NotNull(connectionSettings, "settings");
			Ensure.NotNull(endPointDiscoverer, "endPointDiscoverer");
			return new EventStoreNodeConnection(connectionSettings, null, endPointDiscoverer, connectionName);
		}
	}
}
