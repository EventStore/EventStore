// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.LogAbstraction;

public interface INameIndexPersistence<TValue> : IValueLookup<TValue>, IDisposable
	where TValue : struct {
	TValue LastValueAdded { get; }
	ValueTask Init(INameLookup<TValue> source, CancellationToken token);
	bool TryGetValue(string name, out TValue value);
	void Add(string name, TValue value);
}
