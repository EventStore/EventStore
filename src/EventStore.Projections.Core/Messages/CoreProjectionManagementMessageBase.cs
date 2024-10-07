// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages;

[DerivedMessage]
public abstract partial class CoreProjectionManagementMessageBase : Message {
	private readonly Guid _projectionIdId;

	protected CoreProjectionManagementMessageBase(Guid projectionId) {
		_projectionIdId = projectionId;
	}

	public Guid ProjectionId {
		get { return _projectionIdId; }
	}
}
