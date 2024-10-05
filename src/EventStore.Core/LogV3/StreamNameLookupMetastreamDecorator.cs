// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3;

// Decorates a StreamNameLookup, intercepting Metastream (and VirtualStream) calls
public class StreamNameLookupMetastreamDecorator : INameLookup<StreamId> {
	private readonly INameLookup<StreamId> _wrapped;
	private readonly IMetastreamLookup<StreamId> _metastreams;

	public StreamNameLookupMetastreamDecorator(
		INameLookup<StreamId> wrapped,
		IMetastreamLookup<StreamId> metastreams) {

		_wrapped = wrapped;
		_metastreams = metastreams;
	}

	public bool TryGetName(StreamId streamId, out string name) {
		if (_metastreams.IsMetaStream(streamId)) {
			streamId = _metastreams.OriginalStreamOf(streamId);
			if (!TryGetName(streamId, out name))
				return false;
			name = SystemStreams.MetastreamOf(name);
			return true;
		}

		if (LogV3SystemStreams.TryGetVirtualStreamName(streamId, out name))
			return true;

		return _wrapped.TryGetName(streamId, out name);
	}

	public bool TryGetLastValue(out StreamId last) {
		return _wrapped.TryGetLastValue(out last);
	}
}
