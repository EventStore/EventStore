// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Transport.Tcp.Framing;

namespace EventStore.Core.Services.Replication;


public class TransactionFramer : IMessageFramer<IEnumerable<ILogRecord>> {
	private readonly List<ILogRecord> _records;
	private readonly IMessageFramer<ILogRecord> _inner;
	private Action<IEnumerable<ILogRecord>> _handler = _ => { };

	public TransactionFramer(IMessageFramer<ILogRecord> inner) {
		_records = new List<ILogRecord>(capacity: 1024);
		_inner = inner;
		_inner.RegisterMessageArrivedCallback(OnLogRecordUnframed);
	}

	public bool HasData => _records.Count > 0 || _inner.HasData;
	public IEnumerable<ArraySegment<byte>> FrameData(ArraySegment<byte> data) => _inner.FrameData(data);
	public void UnFrameData(IEnumerable<ArraySegment<byte>> data) => _inner.UnFrameData(data);
	public void UnFrameData(ArraySegment<byte> data) => _inner.UnFrameData(data);

	public void Reset() {
		_records.Clear();
		_inner.Reset();
	}

	private void OnLogRecordUnframed(ILogRecord record) {
		_records.Add(record);
		if (record.IsTransactionBoundary()) {
			_handler(_records);
			_records.Clear();
		}
	}

	public void RegisterMessageArrivedCallback(Action<IEnumerable<ILogRecord>> handler) {
		Ensure.NotNull(handler, nameof(handler));
		_handler = handler;
	}

	public bool UnFramePendingLogRecords(out int numLogRecordsUnframed) {
		if (_records.Count == 0) {
			numLogRecordsUnframed = 0;
			return false;
		}

		numLogRecordsUnframed = _records.Count;
		_handler(_records);
		_records.Clear();
		return true;
	}
}
