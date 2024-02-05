#nullable enable

using System;
using EventStore.Common.Options;
using EventStore.Core.Configuration;
using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Configuration;

public class ClusterVNodeOptionsPrinterTests {
	[Fact]
	public void prints_options() {
		var config = new ConfigurationBuilder()
			.AddEventStoreDefaultValues()
			.AddEventStoreEnvironmentVariables(
				("EVENTSTORE_CLUSTER_GOSSIP_PORT", "99"),
				("EVENTSTORE_UNSAFE_ALLOW_SURPLUS_NODES", "true"),
				("EVENTSTORE_CONFIG", "/path/to/config/envvar"),
				("EVENTSTORE_RUN_PROJECTIONS", "All")
			)
			.AddEventStoreCommandLine(
				"--config", "/path/to/config/commandline",
				"--cluster-gossip-port=88"
			)
			.Build();

		var loadedOptions = ClusterVNodeOptions.GetLoadedOptions(config);
		
		var output = ClusterVNodeOptionsPrinter.Print(loadedOptions);
		
#pragma warning disable CS0618 // Type or member is obsolete
		var oldOutput = new OptionsDumper(ClusterVNodeOptions.OptionSections).Dump(config);
#pragma warning restore CS0618 // Type or member is obsolete
		
		output.Should().BeEquivalentTo(oldOutput);
	}

	[Fact]
	public void loaded_options_do_not_contain_sensitive_values() {
		var secretText = Guid.NewGuid().ToString();
		
		var config = new ConfigurationBuilder()
			.AddEventStoreDefaultValues()
			.AddEventStoreCommandLine($"--default-ops-password={secretText}")
			.Build();
		
		var loadedOptions = ClusterVNodeOptions.GetLoadedOptions(config);

		var option = loadedOptions["EventStore:DefaultOpsPassword"];
		
		option.Value.Should().BeEquivalentTo(secretText);
		option.DisplayValue.Should().BeEquivalentTo(new('*', 8));
	}

	[Fact]
	public void loaded_options_show_allowed_values() {
		var config = new ConfigurationBuilder()
			.AddEventStoreDefaultValues()
			.Build();
		
		var loadedOptions = ClusterVNodeOptions.GetLoadedOptions(config);
		
		var option = loadedOptions["EventStore:DbLogFormat"];
		
		option.Metadata.AllowedValues.Should().BeEquivalentTo(Enum.GetNames(typeof(DbLogFormat)));
	}
	
	[Fact]
	public void loaded_option_provided_by_another_source_shows_the_correct_source() {
		var expectedValue             = LogLevel.Information.ToString();
		var expectedSourceDisplayName = "(Command Line)";
		
		var config = new ConfigurationBuilder()
			.AddEventStoreDefaultValues()
			.AddEventStoreEnvironmentVariables(("EVENTSTORE_LOG_LEVEL", LogLevel.Fatal.ToString()))
			.AddEventStoreCommandLine($"--log-level={expectedValue}")
			.Build();
		
		var loadedOptions = ClusterVNodeOptions.GetLoadedOptions(config);
		
		var option = loadedOptions["EventStore:LogLevel"];
		
		option.Value.Should().BeEquivalentTo(expectedValue);
		option.SourceDisplayName.Should().BeEquivalentTo(expectedSourceDisplayName);
	}
}
