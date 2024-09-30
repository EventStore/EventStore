// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using Xunit;

namespace EventStore.LogV3.Tests {
	public class RecordHeaderTests {
		[Fact]
		public void can_set_time_stamp() {
			var header = new Raw.RecordHeader {
				TimeStamp = new DateTime(2000, 03, 21),
				Type = LogCommon.LogRecordType.EventType,
				Version = 0x45,
			};

			var now = DateTime.UtcNow;
			header.TimeStamp = now;

			Assert.Equal(LogCommon.LogRecordType.EventType, header.Type);
			Assert.Equal(0x45, header.Version);
			Assert.Equal(now, header.TimeStamp, TimeSpan.FromMilliseconds(7));
		}

		[Fact]
		public void can_get_time_stamp() {
			var timeStamp = new DateTime(0x08_D9_15_80_C3_A1_00_00);
			var header = new Raw.RecordHeader {
				TimeStamp = timeStamp,
			};

			header.Type = LogCommon.LogRecordType.EventType;
			header.Version = 0x45;

			Assert.Equal(LogCommon.LogRecordType.EventType, header.Type);
			Assert.Equal(0x45, header.Version);
			Assert.Equal(timeStamp, header.TimeStamp);
		}
	}
}
