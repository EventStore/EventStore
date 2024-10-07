// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Data;

namespace EventStore.Core;

public class VNodeStatusChangeArgs : EventArgs {
	public readonly VNodeState NewVNodeState;

	public VNodeStatusChangeArgs(VNodeState newVNodeState) {
		NewVNodeState = newVNodeState;
	}
}
