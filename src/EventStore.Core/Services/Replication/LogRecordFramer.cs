// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Transport.Tcp.Framing;

namespace EventStore.Core.Services.Replication;

internal sealed class LogRecordFramer : IAsyncMessageFramer<ILogRecord> {
	private readonly IAsyncMessageFramer<BinaryReader> _inner;
	private Func<ILogRecord, CancellationToken, ValueTask> _handler = static (_, _) => ValueTask.CompletedTask;

	public LogRecordFramer(IAsyncMessageFramer<BinaryReader> inner) {
		_inner = inner;
		_inner.RegisterMessageArrivedCallback(OnMessageArrived);
	}

	public bool HasData => _inner.HasData;
	public IEnumerable<ArraySegment<byte>> FrameData(ArraySegment<byte> data) => _inner.FrameData(data);
	public ValueTask UnFrameData(IEnumerable<ArraySegment<byte>> data, CancellationToken token) => _inner.UnFrameData(data, token);
	public ValueTask UnFrameData(ArraySegment<byte> data, CancellationToken token) => _inner.UnFrameData(data, token);
	public void Reset() => _inner.Reset();

	private async ValueTask OnMessageArrived(BinaryReader reader, CancellationToken token) {
		var rawLength = reader.BaseStream.Length;

		if (rawLength >= int.MaxValue)
			throw new ArgumentOutOfRangeException(
				nameof(reader),
				$"Length of stream was {rawLength}");

		var length = (int)rawLength;

		var record = LogRecord.ReadFrom(reader, length: length);
		await _handler(record, token);
	}

	public void RegisterMessageArrivedCallback(Func<ILogRecord, CancellationToken, ValueTask> handler) {
		Ensure.NotNull(handler, nameof(handler));
		_handler = handler;
	}
}
