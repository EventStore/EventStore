// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public struct IndexReadEventInfoResult {
		public EventInfo[] EventInfos { get; }
		public long NextEventNumber { get; }
		public bool IsEndOfStream => NextEventNumber < 0;

		public IndexReadEventInfoResult(EventInfo[] eventInfos, long nextEventNumber) {
			EventInfos = eventInfos;
			NextEventNumber = nextEventNumber;
		}
	}
}
