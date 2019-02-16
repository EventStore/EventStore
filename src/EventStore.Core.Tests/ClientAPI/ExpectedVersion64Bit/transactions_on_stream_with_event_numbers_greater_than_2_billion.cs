using System;
using EventStore.ClientAPI;
using NUnit.Framework;
using EventStore.Core.Data;

namespace EventStore.Core.Tests.ClientAPI.ExpectedVersion64Bit {
	[TestFixture]
	[Category("ClientAPI"), Category("LongRunning")]
	public class transactions_on_stream_with_event_numbers_greater_than_2_billion : MiniNodeWithExistingRecords {
		private const string StreamName = "transactions_on_stream_with_event_numbers_greater_than_2_billion";
		private const long intMaxValue = (long)int.MaxValue;

		private EventRecord _r1, _r2, _r3, _r4, _r5;

		public override void WriteTestScenario() {
			_r1 = WriteSingleEvent(StreamName, intMaxValue + 1, new string('.', 3000));
			_r2 = WriteSingleEvent(StreamName, intMaxValue + 2, new string('.', 3000));
			_r3 = WriteSingleEvent(StreamName, intMaxValue + 3, new string('.', 3000));
			_r4 = WriteSingleEvent(StreamName, intMaxValue + 4, new string('.', 3000));
			_r5 = WriteSingleEvent(StreamName, intMaxValue + 5, new string('.', 3000));
		}

		public override void Given() {
			_store = BuildConnection(Node);
			_store.ConnectAsync().Wait();
			_store.SetStreamMetadataAsync(StreamName, EventStore.ClientAPI.ExpectedVersion.Any,
				EventStore.ClientAPI.StreamMetadata.Create(truncateBefore: intMaxValue + 1)).Wait();
		}

		[Test]
		public void should_be_able_to_append_to_stream_in_a_transaction() {
			var evnt1 = new EventData(Guid.NewGuid(), "EventType", false, new byte[10], new byte[15]);
			var evnt2 = new EventData(Guid.NewGuid(), "EventType", false, new byte[10], new byte[15]);

			var transaction = _store.StartTransactionAsync(StreamName, intMaxValue + 5, DefaultData.AdminCredentials)
				.Result;
			transaction.WriteAsync(evnt1).Wait();
			transaction.WriteAsync(evnt2).Wait();
			transaction.CommitAsync().Wait();

			var records = _store.ReadStreamEventsForwardAsync(StreamName, intMaxValue, 10, false).Result;
			Assert.AreEqual(7, records.Events.Length);
			Assert.AreEqual(_r1.EventId, records.Events[0].Event.EventId);
			Assert.AreEqual(_r2.EventId, records.Events[1].Event.EventId);
			Assert.AreEqual(_r3.EventId, records.Events[2].Event.EventId);
			Assert.AreEqual(_r4.EventId, records.Events[3].Event.EventId);
			Assert.AreEqual(_r5.EventId, records.Events[4].Event.EventId);
			Assert.AreEqual(evnt1.EventId, records.Events[5].Event.EventId);
			Assert.AreEqual(evnt2.EventId, records.Events[6].Event.EventId);
		}
	}
}
