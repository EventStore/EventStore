using System;
using EventStore.Common.Utils;
using EventStore.Core.DataStructures;
using EventStore.Core.Index.Hashes;
using EventStore.Core.LogV2;
using EventStore.Core.LogV3;
using EventStore.Core.LogV3.FASTER;
using EventStore.Core.Settings;
using LogV3StreamId = System.UInt32;

namespace EventStore.Core.LogAbstraction {
	public class LogFormatAbstractorOptions {
		public string IndexDirectory { get; init; }
		public bool InMemory { get; init; }
		public int InitialReaderCount { get; init; } = ESConsts.PTableInitialReaderCount;
		public int LruSize { get; init; } = 10_000;
		public int MaxReaderCount { get; init; } = 100;
		public bool SkipIndexScanOnReads { get; init; }
	}

	public interface ILogFormatAbstractorFactory<TStreamId> {
		LogFormatAbstractor<TStreamId> Create(LogFormatAbstractorOptions options);
	}

	public class LogV2FormatAbstractorFactory : ILogFormatAbstractorFactory<string> {
		public LogFormatAbstractor<string> Create(LogFormatAbstractorOptions options) {
			var streamNameIndex = new LogV2StreamNameIndex();
			return new LogFormatAbstractor<string>(
				lowHasher: new XXHashUnsafe(),
				highHasher: new Murmur3AUnsafe(),
				streamNameIndex: streamNameIndex,
				streamNameIndexConfirmer: streamNameIndex,
				streamIds: streamNameIndex,
				metastreams: new LogV2SystemStreams(),
				streamNamesProvider: new SingletonStreamNamesProvider<string>(new LogV2SystemStreams(), streamNameIndex),
				streamIdConverter: new LogV2StreamIdConverter(),
				streamIdValidator: new LogV2StreamIdValidator(),
				emptyStreamId: string.Empty,
				streamIdSizer: new LogV2Sizer(),
				recordFactory: new LogV2RecordFactory(),
				supportsExplicitTransactions: true,
				skipIndexScanOnReads: options.SkipIndexScanOnReads);
		}
	}

	public class LogV3FormatAbstractorFactory : ILogFormatAbstractorFactory<LogV3StreamId> {
		public LogFormatAbstractor<LogV3StreamId> Create(LogFormatAbstractorOptions options) {
			var streamNameIndexPersistence = GenStreamNameIndexPersistence(options);
			var streamNameIndex = GenStreamNameIndex(options, streamNameIndexPersistence);
			var metastreams = new LogV3Metastreams();

			ILRUCache<LogV3StreamId, string> streamNameLookupLru = null;
			INameIndexConfirmer<LogV3StreamId> streamNameIndexConfirmer = streamNameIndex;

			var useStreamNameLookupLru = options.LruSize > 0;
			if (useStreamNameLookupLru) {
				streamNameLookupLru = new LRUCache<LogV3StreamId, string>(options.LruSize);
				streamNameIndexConfirmer = new NameConfirmerLruDecorator<LogV3StreamId>(streamNameLookupLru, streamNameIndexConfirmer);
			}

			var abstractor = new LogFormatAbstractor<LogV3StreamId>(
				lowHasher: new IdentityLowHasher(),
				highHasher: new IdentityHighHasher(),
				streamNameIndex: new StreamNameIndexMetastreamDecorator(streamNameIndex, metastreams),
				streamNameIndexConfirmer: streamNameIndexConfirmer,
				streamIds: new StreamIdLookupMetastreamDecorator(streamNameIndexPersistence, metastreams),
				metastreams: metastreams,
				streamNamesProvider: new AdHocStreamNamesProvider<LogV3StreamId>(indexReader => {
					INameLookup<LogV3StreamId> streamNames = new StreamIdToNameFromStandardIndex(indexReader);

					if (useStreamNameLookupLru) {
						streamNames = new NameLookupLruDecorator<LogV3StreamId>(
							lru: streamNameLookupLru,
							wrappedLookup: streamNames);
					}

					var systemStreams = new LogV3SystemStreams(metastreams, streamNames);
					streamNames = new StreamNameLookupMetastreamDecorator(streamNames, metastreams);
					return (systemStreams, streamNames);
				}),
				streamIdConverter: new LogV3StreamIdConverter(),
				streamIdValidator: new LogV3StreamIdValidator(),
				emptyStreamId: 0,
				streamIdSizer: new LogV3Sizer(),
				recordFactory: new LogV3RecordFactory(),
				supportsExplicitTransactions: false,
				skipIndexScanOnReads: true);
			return abstractor;
		}

