using System;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.ExpectedVersion64Bit {
	[TestFixture]
	[Category("ClientAPI"), Category("LongRunning")]
	public class append_to_stream_with_event_numbers_greater_than_2_billion : MiniNodeWithExistingRecords {
		private const string StreamName = "append_to_stream_with_event_numbers_greater_than_2_billion";
		private const long intMaxValue = (long)int.MaxValue;

		public override void WriteTestScenario() {
			WriteSingleEvent(StreamName, intMaxValue + 1, new string('.', 3000));
			WriteSingleEvent(StreamName, intMaxValue + 2, new string('.', 3000));
			WriteSingleEvent(StreamName, intMaxValue + 3, new string('.', 3000));
			WriteSingleEvent(StreamName, intMaxValue + 4, new string('.', 3000));
			WriteSingleEvent(StreamName, intMaxValue + 5, new string('.', 3000));
		}

		public override void Given() {
			_store = BuildConnection(Node);
			_store.ConnectAsync().Wait();
			_store.SetStreamMetadataAsync(StreamName, EventStore.ClientAPI.ExpectedVersion.Any,
				EventStore.ClientAPI.StreamMetadata.Create(truncateBefore: intMaxValue + 1)).Wait();
		}

		[Test]
		public void should_be_able_to_append_to_stream() {
			var evnt = new EventData(Guid.NewGuid(), "EventType", false, new byte[10], new byte[15]);
			var writeResult = _store.AppendToStreamAsync(StreamName, intMaxValue + 5, evnt).Result;
			Assert.AreEqual(intMaxValue + 6, writeResult.NextExpectedVersion);

			var readResult = _store
				.ReadStreamEventsForwardAsync(StreamName, intMaxValue + 6, 1, false, DefaultData.AdminCredentials)
				.Result;
			Assert.AreEqual(SliceReadStatus.Success, readResult.Status);
			Assert.AreEqual(evnt.EventId, readResult.Events[0].Event.EventId);
		}

		[Test]
		public void should_throw_wrong_expected_version_when_version_incorrect() {
			Exception thrownException = null;
			var evnt = new EventData(Guid.NewGuid(), "EventType", false, new byte[10], new byte[15]);
			try {
				_store.AppendToStreamAsync(StreamName, intMaxValue + 15, evnt).Wait();
			} catch (Exception ex) {
				thrownException = ex;
			}

			Assert.IsNotNull(thrownException);
			Assert.IsInstanceOf(typeof(WrongExpectedVersionException), thrownException.InnerException);
		}
	}
}
