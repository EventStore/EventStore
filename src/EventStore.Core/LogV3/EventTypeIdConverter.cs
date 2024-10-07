// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.LogV3;

/// Converts between EventTypeIds and their event number in the event types stream.
public static class EventTypeIdConverter {
	static readonly uint _offset = LogV3SystemEventTypes.FirstRealEventTypeNumber;

	public static uint ToEventTypeId(long index) {
		return (uint)index + _offset;
	}

	public static long ToEventNumber(uint eventTypeId) {
		return eventTypeId - _offset;
	}
}
