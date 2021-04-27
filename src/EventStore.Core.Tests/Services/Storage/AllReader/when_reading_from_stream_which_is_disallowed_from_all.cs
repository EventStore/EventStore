using System;
using System.Linq;
using NUnit.Framework;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Util;
using static EventStore.Core.Messages.TcpClientMessageDto.Filter;

namespace EventStore.Core.Tests.Services.Storage.AllReader {
	[TestFixture(typeof(LogFormat.V2), typeof(string), "$persistentsubscription-$all::group-checkpoint")]
	[TestFixture(typeof(LogFormat.V2), typeof(string), "$persistentsubscription-$all::group-parked")]
	[TestFixture(typeof(LogFormat.V3), typeof(long), "$persistentsubscription-$all::group-checkpoint")]
	[TestFixture(typeof(LogFormat.V3), typeof(long), "$persistentsubscription-$all::group-parked")]
	public class when_reading_from_stream_which_is_disallowed_from_all<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private string _stream;
		public when_reading_from_stream_which_is_disallowed_from_all(string stream) {
			_stream = stream;
		}

		protected override void WriteTestScenario() {
			WriteSingleEvent(_stream, 1, new string('.', 3000), eventId: Guid.NewGuid(),
				eventType: "event-type-1", retryOnFail: true);
			WriteSingleEvent(_stream, 2, new string('.', 3000), eventId: Guid.NewGuid(),
				eventType: "event-type-1", retryOnFail: true);
		}

		[Test]
		public void should_be_able_to_read_stream_events_forward() {
			var result = ReadIndex.ReadStreamEventsForward(_stream, 0L, 10);
			Assert.AreEqual(2, result.Records.Length);
		}

		[Test]
		public void should_be_able_to_read_stream_events_backward() {
			var result = ReadIndex.ReadStreamEventsBackward(_stream, -1, 10);
			Assert.AreEqual(2, result.Records.Length);
		}
	}
}
