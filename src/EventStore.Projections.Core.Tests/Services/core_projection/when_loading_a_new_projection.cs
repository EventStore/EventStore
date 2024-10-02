// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Linq;
using EventStore.Core.Tests;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_loading_a_new_projection<TLogFormat, TStreamId> : TestFixtureWithCoreProjectionLoaded<TLogFormat, TStreamId> {
		protected override void Given() {
			NoStream("$projections-projection-result");
			NoStream("$projections-projection-order");
			AllWritesToSucceed("$projections-projection-order");
			NoStream("$projections-projection-checkpoint");
		}

		protected override void When() {
		}

		[Test]
		public void should_not_subscribe() {
			Assert.AreEqual(0, _subscribeProjectionHandler.HandledMessages.Count);
		}

		[Test]
		public void should_not_initialize_projection_state_handler() {
			Assert.AreEqual(0, _stateHandler._initializeCalled);
		}

		[Test]
		public void should_not_publish_started_message() {
			Assert.AreEqual(0, _consumer.HandledMessages.OfType<CoreProjectionStatusMessage.Started>().Count());
		}
	}
}
