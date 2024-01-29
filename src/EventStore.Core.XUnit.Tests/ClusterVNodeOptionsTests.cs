#nullable enable
using System.Collections.Generic;
using EventStore.Core.Configuration;
using Xunit;

namespace EventStore.Core.Tests;

public class ClusterVNodeOptionsTests {
	private static ClusterVNodeOptions BuildConfiguration(string args) {
		var configuration = EventStoreConfiguration.Build(args.Split());
		var sut = ClusterVNodeOptions.FromConfiguration(configuration);
		return sut;
	}

	[Fact]
	public void confirm_suggested_option() {
		var options = BuildConfiguration("--cluster-sze 3");

		var (key, value) = options.Unknown.Options[0];
		Assert.Equal("ClusterSze", key);
		Assert.Equal("ClusterSize", value);
	}

	[Fact]
	public void when_we_dont_have_that_option() {
		var c = BuildConfiguration("--something-that-is-wildly-off 3");

		var (key, value) = c.Unknown.Options[0];
		Assert.Equal("SomethingThatIsWildlyOff", key);
		Assert.Equal("", value);
	}

	[Fact]
	public void valid_parameters() {
		var c = BuildConfiguration("--cluster-size 3");

		var values = c.Unknown.Options;
		Assert.Empty(values);
	}

	[Fact]
	public void four_characters_off() {
		var c = BuildConfiguration("--cluse-ie 3");

		var (key, value) = c.Unknown.Options[0];
		Assert.Equal("CluseIe", key);
		Assert.Equal("ClusterSize", value);
	}
}
