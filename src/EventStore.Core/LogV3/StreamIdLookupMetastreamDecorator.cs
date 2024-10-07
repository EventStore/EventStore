// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3;

// Decorates a StreamIdLookup, intercepting Metastream (and VirtualStream) calls
public class StreamIdLookupMetastreamDecorator : IValueLookup<StreamId> {
	private readonly IValueLookup<StreamId> _wrapped;
	private readonly IMetastreamLookup<StreamId> _metastreams;

	public StreamIdLookupMetastreamDecorator(
		IValueLookup<StreamId> wrapped,
		IMetastreamLookup<StreamId> metastreams) {

		_wrapped = wrapped;
		_metastreams = metastreams;
	}

	public StreamId LookupValue(string streamName) {
		if (string.IsNullOrEmpty(streamName))
			throw new ArgumentNullException(nameof(streamName));

		StreamId streamId;
		if (SystemStreams.IsMetastream(streamName)) {
			streamName = SystemStreams.OriginalStreamOf(streamName);
			streamId = LookupValue(streamName);
			return _metastreams.MetaStreamOf(streamId);
		}

		if (LogV3SystemStreams.TryGetVirtualStreamId(streamName, out streamId))
			return streamId;

		var result = _wrapped.LookupValue(streamName);

		return result == default
			? SystemStreams.IsSystemStream(streamName)
				? LogV3SystemStreams.NoSystemStream
				: LogV3SystemStreams.NoUserStream
			: result;
	}
}
