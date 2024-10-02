// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public class ReadIndexStats {
		public readonly long CachedRecordReads;
		public readonly long NotCachedRecordReads;
		public readonly long CachedStreamInfoReads;
		public readonly long NotCachedStreamInfoReads;
		public readonly long HashCollisions;
		public readonly long CachedTransInfoReads;
		public readonly long NotCachedTransInfoReads;

		public ReadIndexStats(long cachedRecordReads, long notCachedRecordReads,
			long cachedStreamInfoReads, long notCachedStreamInfoReads,
			long hashCollisions,
			long cachedTransInfoReads, long notCachedTransInfoReads) {
			CachedRecordReads = cachedRecordReads;
			NotCachedRecordReads = notCachedRecordReads;
			CachedStreamInfoReads = cachedStreamInfoReads;
			NotCachedStreamInfoReads = notCachedStreamInfoReads;
			HashCollisions = hashCollisions;
			CachedTransInfoReads = cachedTransInfoReads;
			NotCachedTransInfoReads = notCachedTransInfoReads;
		}
	}
}
