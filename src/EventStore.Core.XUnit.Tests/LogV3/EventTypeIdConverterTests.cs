// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.LogV3;
using Xunit;

namespace EventStore.Core.XUnit.Tests.LogV3;

public class EventTypeIdConverterTests {

	[Fact]
	public void returns_expected_event_type_id() {
		// guards against unexpected interval change

		var index = 0;
		Assert.Equal(GetExpectedEventId(index), EventTypeIdConverter.ToEventTypeId(index));

		index = 9;
		Assert.Equal(GetExpectedEventId(index), EventTypeIdConverter.ToEventTypeId(index));
	}

	[Fact]
	public void returns_expected_event_number() {
		// guards against unexpected interval change

		var eventTypeId = 3000U;
		Assert.Equal(GetExpectedEventNumber(eventTypeId), EventTypeIdConverter.ToEventNumber(eventTypeId));

		eventTypeId = 3001U;
		Assert.Equal(GetExpectedEventNumber(eventTypeId), EventTypeIdConverter.ToEventNumber(eventTypeId));
	}

	private long GetExpectedEventId(int index) {
		var offset = LogV3SystemEventTypes.FirstRealEventTypeNumber;
		return index * LogV3SystemEventTypes.EventTypeInterval + offset;
	}

	private long GetExpectedEventNumber(uint eventTypeId) {
		var offset = LogV3SystemEventTypes.FirstRealEventTypeNumber;
		return (eventTypeId - offset) / LogV3SystemEventTypes.EventTypeInterval;
	}
}
