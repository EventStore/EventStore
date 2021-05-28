using LogV3StreamId = System.UInt32;

namespace EventStore.Core.LogAbstraction {
	// Converts from LogV3StreamId to streamId
	// trivial conversion for v3 and non-existent for v2
	public interface IStreamIdConverter<TStreamId> {
		TStreamId ToStreamId(LogV3StreamId x);
	}
}
