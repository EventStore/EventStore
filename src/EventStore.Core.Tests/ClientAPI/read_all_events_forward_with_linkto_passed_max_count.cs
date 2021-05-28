﻿using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI {
	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class read_all_events_forward_with_linkto_passed_max_count<TLogFormat, TStreamId> : SpecificationWithLinkToToMaxCountDeletedEvents<TLogFormat, TStreamId> {
		private StreamEventsSlice _read;

		protected override async Task When() {
			_read = await _conn.ReadStreamEventsForwardAsync(LinkedStreamName, 0, 1, true);
		}

		[Test]
		public void one_event_is_read() {
			Assert.AreEqual(1, _read.Events.Length);
		}
	}
}
