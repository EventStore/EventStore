using EventStore.Common.Options;
using EventStore.Core.Util;
using NUnit.Framework;
using System;
using System.Net;
using EventStore.Core.Tests.Helpers;

namespace EventStore.Core.Tests.Common.EventStoreOptionsTests.when_parsing {
	[TestFixture]
	public class with_a_complex_type {
		[Test]
		public void should_be_able_to_parse_the_value_from_command_line() {
			var args = new[] {"-gossip-seed", "127.0.0.1:1000,127.0.0.2:2000"};
			var testArgs = EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);
			Assert.AreEqual(new[] {
				new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1000),
				new IPEndPoint(IPAddress.Parse("127.0.0.2"), 2000)
			}, testArgs.GossipSeed);
		}

		[Test]
		public void should_be_able_to_parse_the_value_from_a_config_file() {
			var args = new[]
				{"-config", HelperExtensions.GetFilePathFromAssembly("TestConfigs/test_config.yaml")};
			var testArgs = EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);
			Assert.AreEqual(new[] {
				new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1000),
				new IPEndPoint(IPAddress.Parse("127.0.0.2"), 2000)
			}, testArgs.GossipSeed);
		}

		[Test]
		public void should_be_able_to_parse_the_value_from_an_environment_variable() {
			Environment.SetEnvironmentVariable($"{Opts.EnvPrefix}GOSSIP_SEED",
				"127.0.0.1:1000,127.0.0.2:2000");
			var args = Array.Empty<string>();
			var testArgs = EventStoreOptions.Parse<TestArgs>(args, Opts.EnvPrefix);
			Assert.AreEqual(new[] {
				new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1000),
				new IPEndPoint(IPAddress.Parse("127.0.0.2"), 2000)
			}, testArgs.GossipSeed);
		}
	}
}
