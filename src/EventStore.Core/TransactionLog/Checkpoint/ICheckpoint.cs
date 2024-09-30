// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.TransactionLog.Checkpoint {
	public interface ICheckpoint : IReadOnlyCheckpoint {
		void Write(long checkpoint);
		void Flush();
		void Close(bool flush);
		IReadOnlyCheckpoint AsReadOnly() => this;
	}

	public interface IReadOnlyCheckpoint {
		string Name { get; }
		long Read();
		long ReadNonFlushed();

		event Action<long> Flushed;
	}
}
