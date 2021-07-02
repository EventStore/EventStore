using System.Collections.Generic;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3 {
	public class LogV3SystemStreams : ISystemStreamLookup<StreamId> {
		// Virtual streams are streams that exist without requiring a stream record.
		// Reserving a thousand. Doubtful we will need many, but just in case.
		// As an alternative to reserving virtual streams, we could add their stream records as part
		// of the database bootstrapping. This would be odd for the AllStream
		// but more appealing for the settings stream.
		// Reservations are 2 apart to make room for their metastreams.
		public static StreamId FirstRealStream { get; } = 1024;
		// difference between each stream record (2 because of metastreams)
		public static StreamId StreamInterval { get; } = 2;

		// Even streams that dont exist can be normal/meta and user/system
		// e.g. for default ACLs.
		public const StreamId NoUserStream = 0;
		public const StreamId NoUserMetastream = 1;
		public const StreamId NoSystemStream = 2;
		public const StreamId NoSystemMetastream = 3;

		public const StreamId FirstVirtualStream = 4;

		// virtual stream so that we can write metadata to $$$all
		private const StreamId AllStreamNumber = 4;

		// virtual stream so that we can index StreamRecords for looking up stream names
		public const StreamId StreamsCreatedStreamNumber = 6;

		// virtual stream for storing system settings
		private const StreamId SettingsStreamNumber = 8;

		public StreamId AllStream => AllStreamNumber;
		public StreamId SettingsStream => SettingsStreamNumber;

		private readonly IMetastreamLookup<StreamId> _metastreams;
		private readonly INameLookup<StreamId> _streamNames;

		public LogV3SystemStreams(
			IMetastreamLookup<StreamId> metastreams,
			INameLookup<StreamId> streamNames) {

			_metastreams = metastreams;
			_streamNames = streamNames;
		}

		public static bool TryGetVirtualStreamName(StreamId streamId, out string name) {
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

		public static bool TryGetVirtualStreamId(string name, out StreamId streamId) {
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

		//qq need?
		public static IEnumerable<string> EnumerateVirtualStreamNames() {
			yield return SystemStreams.AllStream;
			yield return SystemStreams.StreamsCreatedStream;
			yield return SystemStreams.SettingsStream;
		}

		//qq need?
		public static IEnumerable<StreamId> EnumerateVirtualStreamIds() {
			yield return AllStreamNumber;
			yield return StreamsCreatedStreamNumber;
			yield return SettingsStreamNumber;
		}

		// in v2 this checks if the first character is '$'
		// system streams can be created dynamically at runtime
		// e.g. "$persistentsubscription-" + _eventStreamId + "::" + _groupName + "-parked"
		// so we can either allocate a range of numbers (how many?) for them, or look up the name and see if it begins with $.
		// for now do the latter because (1) allocating a range of numbers will probably get fiddly
		// and (2) i expect we will find that at the point we are trying to determine if a stream is a system stream then
		// we will have already looked up its info in the stream index, so this call will become trivial or unnecessary.
		public bool IsSystemStream(StreamId streamId) {
			if (IsVirtualStream(streamId) ||
				_metastreams.IsMetaStream(streamId) ||
				streamId == NoSystemStream)
				return true;

			if (streamId == NoUserStream)
				return false;

			var streamName = _streamNames.LookupName(streamId);
			return SystemStreams.IsSystemStream(streamName);
		}

		private static bool IsVirtualStream(StreamId streamId) =>
			FirstVirtualStream <= streamId && streamId < FirstRealStream;

		public bool IsMetaStream(StreamId streamId) => _metastreams.IsMetaStream(streamId);

		public StreamId MetaStreamOf(StreamId streamId) => _metastreams.MetaStreamOf(streamId);

		public StreamId OriginalStreamOf(StreamId streamId) => _metastreams.OriginalStreamOf(streamId);
	}
}
