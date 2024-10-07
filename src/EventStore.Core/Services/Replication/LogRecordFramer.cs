// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Transport.Tcp.Framing;

namespace EventStore.Core.Services.Replication;


public class LogRecordFramer : IMessageFramer<ILogRecord> {
	private readonly IMessageFramer<BinaryReader> _inner;
	private Action<ILogRecord> _handler = _ => { };

	public LogRecordFramer(IMessageFramer<BinaryReader> inner) {
		_inner = inner;
		_inner.RegisterMessageArrivedCallback(OnMessageArrived);
	}

	public bool HasData => _inner.HasData;
	public IEnumerable<ArraySegment<byte>> FrameData(ArraySegment<byte> data) => _inner.FrameData(data);
	public void UnFrameData(IEnumerable<ArraySegment<byte>> data) => _inner.UnFrameData(data);
	public void UnFrameData(ArraySegment<byte> data) => _inner.UnFrameData(data);
	public void Reset() => _inner.Reset();

	private void OnMessageArrived(BinaryReader reader) {
		var rawLength = reader.BaseStream.Length;

		if (rawLength >= int.MaxValue)
			throw new ArgumentOutOfRangeException(
				nameof(reader),
				$"Length of stream was {rawLength}");

		var length = (int)rawLength;

		var record = LogRecord.ReadFrom(reader, length: length);
		_handler(record);
	}

	public void RegisterMessageArrivedCallback(Action<ILogRecord> handler) {
		Ensure.NotNull(handler, nameof(handler));
		_handler = handler;
	}
}
