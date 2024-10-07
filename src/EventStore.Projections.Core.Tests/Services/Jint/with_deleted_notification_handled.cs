// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.Jint;

[TestFixture]
public class with_deleted_notification_handled : TestFixtureWithInterpretedProjection {
	protected override void Given() {
		_projection = @"fromAll().foreachStream().when({
                $deleted: function(){}
            })";
		_state = @"{}";
	}

	[Test]
	public void source_definition_is_correct() {
		Assert.AreEqual(true, _source.HandlesDeletedNotifications);
	}
}
