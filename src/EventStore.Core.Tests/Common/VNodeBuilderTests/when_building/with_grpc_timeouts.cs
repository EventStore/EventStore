using System;
using NUnit.Framework;

namespace EventStore.Core.Tests.Common.VNodeBuilderTests.when_building {
	[TestFixture]
	public class with_grpc_timeouts : SingleNodeScenario {
		public override void Given() {
			_builder.WithKeepAliveInterval(TimeSpan.FromMinutes(1))
				.WithKeepAliveTimeout(TimeSpan.FromMinutes(1));
		}

		[Test]
		public void should_set_keep_alive_interval() {
			Assert.AreEqual(TimeSpan.FromMinutes(1), _settings.KeepAliveInterval);
		}

		[Test]
		public void should_set_keep_alive_timeout() {
			Assert.AreEqual(TimeSpan.FromMinutes(1), _settings.KeepAliveTimeout);
		}
	}
}
