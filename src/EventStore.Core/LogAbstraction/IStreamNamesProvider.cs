// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.LogAbstraction {
	// certain abstraction points cant be provided until we have access to the index reader.
	// hopefully when some other pieces have fallen into place we can replace this with a nicer mechanism.
	public interface IStreamNamesProvider<TStreamId> {
		void SetReader(IIndexReader<TStreamId> reader);
		void SetTableIndex(ITableIndex reader);
		ISystemStreamLookup<TStreamId> SystemStreams { get; }
		INameLookup<TStreamId> StreamNames { get; }
		INameLookup<TStreamId> EventTypes { get; }
		INameExistenceFilterInitializer StreamExistenceFilterInitializer { get; }
	}
}
