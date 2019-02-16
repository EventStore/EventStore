using System;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_filter {
	[TestFixture]
	public class empty_event_filter : TestFixtureWithEventFilter {
		[Test]
		public void cannot_be_built() {
			Assert.IsAssignableFrom(typeof(InvalidOperationException), _exception);
		}
	}
}
