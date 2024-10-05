// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using EventStore.LogCommon;

namespace EventStore.Core.TransactionLog.LogRecords;

public static class LogV3Reader {
	public static byte[] ReadBytes(LogRecordType type, byte version, BinaryReader reader, int recordLength) {
		// todo: if we could get some confidence that we would return to the pool
		// (e.g. with reference counting) then we could use arraypool here. or just maybe a ring buffer
		// var bytes = ArrayPool<byte>.Shared.Rent(length);
		var bytes = new byte[recordLength];
		bytes[0] = (byte)type;
		bytes[1] = version;
		reader.Read(bytes.AsSpan(2..recordLength));
		return bytes;
	}
}
