// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Data {
	public struct CommitEventRecord {
		public readonly EventRecord Event;
		public readonly long CommitPosition;

		public CommitEventRecord(EventRecord @event, long commitPosition) {
			Event = @event;
			CommitPosition = commitPosition;
		}

		public override string ToString() {
			return string.Format("CommitPosition: {0}, Event: {1}", CommitPosition, Event);
		}
	}
}
