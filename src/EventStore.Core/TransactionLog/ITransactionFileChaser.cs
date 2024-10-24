// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog;

public interface ITransactionFileChaser : IDisposable {
	ICheckpoint Checkpoint { get; }

	void Open();

	ValueTask<SeqReadResult> TryReadNext(CancellationToken token);

	void Close();
	void Flush();
}
