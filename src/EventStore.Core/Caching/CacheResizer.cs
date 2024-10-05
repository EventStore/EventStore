// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Common.Utils;

namespace EventStore.Core.Caching;

public abstract class CacheResizer {
	public string Name => Cache.Name;
	public long Size => Cache.Size;
	public long Count => Cache.Count;
	public long FreedSize => Cache.FreedSize;
	protected IDynamicCache Cache { get; }
	public ResizerUnit Unit { get; }

	protected CacheResizer(
		ResizerUnit unit,
		IDynamicCache cache) {
		Ensure.NotNull(cache, nameof(cache));

		Cache = cache;
		Unit = unit;
	}

	protected string BuildStatsKey(string parentKey) =>
		parentKey.Length == 0 ? Name : $"{parentKey}-{Name}";

	protected static string GetParentKey(string key) {
		var index = key.LastIndexOf('-');
		return index < 0 ? null : key[..index];
	}
}
