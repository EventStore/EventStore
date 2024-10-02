// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Data {
	public readonly struct EventInfo {
		public readonly long LogPosition;
		public readonly long EventNumber;

		public EventInfo(long logPosition, long eventNumber) {
			LogPosition = logPosition;
			EventNumber = eventNumber;
		}
	}
}
