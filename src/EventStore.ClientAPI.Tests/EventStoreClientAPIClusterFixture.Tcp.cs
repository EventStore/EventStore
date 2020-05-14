using System;
using System.Net;
using System.Net.Http;
using EventStore.Common.Utils;

namespace EventStore.ClientAPI.Tests {
	partial class EventStoreClientAPIClusterFixture {
		private const bool UseLoggerBridge = true;
		public IEventStoreConnection CreateConnectionWithGossipSeeds(Func<ConnectionSettingsBuilder, ConnectionSettingsBuilder> configureSettings = default, bool useDnsEndPoint = false) {
			var settings = (configureSettings ?? DefaultConfigureSettings)(DefaultBuilder)
			.UseCustomHttpMessageHandler(new SocketsHttpHandler {
				SslOptions = {
					RemoteCertificateValidationCallback = delegate { return true; }
				}
			})
			.Build();
			var gossipSeeds = GetGossipSeedEndPointsExceptFor(-1, useDnsEndPoint);
			var clusterSettings = new ClusterSettingsBuilder()
				.DiscoverClusterViaGossipSeeds()
				.SetGossipSeedEndPoints(true, gossipSeeds)
				.SetMaxDiscoverAttempts(1)
				.Build();
			return EventStoreConnection.Create(settings, clusterSettings);
		}

		public IEventStoreConnection CreateConnectionWithConnectionString(
			bool useSsl,
			string configureSettings = default,
			bool useDnsEndPoint = false) {
			var settings = configureSettings ?? DefaultConfigureSettingsForConnectionString;
			var host = useDnsEndPoint ? "localhost" : IPAddress.Loopback.ToString();

			if (useSsl) settings += "UseSslConnection=true;";

			var gossipSeeds = GetGossipSeedEndPointsExceptFor(-1, useDnsEndPoint);
			var gossipSeedsString = "";
			for(var i=0;i<gossipSeeds.Length;i++) {
				if (i > 0) gossipSeedsString += ",";
				gossipSeedsString += host + ":" + gossipSeeds[i].GetPort();
			}

			var connectionString = $"GossipSeeds={gossipSeedsString};{settings}";
			return EventStoreConnection.Create(connectionString);
		}
	}
}
