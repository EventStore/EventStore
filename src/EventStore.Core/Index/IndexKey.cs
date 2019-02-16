namespace EventStore.Core.Index {
	public struct IndexKey {
		public string StreamId;
		public long Version;
		public long Position;
		public ulong Hash;

		public IndexKey(string streamId, long version, long position) : this(streamId, version, position, 0) {
		}

		public IndexKey(string streamId, long version, long position, ulong hash) {
			StreamId = streamId;
			Version = version;
			Position = position;

			Hash = hash;
		}
	}
}
