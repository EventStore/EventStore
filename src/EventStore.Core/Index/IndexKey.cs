// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Index {
	public struct IndexKey<TStreamId> {
		public TStreamId StreamId;
		public long Version;
		public long Position;
		public ulong Hash;

		public IndexKey(TStreamId streamId, long version, long position) : this(streamId, version, position, 0) {
		}

		public IndexKey(TStreamId streamId, long version, long position, ulong hash) {
			StreamId = streamId;
			Version = version;
			Position = position;

			Hash = hash;
		}
	}
}
