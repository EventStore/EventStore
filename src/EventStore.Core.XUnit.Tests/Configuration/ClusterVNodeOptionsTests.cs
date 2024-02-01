#nullable enable

using EventStore.Core.Configuration;
using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Configuration;

public class ClusterVNodeOptionsTests {
	static ClusterVNodeOptions GetOptions(string args) {
		var configuration = EventStoreConfiguration.Build(args.Split());
		return ClusterVNodeOptions.FromConfiguration(configuration);
	}

	[Fact]
	public void builds_proper() {
		var options = new ClusterVNodeOptions();
	}

	[Fact]
	public void confirm_suggested_option() {
		var options = GetOptions("--cluster-sze 3");

		var (key, value) = options.Unknown.Options[0];
		Assert.Equal("ClusterSze", key);
		Assert.Equal("ClusterSize", value);
	}

	[Fact]
	public void when_we_dont_have_that_option() {
		var options = GetOptions("--something-that-is-wildly-off 3");

		var (key, value) = options.Unknown.Options[0];
		Assert.Equal("SomethingThatIsWildlyOff", key);
		Assert.Equal("", value);
	}

	[Fact]
	public void valid_parameters() {
		var options = GetOptions("--cluster-size 3");

		var values = options.Unknown.Options;
		Assert.Empty(values);
	}

	[Fact]
	public void four_characters_off() {
		var options = GetOptions("--cluse-ie 3");

		var (key, value) = options.Unknown.Options[0];
		Assert.Equal("CluseIe", key);
		Assert.Equal("ClusterSize", value);
	}
	
	[Fact]
	public void print_help_text() {
		var helpText = ClusterVNodeOptions.HelpText;
		helpText.Should().NotBeEmpty();
	}

	[Fact]
	public void ignores_subsection_arguments() {
		var configuration = new ConfigurationBuilder()
			.AddEventStoreDefaultValues()
			.AddEventStoreEnvironmentVariables(
				("EVENTSTORE__METRICS__X", "xxx"),
				("EVENTSTORE__PLUGINS__Y", "yyy")
			)
			.AddEventStoreCommandLine(
				"--EventStore:Metrics:A aaa " +
				"--EventStore:Plugins:B bbb"
			)
			.Build();
		
		var options = ClusterVNodeOptions.FromConfiguration(configuration);
		
		options.Unknown.Options.Should().BeEmpty();
	}
	
	[Fact]
	public void validation_should_return_error_when_default_password_options_pass_through_command_line() {
		var configuration =  new ConfigurationBuilder()
			.AddEventStoreDefaultValues()
			.AddEventStoreCommandLine(
				"--DefaultAdminPassword=Admin#",
				"--DefaultOpsPassword=Ops#")
			.Build();
			
		var options = ClusterVNodeOptions.FromConfiguration(configuration);

		var result = options.CheckForEnvironmentOnlyOptions();

		result.Should().NotBeNull();
	}
	
	[Fact]
	public void validation_should_return_null_when_default_password_options_pass_through_environment_variables() {
		var configuration = new ConfigurationBuilder()
			.AddEventStoreDefaultValues()
			.AddEventStoreEnvironmentVariables(
				("EVENTSTORE_DEFAULT_ADMIN_PASSWORD", "Admin#"),
				("EVENTSTORE_DEFAULT_OPS_PASSWORD", "Ops#")
			)
			.AddEventStoreCommandLine()
			.Build();
		
		var options = ClusterVNodeOptions.FromConfiguration(configuration);
		
		var result = options.CheckForEnvironmentOnlyOptions();

		result.Should().BeNull();
	}

	[Fact]
	public void BindWorks() {
		var config = new ConfigurationBuilder()
			.AddEventStoreDefaultValues()
			.Build();
			
		var manual = ClusterVNodeOptions.FromConfiguration(config);
		var binded = ClusterVNodeOptions.BindFromConfiguration(config);
			
		manual.Should().BeEquivalentTo(binded);
	}

	// [Fact]
	// public void BindCommaSeparatedValuesOption() {
	// 	EndPoint[] endpoints = [new IPEndPoint(IPAddress.Loopback, 1113), new DnsEndPoint("some-host", 1114)];
	// 		
	// 	var values = string.Join(",", endpoints.Select(x => $"{x}"));
	//
	// 	var config = new ConfigurationBuilder()
	// 		.AddInMemoryCollection(new KeyValuePair<string, string>[] {
	// 			new("GossipSeed", values),
	// 			new("FakeEndpoint", endpoints[0].ToString())
	// 		})
	// 		.Build();
	// 		
	// 	var options = config.Get<ClusterVNodeOptions.ClusterOptions>();
	//
	// 	options.GossipSeed.Should().BeEquivalentTo(endpoints);
	// }
}
