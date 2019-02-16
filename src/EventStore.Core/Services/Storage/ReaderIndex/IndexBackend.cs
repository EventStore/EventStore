using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.TransactionLog;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public interface IIndexBackend {
		TFReaderLease BorrowReader();

		IndexBackend.EventNumberCached TryGetStreamLastEventNumber(string streamId);
		IndexBackend.MetadataCached TryGetStreamMetadata(string streamId);

		long? UpdateStreamLastEventNumber(int cacheVersion, string streamId, long? lastEventNumber);
		StreamMetadata UpdateStreamMetadata(int cacheVersion, string streamId, StreamMetadata metadata);

		long? SetStreamLastEventNumber(string streamId, long lastEventNumber);
		StreamMetadata SetStreamMetadata(string streamId, StreamMetadata metadata);

		void SetSystemSettings(SystemSettings systemSettings);
		SystemSettings GetSystemSettings();
	}

	public class IndexBackend : IIndexBackend {
		private readonly ObjectPool<ITransactionFileReader> _readers;
		private readonly ILRUCache<string, EventNumberCached> _streamLastEventNumberCache;
		private readonly ILRUCache<string, MetadataCached> _streamMetadataCache;
		private SystemSettings _systemSettings;

		public IndexBackend(ObjectPool<ITransactionFileReader> readers,
			int lastEventNumberCacheCapacity,
			int metadataCacheCapacity) {
			Ensure.NotNull(readers, "readers");

			_readers = readers;
			_streamLastEventNumberCache = new LRUCache<string, EventNumberCached>(lastEventNumberCacheCapacity);
			_streamMetadataCache = new LRUCache<string, MetadataCached>(metadataCacheCapacity);
		}

		public TFReaderLease BorrowReader() {
			return new TFReaderLease(_readers);
		}

		public EventNumberCached TryGetStreamLastEventNumber(string streamId) {
			EventNumberCached cacheInfo;
			_streamLastEventNumberCache.TryGet(streamId, out cacheInfo);
			return cacheInfo;
		}

		public MetadataCached TryGetStreamMetadata(string streamId) {
			MetadataCached cacheInfo;
			_streamMetadataCache.TryGet(streamId, out cacheInfo);
			return cacheInfo;
		}

		public long? UpdateStreamLastEventNumber(int cacheVersion, string streamId, long? lastEventNumber) {
			var res = _streamLastEventNumberCache.Put(
				streamId,
				new KeyValuePair<int, long?>(cacheVersion, lastEventNumber),
				(key, d) => d.Key == 0 ? new EventNumberCached(1, d.Value) : new EventNumberCached(1, null),
				(key, old, d) => old.Version == d.Key
					? new EventNumberCached(d.Key + 1, d.Value ?? old.LastEventNumber)
					: old);
			return res.LastEventNumber;
		}

		public StreamMetadata UpdateStreamMetadata(int cacheVersion, string streamId, StreamMetadata metadata) {
			var res = _streamMetadataCache.Put(
				streamId,
				new KeyValuePair<int, StreamMetadata>(cacheVersion, metadata),
				(key, d) => d.Key == 0 ? new MetadataCached(1, d.Value) : new MetadataCached(1, null),
				(key, old, d) => old.Version == d.Key ? new MetadataCached(d.Key + 1, d.Value ?? old.Metadata) : old);
			return res.Metadata;
		}

		long? IIndexBackend.SetStreamLastEventNumber(string streamId, long lastEventNumber) {
			var res = _streamLastEventNumberCache.Put(streamId,
				lastEventNumber,
				(key, lastEvNum) => new EventNumberCached(1, lastEvNum),
				(key, old, lastEvNum) => new EventNumberCached(old.Version + 1, lastEvNum));
			return res.LastEventNumber;
		}

		StreamMetadata IIndexBackend.SetStreamMetadata(string streamId, StreamMetadata metadata) {
			var res = _streamMetadataCache.Put(streamId,
				metadata,
				(key, meta) => new MetadataCached(1, meta),
				(key, old, meta) => new MetadataCached(old.Version + 1, meta));
			return res.Metadata;
		}

		public void SetSystemSettings(SystemSettings systemSettings) {
			_systemSettings = systemSettings;
		}

		public SystemSettings GetSystemSettings() {
			return _systemSettings;
		}

		public struct EventNumberCached {
			public readonly int Version;
			public readonly long? LastEventNumber;

			public EventNumberCached(int version, long? lastEventNumber) {
				Version = version;
				LastEventNumber = lastEventNumber;
			}
		}

		public struct MetadataCached {
			public readonly int Version;
			public readonly StreamMetadata Metadata;

			public MetadataCached(int version, StreamMetadata metadata) {
				Version = version;
				Metadata = metadata;
			}
		}
	}
}
