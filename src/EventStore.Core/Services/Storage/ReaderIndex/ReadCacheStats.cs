// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public class ReadCacheStats {
		public readonly long Size;
		public readonly int Count;
		public readonly long HitCount;
		public readonly long MissCount;

		public ReadCacheStats(long size, int count, long hitCount, long missCount) {
			Size = size;
			Count = count;
			HitCount = hitCount;
			MissCount = missCount;
		}
	}
}
