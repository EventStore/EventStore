// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.LogAbstraction;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3 {
	public class LogV3Sizer : ISizer<StreamId> {
		public int GetSizeInBytes(StreamId t) => sizeof(StreamId);
	}
}
