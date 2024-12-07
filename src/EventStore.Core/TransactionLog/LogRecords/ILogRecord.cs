// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using DotNext.Buffers;
using EventStore.LogCommon;

namespace EventStore.Core.TransactionLog.LogRecords;

public interface ILogRecord {
	LogRecordType RecordType { get; }
	byte Version { get; }
	public long LogPosition { get; }
	void WriteTo(ref BufferWriterSlim<byte> writer);
	long GetNextLogPosition(long logicalPosition, int length);
	long GetPrevLogPosition(long logicalPosition, int length);
	int GetSizeWithLengthPrefixAndSuffix();
}
