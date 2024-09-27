// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Caching;
using EventStore.Core.LogAbstraction;

namespace EventStore.Core.LogV2 {
	public class LogV2Sizer : ISizer<string> {
		public int GetSizeInBytes(string t) => MemSizer.SizeOf(t);
	}
}
