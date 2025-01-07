// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog;

public interface ITransactionFileWriter : IAsyncDisposable {
	ValueTask Open(CancellationToken token);
	bool CanWrite(int numBytes);
	ValueTask<(bool, long)> Write(ILogRecord record, CancellationToken token);
	void OpenTransaction();
	ValueTask<long?> WriteToTransaction(ILogRecord record, CancellationToken token);
	void CommitTransaction();
	bool HasOpenTransaction();
	ValueTask Flush(CancellationToken token);

	long Position { get; }
	long FlushedPosition { get; }
}
