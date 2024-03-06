#nullable enable

using System;
using EventStore.Common.Options;
using EventStore.Core.Configuration;
using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Configuration {
	public class ClusterVNodeOptionsPrinterTests {
		[Fact]
		public void prints_options() {
			var config = new ConfigurationBuilder()
				.AddEventStoreDefaultValues()
				.AddEventStoreEnvironmentVariables(
					("EVENTSTORE_CLUSTER_GOSSIP_PORT", "99"),
					("EVENTSTORE_UNSAFE_ALLOW_SURPLUS_NODES", "true"),
					("EVENTSTORE_CONFIG", "/path/to/config/envvar"),
					("EVENTSTORE_RUN_PROJECTIONS", "All"))
				.AddEventStoreCommandLine(
					"--config", "/path/to/config/commandline",
					"--cluster-gossip-port=88",
					$"--default-ops-password={Guid.NewGuid()}")
				.Build();

			var loadedOptions = ClusterVNodeOptions.GetLoadedOptions(config);

			var output = ClusterVNodeOptionsPrinter.Print(loadedOptions);

			output.Should().NotBeEmpty();
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
			var config = new ConfigurationBuilder()
				.AddEventStoreDefaultValues()
				.AddEventStoreEnvironmentVariables(
					("EVENTSTORE_CLUSTER_SIZE", "15"),
					("EVENTSTORE_LOG_LEVEL", "Fatal"))
				.AddEventStoreCommandLine($"--log-level=Information")
				.Build();

			var loadedOptions = ClusterVNodeOptions.GetLoadedOptions(config);

			// default
			var insecure = loadedOptions["EventStore:Insecure"];
			insecure.Value.Should().BeEquivalentTo("false");
			insecure.SourceDisplayName.Should().BeEquivalentTo("(<DEFAULT>)");

			// environment variables
			var clusterSize = loadedOptions["EventStore:ClusterSize"];
			clusterSize.Value.Should().BeEquivalentTo("15");
			clusterSize.SourceDisplayName.Should().BeEquivalentTo("(Environment Variables)");

			// command line
			var logLevel = loadedOptions["EventStore:LogLevel"];
			logLevel.Value.Should().BeEquivalentTo("Information");
			logLevel.SourceDisplayName.Should().BeEquivalentTo("(Command Line)");
		}
	}
}
