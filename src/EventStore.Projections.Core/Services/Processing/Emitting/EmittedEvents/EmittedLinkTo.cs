// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Globalization;
using EventStore.Projections.Core.Services.Processing.Checkpointing;

namespace EventStore.Projections.Core.Services.Processing.Emitting.EmittedEvents;

public class EmittedLinkTo : EmittedEvent {
	private readonly string _targetStreamId;
	private long? _eventNumber;

	public EmittedLinkTo(
		string streamId, Guid eventId,
		string targetStreamId, CheckpointTag causedByTag, CheckpointTag expectedTag,
		Action<long> onCommitted = null)
		: base(streamId, eventId, "$>", causedByTag, expectedTag, onCommitted) {
		_targetStreamId = targetStreamId;
	}

	public EmittedLinkTo(
		string streamId, Guid eventId,
		string targetStreamId, int targetEventNumber, CheckpointTag causedByTag, CheckpointTag expectedTag,
		string originalStreamId = null)
		: base(streamId, eventId, "$>", causedByTag, expectedTag, null) {
		_eventNumber = targetEventNumber;
		_targetStreamId = targetStreamId;
	}

	public override string Data {
		get {
			if (!IsReady())
				throw new InvalidOperationException("Link target has not been yet committed");
			return
				_eventNumber.Value.ToString(CultureInfo.InvariantCulture) + "@" + _targetStreamId;
		}
	}

	public override bool IsJson {
		get { return false; }
	}

	public override bool IsReady() {
		return _eventNumber != null;
	}

	public void SetTargetEventNumber(long eventNumber) {
		if (_eventNumber != null)
			throw new InvalidOperationException("Target event number has been already set");
		_eventNumber = eventNumber;
	}
}
