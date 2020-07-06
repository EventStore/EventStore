using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using EventStore.ClientAPI.Transport.Http;

namespace EventStore.ClientAPI.Tests {
	partial class EventStoreClientAPIClusterFixture {
		private const bool UseLoggerBridge = true;

		public IEventStoreConnection CreateConnectionWithGossipSeeds(Func<ConnectionSettingsBuilder, ConnectionSettingsBuilder> configureSettings = default, bool useDnsEndPoint = false) {
			var settings = (configureSettings ?? DefaultConfigureSettings)(DefaultBuilder)
				.UseCustomHttpClient(new HttpAsyncClient(TimeSpan.FromSeconds(5),
					new HttpClientHandler {
						ServerCertificateCustomValidationCallback = delegate { return true; }
					}))
				.Build();
			var gossipSeeds = GetGossipSeedEndPointsExceptFor(-1, false).Cast<IPEndPoint>().ToArray();
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

			if (useSsl) settings += $"UseSslConnection=true;ValidateServer=false;TargetHost={host};";
			else settings += "UseSslConnection=false;";

			var gossipSeeds = GetGossipSeedEndPointsExceptFor(-1, false).Cast<IPEndPoint>().ToArray();
			var gossipSeedsString = "";
			for(var i=0;i<gossipSeeds.Length;i++) {
				if (i > 0) gossipSeedsString += ",";
				gossipSeedsString += (useSsl?"https://":"") + host + ":" + gossipSeeds[i].Port;
			}

			settings += "SkipCertificateValidation=true;";

			var connectionString = $"GossipSeeds={gossipSeedsString};{settings}";
			return EventStoreConnection.Create(connectionString);
		}
	}
}
