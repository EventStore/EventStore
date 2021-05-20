using EventStore.Common.Utils;
using EventStore.Core.Index.Hashes;
using EventStore.Core.LogV2;
using EventStore.Core.LogV3;

namespace EventStore.Core.LogAbstraction {
	public class LogFormatAbstractor {
		public static LogFormatAbstractor<string> V2 { get; }
		public static LogFormatAbstractor<long> V3 { get; }

		static LogFormatAbstractor() {
			var streamNameIndex = new LogV2StreamNameIndex();
			V2 = new LogFormatAbstractor<string>(
				new XXHashUnsafe(),
				new Murmur3AUnsafe(),
				streamNameIndex,
				streamNameIndex,
				new SingletonStreamNamesProvider<string>(new LogV2SystemStreams(), streamNameIndex),
				new LogV2StreamIdValidator(),
				emptyStreamId: string.Empty,
				new LogV2Sizer(),
				new LogV2RecordFactory(),
				supportsExplicitTransactions: true);

			var logV3StreamNameIndex = new InMemoryStreamNameIndex();
			var metastreams = new LogV3Metastreams();
			V3 = new LogFormatAbstractor<long>(
				lowHasher: new IdentityLowHasher(),
				highHasher: new IdentityHighHasher(),
				streamNameIndex: new StreamNameIndexMetastreamDecorator(logV3StreamNameIndex, metastreams),
				streamIds: new StreamIdLookupMetastreamDecorator(logV3StreamNameIndex, metastreams),
				streamNamesProvider: new AdHocStreamNamesProvider<long>(indexReader => {
					// todo: IStreamNameLookup<long> streamNames = new StreamIdToNameFromStandardIndex(indexReader);
					IStreamNameLookup<long> streamNames = logV3StreamNameIndex;
					var systemStreams = new LogV3SystemStreams(metastreams, streamNames);
					streamNames = new StreamNameLookupMetastreamDecorator(streamNames, metastreams);
					return (systemStreams, streamNames);
				}),
				streamIdValidator: new LogV3StreamIdValidator(),
				emptyStreamId: 0,
				streamIdSizer: new LogV3Sizer(),
				recordFactory: new LogV3RecordFactory(),
				supportsExplicitTransactions: false);
		}
	}

	public class LogFormatAbstractor<TStreamId> : LogFormatAbstractor {
		public LogFormatAbstractor(
			IHasher<TStreamId> lowHasher,
			IHasher<TStreamId> highHasher,
			IStreamNameIndex<TStreamId> streamNameIndex,
			IStreamIdLookup<TStreamId> streamIds,
			IStreamNamesProvider<TStreamId> streamNamesProvider,
			IValidator<TStreamId> streamIdValidator,
			TStreamId emptyStreamId,
			ISizer<TStreamId> streamIdSizer,
			IRecordFactory<TStreamId> recordFactory,
			bool supportsExplicitTransactions) {

			LowHasher = lowHasher;
			HighHasher = highHasher;
			StreamNameIndex = streamNameIndex;
			StreamIds = streamIds;
			StreamNamesProvider = streamNamesProvider;
			StreamIdValidator = streamIdValidator;
			EmptyStreamId = emptyStreamId;
			StreamIdSizer = streamIdSizer;
			RecordFactory = recordFactory;
			SupportsExplicitTransactions = supportsExplicitTransactions;
		}

		public IHasher<TStreamId> LowHasher { get; }
		public IHasher<TStreamId> HighHasher { get; }
		public IStreamNameIndex<TStreamId> StreamNameIndex { get; }
		public IStreamIdLookup<TStreamId> StreamIds { get; }
		public IStreamNamesProvider<TStreamId> StreamNamesProvider { get; }
		public IValidator<TStreamId> StreamIdValidator { get; }
		public TStreamId EmptyStreamId { get; }
		public ISizer<TStreamId> StreamIdSizer { get; }
		public IRecordFactory<TStreamId> RecordFactory { get; }

		public IStreamNameLookup<TStreamId> StreamNames => StreamNamesProvider.StreamNames;
		public ISystemStreamLookup<TStreamId> SystemStreams => StreamNamesProvider.SystemStreams;
		public bool SupportsExplicitTransactions { get; }
	}
}
