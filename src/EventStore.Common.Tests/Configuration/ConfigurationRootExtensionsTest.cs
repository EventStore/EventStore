using System.Collections;
using EventStore.Common.Configuration;
using EventStore.Common.Configuration.Sources;
using Microsoft.Extensions.Configuration;

namespace EventStore.Common.Tests.Configuration;

public class ConfigurationRootExtensionsTest {
	private const string GOSSIP_SEED = "GossipSeed";
	private const string CONFIG_FILE_KEY = "Config";
	
	[Fact]
	public void successful_comma_separated_value() {
		var config = new Dictionary<string, string?> {
			{ GOSSIP_SEED, "nodeb.eventstore.test:2113,nodec.eventstore.test:3113" }
		};
		
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(config)
			.Build();
		
		var values = configuration.GetCommaSeparatedValueAsArray("GossipSeed");
		
		Assert.Equal(2, values.Length);
	}

	[Fact]
	public void invalid_delimiter() {
		var config = new Dictionary<string, string?> {
			{ GOSSIP_SEED, "nodeb.eventstore.test:2113;nodec.eventstore.test:3113" }
		};
		
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(config)
			.Build();

		Assert.Throws<ArgumentException>(() =>
			configuration.GetCommaSeparatedValueAsArray("GossipSeed"));
	}
	
	[Fact]
	public void mixed_invalid_delimiter() {
		var config = new Dictionary<string, string?> {
			{ GOSSIP_SEED, "nodea.eventstore.test:2113,nodeb.eventstore.test:2113;nodec.eventstore.test:3113" }
		};

		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(config)
			.Build();

		Assert.Throws<ArgumentException>(() =>
			configuration.GetCommaSeparatedValueAsArray("GossipSeed"));
	}

	[Fact]
	public void user_specified_config_file_through_environment_variables_returns_true() {
		var configurationRoot = new ConfigurationBuilder()
			.AddEventStoreEnvironmentVariables(new Dictionary<string, string> {
				{ "EVENTSTORE_CONFIG", "pathToConfigFileOnMachine" }
			})
			.Build();

		var result = configurationRoot
			.IsSettingUserSpecified($"{EventStoreConfigurationKeys.Prefix}:{CONFIG_FILE_KEY}");

		Assert.True(result);
	}
	
	[Fact]
	public void user_specified_config_file_through_command_line_returns_true() {
		var args = new[] {
			"--config=pathToConfigFileOnMachine"
		};

		var configurationRoot = new ConfigurationBuilder()
			.AddEventStoreCommandLine(args)
			.Build();

		var result = configurationRoot
			.IsSettingUserSpecified($"{EventStoreConfigurationKeys.Prefix}:{CONFIG_FILE_KEY}");

		Assert.True(result);
	}

	[Fact]
	public void user_did_not_specified_config_file_returns_false() {
		var configurationRoot = new ConfigurationBuilder()
			.AddEventStoreDefaultValues(new Dictionary<string, string?>())
			.Build();

		var result = configurationRoot
			.IsSettingUserSpecified($"{EventStoreConfigurationKeys.Prefix}:{CONFIG_FILE_KEY}");

		Assert.False(result);
	}
}
