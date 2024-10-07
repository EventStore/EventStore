// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_filter;

[TestFixture]
public class specific_category_event_filter : TestFixtureWithEventFilter {
	protected override void Given() {
		_builder.FromCategory("category");
		_builder.AllEvents();
	}

	[Test]
	public void can_be_built() {
		Assert.IsNotNull(_ef);
	}

	[Test]
	public void passes_event_with_correct_category() {
		Assert.IsTrue(_ef.Passes(true, "$ce-category", "event"));
	}

	[Test]
	public void does_not_pass_event_with_incorrect_category() {
		Assert.IsFalse(_ef.Passes(true, "$ce-another", "event"));
	}

	[Test]
	public void does_not_pass_uncategorized_event() {
		Assert.IsFalse(_ef.Passes(false, "stream", "event"));
	}
}
