using EventStore.Core.Configuration.Sources;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Configuration {
	public class CommandLineConfigurationSourceTests {
		[Theory]
		[InlineData(new[] { "--stream_info_cache_capacity=99" }, "EventStore:StreamInfoCacheCapacity", "99")]
		[InlineData(new[] { "--stream_info_cache_capacity", "99" }, "EventStore:StreamInfoCacheCapacity", "99")]
		[InlineData(new[] { "--stream-info-cache-capacity=99" }, "EventStore:StreamInfoCacheCapacity", "99")]
		[InlineData(new[] { "--stream-info-cache-capacity", "99" }, "EventStore:StreamInfoCacheCapacity", "99")]
		[InlineData(new[] { "--EventStore:StreamInfoCacheCapacity=99" }, "EventStore:StreamInfoCacheCapacity", "99")]
		[InlineData(new[] { "--EventStore:StreamInfoCacheCapacity", "99" }, "EventStore:StreamInfoCacheCapacity", "99")]
		[InlineData(new[] { "--EventStore:Stream-Info-Cache-Capacity=99" }, "EventStore:StreamInfoCacheCapacity", "99")]
		[InlineData(new[] { "--EventStore:Stream-Info-Cache-Capacity", "99" }, "EventStore:StreamInfoCacheCapacity", "99")]
		[InlineData(new[] { "--EventStore:Stream_Info_Cache_Capacity=99" }, "EventStore:StreamInfoCacheCapacity", "99")]
		[InlineData(new[] { "--EventStore:Stream_Info_Cache_Capacity", "99" }, "EventStore:StreamInfoCacheCapacity", "99")]
		public void AddsArguments(string[] arguments, string normalizedKey, string expectedValue) {
			// Act
			var configuration = new ConfigurationBuilder()
				.AddEventStoreCommandLine(arguments)
				.Build();

			// Assert
			configuration.GetValue<string>(normalizedKey).Should().Be(expectedValue);
		}

		static IConfiguration BuildConfiguration(params string[] args) =>
			new ConfigurationBuilder()
				.AddEventStoreCommandLine(args)
				.Build()
				.GetSection(EventStoreConfigurationKeys.Prefix);

		[Fact]
		public void normalize_keys() {
			var configuration = BuildConfiguration("--cluster-size", "3", "--log", "/tmp/eventstore/logs");
			Assert.Equal(3, configuration.GetValue<int>("ClusterSize"));
			Assert.Equal("/tmp/eventstore/logs", configuration.GetValue<string>("Log"));
		}

		[Fact]
		public void normalize_keys_boolean_plus_sign() {
			var configuration = BuildConfiguration("--whatever+");
			Assert.Equal("true", configuration.GetValue<string>("Whatever"));
		}

		[Fact]
		public void normalize_keys_boolean_negative_sign() {
			var configuration = BuildConfiguration("--whatever-");
			Assert.Equal("false", configuration.GetValue<string>("Whatever"));
		}

		[Fact]
		public void normalize_keys_boolean_no_value() {
			var configuration = BuildConfiguration("--whatever");
			Assert.Equal("true", configuration.GetValue<string>("Whatever"));
		}

		[Fact]
		public void normalize_keys_equals() {
			var configuration = BuildConfiguration("--cluster-size=3");
			Assert.Equal(3, configuration.GetValue<int>("ClusterSize"));
		}

		[Fact]
		public void normalize_keys_one_dash() {
			var configuration = BuildConfiguration("-cluster-size=3");
			Assert.Equal(3, configuration.GetValue<int>("ClusterSize"));
		}
	}
}