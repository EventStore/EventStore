using System;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Index.Hashes;
using EventStore.Core.LogV2;
using EventStore.Core.LogV3;
using EventStore.Core.LogV3.FASTER;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog;
using LogV3StreamId = System.UInt32;

namespace EventStore.Core.LogAbstraction {
	public class LogFormatAbstractorOptions {
		public string IndexDirectory { get; init; }
		public bool InMemory { get; init; }
		public int InitialReaderCount { get; init; } = ESConsts.PTableInitialReaderCount;
		public int MaxReaderCount { get; init; } = 100;

	}

	public interface ILogFormatAbstractorFactory<TStreamId> {
		LogFormatAbstractor<TStreamId> Create(LogFormatAbstractorOptions options);
	}

	public class LogV2FormatAbstractorFactory : ILogFormatAbstractorFactory<string> {
		public LogFormatAbstractor<string> Create(LogFormatAbstractorOptions _) {
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
				partitionManagerFactory: (r, w) => new LogV2PartitionManager());
		}
	}

	public class LogV3FormatAbstractorFactory : ILogFormatAbstractorFactory<LogV3StreamId> {
		public LogFormatAbstractor<LogV3StreamId> Create(LogFormatAbstractorOptions options) {
			var streamNameIndexPersistence = GenStreamNameIndexPersistence(options);
			var streamNameIndex = GenStreamNameIndex(options, streamNameIndexPersistence);
			var metastreams = new LogV3Metastreams();
			var recordFactory = new LogV3RecordFactory();

			var abstractor = new LogFormatAbstractor<LogV3StreamId>(
				lowHasher: new IdentityLowHasher(),
				highHasher: new IdentityHighHasher(),
				streamNameIndex: new StreamNameIndexMetastreamDecorator(streamNameIndex, metastreams),
				streamNameIndexConfirmer: streamNameIndex,
				streamIds: new StreamIdLookupMetastreamDecorator(streamNameIndexPersistence, metastreams),
				metastreams: metastreams,
				streamNamesProvider: new AdHocStreamNamesProvider<LogV3StreamId>(indexReader => {
					INameLookup<LogV3StreamId> streamNames = new StreamIdToNameFromStandardIndex(indexReader);
					var systemStreams = new LogV3SystemStreams(metastreams, streamNames);
					streamNames = new StreamNameLookupMetastreamDecorator(streamNames, metastreams);
					return (systemStreams, streamNames);
				}),
				streamIdConverter: new LogV3StreamIdConverter(),
				streamIdValidator: new LogV3StreamIdValidator(),
				emptyStreamId: 0,
				streamIdSizer: new LogV3Sizer(),
				recordFactory: recordFactory,
				supportsExplicitTransactions: false,
				partitionManagerFactory: (r,w) => new PartitionManager(r, w, recordFactory));
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
				enableReadCache: true,
				checkpointInterval: TimeSpan.FromSeconds(60));
			return persistence;
		}
	}

	public class LogFormatAbstractor<TStreamId> : IDisposable {
		private readonly Func<ITransactionFileReader,ITransactionFileWriter,IPartitionManager> _partitionManagerFactory;

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
			Func<ITransactionFileReader,ITransactionFileWriter,IPartitionManager> partitionManagerFactory) {
			
			_partitionManagerFactory = partitionManagerFactory;

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
		
		public IPartitionManager CreatePartitionManager(ITransactionFileReader reader, ITransactionFileWriter writer) {
			return _partitionManagerFactory(reader, writer);
		}
	}
}