		static NameIndex GenStreamNameIndex(LogFormatAbstractorOptions options, INameIndexPersistence<LogV3StreamId> persistence) {
			var streamNameIndex = new NameIndex(
				indexName: "StreamNameIndex",
				firstValue: LogV3SystemStreams.FirstRealStream,
				valueInterval: LogV3SystemStreams.StreamInterval,
				persistence: persistence);
			return streamNameIndex;
		}

		static INameIndexPersistence<LogV3StreamId> GenStreamNameIndexPersistence(LogFormatAbstractorOptions options) {
			if (options.InMemory) {
				return new NameIndexInMemoryPersistence();
			}

			var persistence = new FASTERNameIndexPersistence(
				indexName: "StreamNameIndexPersistence",
				logDir: $"{options.IndexDirectory}/stream-name-index",
				firstValue: LogV3SystemStreams.FirstRealStream,
				valueInterval: LogV3SystemStreams.StreamInterval,
				initialReaderCount: options.InitialReaderCount,
				maxReaderCount: options.MaxReaderCount,
				enableReadCache: false,
				checkpointInterval: TimeSpan.FromSeconds(60));
			return persistence;
		}
	}

	public class LogFormatAbstractor<TStreamId> : IDisposable {
		public LogFormatAbstractor(
			IHasher<TStreamId> lowHasher,
			IHasher<TStreamId> highHasher,
			INameIndex<TStreamId> streamNameIndex,
			INameIndexConfirmer<TStreamId> streamNameIndexConfirmer,
			IValueLookup<TStreamId> streamIds,
			IMetastreamLookup<TStreamId> metastreams,
			IStreamNamesProvider<TStreamId> streamNamesProvider,
			IStreamIdConverter<TStreamId> streamIdConverter,
			IValidator<TStreamId> streamIdValidator,
			TStreamId emptyStreamId,
			ISizer<TStreamId> streamIdSizer,
			IRecordFactory<TStreamId> recordFactory,
			bool supportsExplicitTransactions,
			bool skipIndexScanOnReads) {

			LowHasher = lowHasher;
			HighHasher = highHasher;
			StreamNameIndex = streamNameIndex;
			StreamNameIndexConfirmer = streamNameIndexConfirmer;
			StreamIds = streamIds;
			Metastreams = metastreams;
			StreamNamesProvider = streamNamesProvider;
			StreamIdConverter = streamIdConverter;
			StreamIdValidator = streamIdValidator;
			EmptyStreamId = emptyStreamId;
			StreamIdSizer = streamIdSizer;
			RecordFactory = recordFactory;
			SupportsExplicitTransactions = supportsExplicitTransactions;
			SkipIndexScanOnReads = skipIndexScanOnReads;
		}

		public void Dispose() {
			StreamNameIndexConfirmer?.Dispose();
		}

		public IHasher<TStreamId> LowHasher { get; }
		public IHasher<TStreamId> HighHasher { get; }
		public INameIndex<TStreamId> StreamNameIndex { get; }
		public INameIndexConfirmer<TStreamId> StreamNameIndexConfirmer { get; }
		public IValueLookup<TStreamId> StreamIds { get; }
		public IMetastreamLookup<TStreamId> Metastreams { get; }
		public IStreamNamesProvider<TStreamId> StreamNamesProvider { get; }
		public IStreamIdConverter<TStreamId> StreamIdConverter { get; }
		public IValidator<TStreamId> StreamIdValidator { get; }
		public TStreamId EmptyStreamId { get; }
		public ISizer<TStreamId> StreamIdSizer { get; }
		public IRecordFactory<TStreamId> RecordFactory { get; }

		public INameLookup<TStreamId> StreamNames => StreamNamesProvider.StreamNames;
		public ISystemStreamLookup<TStreamId> SystemStreams => StreamNamesProvider.SystemStreams;
		public bool SupportsExplicitTransactions { get; }
		public bool SkipIndexScanOnReads { get; }
	}
}
