﻿using System;
using System.IO;
using EventStore.Core.TransactionLog.LogRecords;
using Xunit;

namespace EventStore.Core.XUnit.Tests.TransactionLog.LogRecords {
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

			using var memoryStream = new MemoryStream();
			var writer = new BinaryWriter(memoryStream);
			var length = 111;

			writer.Write(length);
			prepare.WriteTo(writer);
			writer.Write(length);

			var recordLen = (int)memoryStream.Length;

			Assert.Equal(recordLen, prepare.SizeOnDisk);
		}
	}
}
