using EventStore.Common.Utils;
using EventStore.Core.Index.Hashes;
using EventStore.Core.LogV2;
using EventStore.Core.LogV3;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.LogAbstraction {
	public class LogFormatAbstractor {
		public static LogFormatAbstractor<string> V2 { get; }
		public static LogFormatAbstractor<string> V3 { get; }

		static LogFormatAbstractor() {
			var streamNameIndex = new LogV2StreamNameIndex();
			V2 = new LogFormatAbstractor<string>(
				new XXHashUnsafe(),
				new Murmur3AUnsafe(),
				streamNameIndex,
				streamNameIndex,
				new StreamNameLookupSingletonFactory<string>(streamNameIndex),
				new LogV2SystemStreams(),
				new LogV2StreamIdValidator(),
				string.Empty,
				new LogV2Sizer(),
				new LogV2RecordFactory());

			// just like v2 for now except epochs
			V3 = new LogFormatAbstractor<string>(
				new XXHashUnsafe(),
				new Murmur3AUnsafe(),
				streamNameIndex,
				streamNameIndex,
				new StreamNameLookupSingletonFactory<string>(streamNameIndex),
				new LogV2SystemStreams(),
				new LogV2StreamIdValidator(),
				string.Empty,
				new LogV2Sizer(),
				new LogV3RecordFactory(V2.RecordFactory));
		}
	}

	public class LogFormatAbstractor<TStreamId> : LogFormatAbstractor {
		public LogFormatAbstractor(
			IHasher<TStreamId> lowHasher,
			IHasher<TStreamId> highHasher,
			IStreamNameIndex<TStreamId> streamNameIndex,
			IStreamIdLookup<TStreamId> streamIds,
			IStreamNameLookupFactory<TStreamId> streamNamesFactory,
			ISystemStreamLookup<TStreamId> systemStreams,
			IValidator<TStreamId> streamIdValidator,
			TStreamId emptyStreamId,
			ISizer<TStreamId> streamIdSizer,
			IRecordFactory<TStreamId> recordFactory) {

			LowHasher = lowHasher;
			HighHasher = highHasher;
			StreamNameIndex = streamNameIndex;
			StreamIds = streamIds;
			StreamNamesFactory = streamNamesFactory;
			SystemStreams = systemStreams;
			StreamIdValidator = streamIdValidator;
			EmptyStreamId = emptyStreamId;
			StreamIdSizer = streamIdSizer;
			RecordFactory = recordFactory;
		}

		public IHasher<TStreamId> LowHasher { get; }
		public IHasher<TStreamId> HighHasher { get; }
		public IStreamNameIndex<TStreamId> StreamNameIndex { get; }
		public IStreamIdLookup<TStreamId> StreamIds { get; }
		public IStreamNameLookupFactory<TStreamId> StreamNamesFactory { get; }
		public ISystemStreamLookup<TStreamId> SystemStreams { get; }
		public IValidator<TStreamId> StreamIdValidator { get; }
		public TStreamId EmptyStreamId { get; }
		public ISizer<TStreamId> StreamIdSizer { get; }
		public IRecordFactory<TStreamId> RecordFactory { get; }
	}
}
