// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Caching {
	public struct CacheStats {
		public string Key { get; }
		public string Name { get; }
		public long Capacity { get; }
		public long Size { get; }
		public long Count { get; }
		public int NumChildren { get; }
		public double UtilizationPercent => Capacity != 0 ? 100.0 * Size / Capacity : 0;

		public CacheStats(string key, string name, long capacity, long size, long count, int numChildren) {
			Key = key;
			Name = name;
			Capacity = capacity;
			Size = size;
			Count = count;
			NumChildren = numChildren;
		}
	}
}
