using Microsoft.Extensions.Configuration;

namespace EventStore.Common.Tests.Configuration;

public class ConfigurationRootExtensionsTest {
	private static string GOSSIP_SEED = "GossipSeed";

	[Fact]
	public void successful_comma_separated_value() {
		var config = new Dictionary<string, string> {
			{ GOSSIP_SEED, "nodeb.eventstore.test:2113,nodec.eventstore.test:3113" }
		};
		IConfigurationRoot configuration = MemoryConfigurationBuilderExtensions
			.AddInMemoryCollection(new ConfigurationBuilder(), config)
			.Build();
		var values =
			EventStore.Common.Configuration.ConfigurationRootExtensions.GetCommaSeparatedValueAsArray(
				configuration, "GossipSeed");
		Assert.Equal(2, values.Length);
	}

	[Fact]
	public void invalid_delimiter() {
		var config = new Dictionary<string, string> {
			{ GOSSIP_SEED, "nodeb.eventstore.test:2113;nodec.eventstore.test:3113" }
		};
		IConfigurationRoot configuration = MemoryConfigurationBuilderExtensions
			.AddInMemoryCollection(new ConfigurationBuilder(), config)
			.Build();

		Assert.Throws<ArgumentException>(() =>
			EventStore.Common.Configuration.ConfigurationRootExtensions.GetCommaSeparatedValueAsArray(
				configuration, "GossipSeed"));
	}
	
	[Fact]
	public void mixed_invalid_delimiter() {
		var config = new Dictionary<string, string> {
			{ GOSSIP_SEED, "nodea.eventstore.test:2113,nodeb.eventstore.test:2113;nodec.eventstore.test:3113" }
		};

		var configuration = MemoryConfigurationBuilderExtensions
			.AddInMemoryCollection(new ConfigurationBuilder(), config)
			.Build();

		Assert.Throws<ArgumentException>(() =>
			EventStore.Common.Configuration.ConfigurationRootExtensions.GetCommaSeparatedValueAsArray(
				configuration, "GossipSeed"));
	}
}
