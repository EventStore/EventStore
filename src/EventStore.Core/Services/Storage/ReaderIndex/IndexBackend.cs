using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using EventStore.Common.Utils;
using EventStore.Core.Caching;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog;
using Serilog;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public interface IIndexBackend {
		TFReaderLease BorrowReader();
		void SetSystemSettings(SystemSettings systemSettings);
		SystemSettings GetSystemSettings();
	}

	public interface IIndexBackend<TStreamId> : IIndexBackend {
		IndexBackend<TStreamId>.EventNumberCached TryGetStreamLastEventNumber(TStreamId streamId);
		IndexBackend<TStreamId>.MetadataCached TryGetStreamMetadata(TStreamId streamId);

		long? UpdateStreamLastEventNumber(int cacheVersion, TStreamId streamId, long? lastEventNumber);
		StreamMetadata UpdateStreamMetadata(int cacheVersion, TStreamId streamId, StreamMetadata metadata);

		long? SetStreamLastEventNumber(TStreamId streamId, long lastEventNumber);
		StreamMetadata SetStreamMetadata(TStreamId streamId, StreamMetadata metadata);
	}

	public class IndexBackend<TStreamId> : IIndexBackend<TStreamId> {
		private readonly ObjectPool<ITransactionFileReader> _readers;
		private readonly IDynamicLRUCache<TStreamId, EventNumberCached> _streamLastEventNumberCache;
		private readonly IDynamicLRUCache<TStreamId, MetadataCached> _streamMetadataCache;
		private readonly ISizer<TStreamId> _streamIdSizer;

		// very rough approximation of memory taken by one item in the stream info cache.
		// used only when cache is configured in terms of "max items" rather than "max bytes".
		public const long StreamInfoCacheUnitSize = 1000;

		private const int LastEventNumberCacheRatio = 60;
		private const int StreamMetadataCacheRatio = 100 - LastEventNumberCacheRatio;

		private const string LastEventNumberCacheName = "LastEventNumber";
		private const string StreamMetadataCacheName = "StreamMetadata";

		private SystemSettings _systemSettings;

		public IndexBackend(ObjectPool<ITransactionFileReader> readers, ISizer<TStreamId> streamIdSizer, ICacheSettings streamInfoCacheSettings) {
			Ensure.NotNull(readers, nameof(readers));
			Ensure.NotNull(streamIdSizer, nameof(streamIdSizer));

			_readers = readers;
			_streamIdSizer = streamIdSizer;

			var lastEventNumberCacheCapacity =
				streamInfoCacheSettings.InitialMaxMemAllocation * LastEventNumberCacheRatio / 100;
			var streamMetadataCacheCapacity =
				streamInfoCacheSettings.InitialMaxMemAllocation * StreamMetadataCacheRatio / 100;

			_streamLastEventNumberCache = new DynamicLRUCache<TStreamId, EventNumberCached>(lastEventNumberCacheCapacity, CalcLastEventNumberItemSize);
			_streamMetadataCache = new DynamicLRUCache<TStreamId, MetadataCached>(streamMetadataCacheCapacity, CalcMetadataItemSize);

			Log.Information("{name} cache capacity set to ~{numEntries:N0} bytes.", LastEventNumberCacheName, lastEventNumberCacheCapacity);
			Log.Information("{name} cache capacity set to ~{numEntries:N0} bytes.", StreamMetadataCacheName, streamMetadataCacheCapacity);

			streamInfoCacheSettings.GetMemoryUsage = () => _streamLastEventNumberCache.Size + _streamMetadataCache.Size;
			streamInfoCacheSettings.UpdateMaxMemoryAllocation = UpdateMaxMemoryAllocation;
		}

		private int CalcLastEventNumberItemSize(TStreamId streamId, EventNumberCached eventNumberCached) =>
			_streamIdSizer.GetSizeInBytes(streamId) + EventNumberCached.Size + EventNumberCached.DictionaryEntryOverhead;

		private int CalcMetadataItemSize(TStreamId streamId, MetadataCached metadataCached) =>
			_streamIdSizer.GetSizeInBytes(streamId) + metadataCached.Size + MetadataCached.DictionaryEntryOverhead;

		private void UpdateMaxMemoryAllocation(long allocatedMem) {
			UpdateCapacity(_streamLastEventNumberCache, LastEventNumberCacheName,
				allocatedMem * LastEventNumberCacheRatio / 100);
			UpdateCapacity(_streamMetadataCache, StreamMetadataCacheName,
				allocatedMem * StreamMetadataCacheRatio / 100);
		}

		private delegate void LogAction(string msg, params object[] args);
		private static void UpdateCapacity(IDynamicLRUCache cache, string cacheName, long newCapacity) {
			var prevCapacity = cache.Capacity;

			var sw = Stopwatch.StartNew();
			cache.Resize(newCapacity, out var removedCount, out var removedSize);
			sw.Stop();

			LogAction logAction = removedCount > 0 ? Log.Information : Log.Debug;
			logAction("{name} cache resized from ~{prevCapacity:N0} bytes to ~{newCapacity:N0} bytes in {elapsed}. "
			          + "Removed {numEntries:N0} entries amounting to ~{numBytes:N0} bytes.",
				cacheName, prevCapacity, newCapacity, sw.Elapsed, removedCount, removedSize);
		}

		public TFReaderLease BorrowReader() {
			return new TFReaderLease(_readers);
		}

		public EventNumberCached TryGetStreamLastEventNumber(TStreamId streamId) {
			EventNumberCached cacheInfo;
			_streamLastEventNumberCache.TryGet(streamId, out cacheInfo);
			return cacheInfo;
		}

		public MetadataCached TryGetStreamMetadata(TStreamId streamId) {
			MetadataCached cacheInfo;
			_streamMetadataCache.TryGet(streamId, out cacheInfo);
			return cacheInfo;
		}

		public long? UpdateStreamLastEventNumber(int cacheVersion, TStreamId streamId, long? lastEventNumber) {
			var res = _streamLastEventNumberCache.Put(
				streamId,
				new KeyValuePair<int, long?>(cacheVersion, lastEventNumber),
				(key, d) => d.Key == 0 ? new EventNumberCached(1, d.Value) : new EventNumberCached(1, null),
				(key, old, d) => old.Version == d.Key
					? new EventNumberCached(d.Key + 1, d.Value ?? old.LastEventNumber)
					: old);
			return res.LastEventNumber;
		}

		public StreamMetadata UpdateStreamMetadata(int cacheVersion, TStreamId streamId, StreamMetadata metadata) {
			var res = _streamMetadataCache.Put(
				streamId,
				new KeyValuePair<int, StreamMetadata>(cacheVersion, metadata),
				(key, d) => d.Key == 0 ? new MetadataCached(1, d.Value) : new MetadataCached(1, null),
				(key, old, d) => old.Version == d.Key ? new MetadataCached(d.Key + 1, d.Value ?? old.Metadata) : old);
			return res.Metadata;
		}

		long? IIndexBackend<TStreamId>.SetStreamLastEventNumber(TStreamId streamId, long lastEventNumber) {
			var res = _streamLastEventNumberCache.Put(streamId,
				lastEventNumber,
				(key, lastEvNum) => new EventNumberCached(1, lastEvNum),
				(key, old, lastEvNum) => new EventNumberCached(old.Version + 1, lastEvNum));
			return res.LastEventNumber;
		}

		StreamMetadata IIndexBackend<TStreamId>.SetStreamMetadata(TStreamId streamId, StreamMetadata metadata) {
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

			public static int Size => Unsafe.SizeOf<EventNumberCached>();
			public const int DictionaryEntryOverhead = 20;
		}

		public struct MetadataCached {
			public readonly int Version;
			public readonly StreamMetadata Metadata;

			public MetadataCached(int version, StreamMetadata metadata) {
				Version = version;
				Metadata = metadata;
			}

			public int Size => Unsafe.SizeOf<MetadataCached>() + (Metadata?.Size ?? 0);
			public const int DictionaryEntryOverhead = 20;
		}
	}
}
