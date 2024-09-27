// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.LogAbstraction {
	public interface INameIndexPersistence<TValue> : IValueLookup<TValue>, IDisposable {
		TValue LastValueAdded { get; }
		void Init(INameLookup<TValue> source);
		bool TryGetValue(string name, out TValue value);
		void Add(string name, TValue value);
	}
}
