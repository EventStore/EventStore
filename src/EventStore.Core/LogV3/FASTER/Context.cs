// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using FASTER.core;

namespace EventStore.Core.LogV3.FASTER {
	public class Context<TValue> {
		public Status Status { get; set; }
		public TValue Value { get; set; }
	}
}

