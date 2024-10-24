// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.LogAbstraction;
using StreamId = System.UInt32;

namespace EventStore.Core.LogV3;

public class NameIndexInMemoryPersistence :
	INameIndexPersistence<StreamId> {

	readonly ConcurrentDictionary<string, StreamId> _dict = new();

	public StreamId LastValueAdded { get; private set; }

	public NameIndexInMemoryPersistence() {
	}

	public void Dispose() {
	}

	public ValueTask Init(INameLookup<StreamId> source, CancellationToken token)
		=> token.IsCancellationRequested ? ValueTask.FromCanceled(token) : ValueTask.CompletedTask;

	public void Add(string name, StreamId value) {
		_dict[name] = value;
		LastValueAdded = value;
	}

	public bool TryGetValue(string name, out StreamId value) =>
		_dict.TryGetValue(name, out value);

	public StreamId LookupValue(string name) {
		if (string.IsNullOrEmpty(name))
			throw new ArgumentNullException(nameof(name));

		if (!_dict.TryGetValue(name, out var value))
			return 0;

		return value;
	}
}
