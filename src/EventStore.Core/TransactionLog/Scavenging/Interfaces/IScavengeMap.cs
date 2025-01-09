// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;

namespace EventStore.Core.TransactionLog.Scavenging.Interfaces;

public interface IScavengeMap<TKey, TValue> {
	bool TryGetValue(TKey key, out TValue value);
	TValue this[TKey key] { set; }
	bool TryRemove(TKey key, out TValue value);
	IEnumerable<KeyValuePair<TKey, TValue>> AllRecords();
}
