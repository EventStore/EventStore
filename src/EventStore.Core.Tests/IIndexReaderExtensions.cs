// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Tests;

public static class IIndexReaderExtensions {
	public static ValueTask<IndexReadEventResult> ReadEvent(this IIndexReader<string> index, string streamName, long eventNumber, CancellationToken token) =>
		index.ReadEvent(streamName, streamName, eventNumber, token);

	public static ValueTask<IndexReadStreamResult> ReadStreamEventsBackward(this IIndexReader<string> index, string streamName, long fromEventNumber, int maxCount, CancellationToken token) =>
		index.ReadStreamEventsBackward(streamName, streamName, fromEventNumber, maxCount, token);

	public static ValueTask<IndexReadStreamResult> ReadStreamEventsForward(this IIndexReader<string> index, string streamName, long fromEventNumber, int maxCount, CancellationToken token) =>
		index.ReadStreamEventsForward(streamName, streamName, fromEventNumber, maxCount, token);
}
