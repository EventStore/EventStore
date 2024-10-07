// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_filter;

[TestFixture]
public class specific_stream_and_category_event_filter : TestFixtureWithEventFilter {
	protected override void Given() {
		_builder.FromCategory("category");
		_builder.FromStream("/test");
		_builder.AllEvents();
	}

	[Test]
	public void cannot_be_built() {
		Assert.IsAssignableFrom(typeof(InvalidOperationException), _exception);
	}
}
