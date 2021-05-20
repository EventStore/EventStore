using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;

namespace EventStore.Core.LogV3 {
	public class LogV3SystemStreams : ISystemStreamLookup<long> {
		// Virtual streams are streams that exist without requiring a stream record.
		// Reserving a thousand. Doubtful we will need many, but just in case.
		// As an alternative to reserving virtual streams, we could add their stream records as part
		// of the database bootstrapping. This would be odd for the AllStream
		// but more appealing for the settings stream.
		// Reservations are 2 apart to make room for their metastreams.
		public static long FirstRealStream { get; } = 1024;

		// Even streams that dont exist can be normal/meta and user/system
		// e.g. for default ACLs.
		public const long NoUserStream = 0;
		public const long NoUserMetastream = 1;
		public const long NoSystemStream = 2;
		public const long NoSystemMetastream = 3;

		private const long FirstVirtualStream = 4;

		// virtual stream so that we can write metadata to $$$all
		private const long AllStreamNumber = 4;

		// virtual stream so that we can index StreamRecords for looking up stream names
		public const long StreamsCreatedStreamNumber = 6;

		// virtual stream for storing system settings
		private const long SettingsStreamNumber = 8;

		public long AllStream => AllStreamNumber;
		public long SettingsStream => SettingsStreamNumber;

		private readonly IMetastreamLookup<long> _metastreams;
		private readonly IStreamNameLookup<long> _streamNames;

		public LogV3SystemStreams(
			IMetastreamLookup<long> metastreams,
			IStreamNameLookup<long> streamNames) {

			_metastreams = metastreams;
			_streamNames = streamNames;
		}

		public static bool TryGetVirtualStreamName(long streamId, out string name) {
			if (!IsVirtualStream(streamId)) {
				name = null;
				return false;
			}

			name = streamId switch {
				AllStreamNumber => SystemStreams.AllStream,
				SettingsStreamNumber => SystemStreams.SettingsStream,
				StreamsCreatedStreamNumber => SystemStreams.StreamsCreatedStream,
				_ => null,
			};

			return name != null;
		}

		public static bool TryGetVirtualStreamId(string name, out long streamId) {
			switch (name) {
				case SystemStreams.AllStream:
					streamId = AllStreamNumber;
					return true;
				case SystemStreams.StreamsCreatedStream:
					streamId = StreamsCreatedStreamNumber;
					return true;
				case SystemStreams.SettingsStream:
					streamId = SettingsStreamNumber;
					return true;
				default:
					streamId = 0;
					return false;
			}
		}

		// in v2 this checks if the first character is '$'
		// system streams can be created dynamically at runtime
		// e.g. "$persistentsubscription-" + _eventStreamId + "::" + _groupName + "-parked"
		// so we can either allocate a range of numbers (how many?) for them, or look up the name and see if it begins with $.
		// for now do the latter because (1) allocating a range of numbers will probably get fiddly
		// and (2) i expect we will find that at the point we are trying to determine if a stream is a system stream then
		// we will have already looked up its info in the stream index, so this call will become trivial or unnecessary.
		public bool IsSystemStream(long streamId) {
			if (IsVirtualStream(streamId) ||
				_metastreams.IsMetaStream(streamId) ||
				streamId == NoSystemStream)
				return true;

			var streamName = _streamNames.LookupName(streamId);
			return SystemStreams.IsSystemStream(streamName);
		}

		private static bool IsVirtualStream(long streamId) =>
			FirstVirtualStream <= streamId && streamId < FirstRealStream;

		public bool IsMetaStream(long streamId) => _metastreams.IsMetaStream(streamId);

		public long MetaStreamOf(long streamId) => _metastreams.MetaStreamOf(streamId);

		public long OriginalStreamOf(long streamId) => _metastreams.OriginalStreamOf(streamId);
	}
}
