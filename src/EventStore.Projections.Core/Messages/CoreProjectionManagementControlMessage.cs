// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages;

[DerivedMessage]
public abstract partial class CoreProjectionManagementControlMessage : CoreProjectionManagementMessageBase {
	private readonly Guid _workerId;

	public Guid WorkerId {
		get { return _workerId; }
	}

	public CoreProjectionManagementControlMessage(Guid projectionId, Guid workerId)
		: base(projectionId) {
		_workerId = workerId;
	}
}
