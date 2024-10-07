// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;

namespace EventStore.Projections.Core.Services.Processing.SingleStream;

public class StreamEventFilter : EventFilter {
	private readonly string _streamId;

	public StreamEventFilter(string streamId, bool allEvents, HashSet<string> events)
		: base(allEvents, false, events) {
		_streamId = streamId;
	}

	protected override bool DeletedNotificationPasses(string positionStreamId) {
		return positionStreamId == _streamId;
	}

	public override bool PassesSource(bool resolvedFromLinkTo, string positionStreamId, string eventType) {
		return positionStreamId == _streamId;
	}

	public override string GetCategory(string positionStreamId) {
		return null;
	}

	public override string ToString() {
		return string.Format("StreamId: {0}", _streamId);
	}
}
