// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Transport.Tcp.Framing;

namespace EventStore.Core.Services.Replication;

internal sealed class TransactionFramer : IAsyncMessageFramer<IEnumerable<ILogRecord>> {
	private readonly List<ILogRecord> _records;
	private readonly IAsyncMessageFramer<ILogRecord> _inner;
	private Func<IEnumerable<ILogRecord>, CancellationToken, ValueTask> _handler = static (_, _) => ValueTask.CompletedTask;

	public TransactionFramer(IAsyncMessageFramer<ILogRecord> inner) {
		_records = new List<ILogRecord>(capacity: 1024);
		_inner = inner;
		_inner.RegisterMessageArrivedCallback(OnLogRecordUnframed);
	}

	public bool HasData => _records.Count > 0 || _inner.HasData;
	public IEnumerable<ArraySegment<byte>> FrameData(ArraySegment<byte> data) => _inner.FrameData(data);
	public ValueTask UnFrameData(IEnumerable<ArraySegment<byte>> data, CancellationToken token) => _inner.UnFrameData(data, token);
	public ValueTask UnFrameData(ArraySegment<byte> data, CancellationToken token) => _inner.UnFrameData(data, token);

	public void Reset() {
		_records.Clear();
		_inner.Reset();
	}

	private async ValueTask OnLogRecordUnframed(ILogRecord record, CancellationToken token) {
		_records.Add(record);
		if (record.IsTransactionBoundary()) {
			await _handler(_records, token);
			_records.Clear();
		}
	}

	public void RegisterMessageArrivedCallback(Func<IEnumerable<ILogRecord>, CancellationToken, ValueTask> handler) {
		Ensure.NotNull(handler, nameof(handler));
		_handler = handler;
	}

	public async ValueTask<int?> UnFramePendingLogRecords(CancellationToken token) {
		if (_records is [])
			return null;

		var numLogRecordsUnframed = _records.Count;
		await _handler(_records, token);
		_records.Clear();
		return numLogRecordsUnframed;
	}
}
