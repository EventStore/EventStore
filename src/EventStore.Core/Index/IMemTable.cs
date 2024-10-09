// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;

namespace EventStore.Core.Index;

public interface IMemTable : ISearchTable {
	bool MarkForConversion();
	void Add(ulong stream, long version, long position);
	void AddEntries(IReadOnlyList<IndexEntry> entries);
}
