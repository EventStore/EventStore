// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Messages;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.VNode.InaugurationManagement {
	[TestFixture]
	public class given_initial_state : InaugurationManagerTests {
		protected override void Given() {
			_publisher.Messages.Clear();
		}

		[Test]
		public void when_become_pre_leader() {
			When(new SystemMessage.BecomePreLeader(_correlationId1));
			AssertWaitingForChaser(_correlationId1);
		}

		[Test]
		public void when_become_other_node_state() {
			When(new SystemMessage.BecomeUnknown(Guid.NewGuid()));
			AssertInitial();
		}
	}
}
