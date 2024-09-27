// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.LogAbstraction {
	public interface INameExistenceFilter : IExistenceFilterReader<string>, IDisposable {
		void Initialize(INameExistenceFilterInitializer source, long truncateToPosition);
		void TruncateTo(long checkpoint);
		void Verify(double corruptionThreshold);
		void Add(string name);
		void Add(ulong hash);
		long CurrentCheckpoint { get; set; }
	}

	public interface IExistenceFilterReader<T> {
		bool MightContain(T item);
	}
}
