// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using DotNext;
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

	public async ValueTask<string> LookupName(StreamId streamId, CancellationToken token) {
		if (_metastreams.IsMetaStream(streamId)) {
			streamId = _metastreams.OriginalStreamOf(streamId);
			return await LookupName(streamId, token) is { } name
				? SystemStreams.MetastreamOf(name)
				: null;
		} else {
			return LogV3SystemStreams.TryGetVirtualStreamName(streamId, out var name)
				? name
				: await _wrapped.LookupName(streamId, token);
		}
	}

	public ValueTask<Optional<StreamId>> TryGetLastValue(CancellationToken token) {
		return _wrapped.TryGetLastValue(token);
	}
}
