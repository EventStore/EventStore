using System;

namespace EventStore.Core.LogAbstraction {
	public interface IStreamIdConverter<TStreamId> {
		TStreamId ToStreamId(ReadOnlySpan<byte> bytes);
		TStreamId ToStreamId(ReadOnlyMemory<byte> bytes);
	}
}
