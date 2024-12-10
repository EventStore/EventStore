// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using DotNext.Buffers;
using EventStore.Core.TransactionLog.LogRecords;
using Xunit;

namespace EventStore.Core.XUnit.Tests.TransactionLog.LogRecords;

public class SizeOnDiskTests {
	[Theory]
	[InlineData(1)]
	[InlineData(127)] // just before needing an extra byte for the 7bit encoding
	[InlineData(128)] // just after
	[InlineData(100_000)]
	public void size_on_disk_is_correct(int stringLength) {
		var theString = new string('a', stringLength);

		var prepare = new PrepareLogRecord(
			logPosition: 123,
			correlationId: Guid.NewGuid(),
			eventId: Guid.NewGuid(),
			transactionPosition: 456,
			transactionOffset: 321,
			eventStreamId: theString,
			eventStreamIdSize: null,
			expectedVersion: 789,
			timeStamp: DateTime.Now,
			flags: PrepareFlags.SingleWrite,
			eventType: theString,
			eventTypeSize: null,
			data: new byte[] { 0xDE, 0XAD, 0xC0, 0XDE },
			metadata: new byte[] { 0XC0, 0xDE },
			prepareRecordVersion: 1);

		var writer = new BufferWriterSlim<byte>(prepare.GetSizeWithLengthPrefixAndSuffix());
		try {
			const int dummyLength = 111;

			writer.WriteLittleEndian(dummyLength);
			prepare.WriteTo(ref writer);
			writer.WriteLittleEndian(dummyLength);

			var recordLen = writer.WrittenCount;

			Assert.Equal(recordLen, prepare.SizeOnDisk);
		} finally {
			writer.Dispose();
		}
	}

	[Fact]
	public void commit_record_size_on_disk_is_correct() {
		var record = new CommitLogRecord(
			logPosition: 123,
			correlationId: Guid.NewGuid(),
			transactionPosition: 456,
			timeStamp: DateTime.Now,
			firstEventNumber: 789,
			commitRecordVersion: 1);

		var writer = new BufferWriterSlim<byte>(record.GetSizeWithLengthPrefixAndSuffix());
		try {
			const int dummyLength = 111;

			writer.WriteLittleEndian(dummyLength);
			record.WriteTo(ref writer);
			writer.WriteLittleEndian(dummyLength);

			var recordLen = writer.WrittenCount;

			Assert.Equal(recordLen, record.GetSizeWithLengthPrefixAndSuffix());
		} finally {
			writer.Dispose();
		}
	}

	[Fact]
	public void system_record_size_on_disk_is_correct() {
		var record = new SystemLogRecord(
			logPosition: 123,
			timeStamp: DateTime.Now,
			systemRecordType: SystemRecordType.Epoch,
			systemRecordSerialization: SystemRecordSerialization.Binary,
			data: new byte[] { 0xDE, 0XAD, 0xC0, 0XDE });

		var writer = new BufferWriterSlim<byte>(record.GetSizeWithLengthPrefixAndSuffix());
		try {
			const int dummyLength = 111;

			writer.WriteLittleEndian(dummyLength);
			record.WriteTo(ref writer);
			writer.WriteLittleEndian(dummyLength);

			var recordLen = writer.WrittenCount;

			Assert.Equal(recordLen, record.GetSizeWithLengthPrefixAndSuffix());
		} finally {
			writer.Dispose();
		}
	}
}
