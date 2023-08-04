using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Core.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Grpc;

[TestFixture]
public class EnumeratorsTests {
	
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_all_from_start<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {

		private readonly List<Guid> _eventIds = new();

		protected override void Given() {
			EnableReadAll();
			_eventIds.Add(WriteEvent("test-stream", "type1", "{}", "{Data: 1}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type2", "{}", "{Data: 2}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type3", "{}", "{Data: 3}").Item1.EventId);
		}

		[Test]
		public async Task should_receive_live_caught_up_message_after_reading_existing_events() {
			var enumerator = new TestEnumerators.AllSubscription<TStreamId>(new TestEnumerators.SubscriptionRequest(_publisher, Position.Start));

			TestEnumerators.SubscriptionResponse response = await enumerator.GetNext();
			Assert.True(response is TestEnumerators.SubscriptionConfirmation);
			while ((response = await enumerator.GetNext()) != TestEnumerators.SubscriptionResponse.None) {
				if (_eventIds.IsNotEmpty()) {
					Assert.AreEqual(Uuid.FromGuid(_eventIds[0]), ((TestEnumerators.Event)response).RecordedEvent.Id);
					_eventIds.RemoveAt(0);
				} else {
					Assert.True(response is TestEnumerators.CaughtUp);
					break;
				}
			}
		}
	}
	
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_all_from_end<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {

		protected override void Given() {
			EnableReadAll();
			WriteEvent("test-stream", "type1", "{}", "{Data: 1}");
			WriteEvent("test-stream", "type2", "{}", "{Data: 2}");
			WriteEvent("test-stream", "type3", "{}", "{Data: 3}");
		}
		
		[Test]
		public async Task should_receive_live_caught_up_message_immediately() {
			var enumerator = new TestEnumerators.AllSubscription<TStreamId>(new TestEnumerators.SubscriptionRequest(_publisher, Position.End));
			
			Assert.True(await enumerator.GetNext() is TestEnumerators.SubscriptionConfirmation);
			Assert.True(await enumerator.GetNext() is TestEnumerators.CaughtUp);
		}
	}
	
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_filtered_all_from_start_<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {

		private readonly List<Guid> _eventIds = new();
		
		protected override void Given() {
			EnableReadAll();
			_eventIds.Add(WriteEvent("test-stream", "type1", "{}", "{Data: 1}").Item1.EventId);
			WriteEvent("test-stream", "type2", "{}", "{Data: 2}");
			WriteEvent("test-stream", "type3", "{}", "{Data: 3}");
		}

		[Test]
		public async Task should_receive_live_caught_up_message_after_reading_existing_events() {
			var enumerator = new TestEnumerators.AllSubscriptionFiltered<TStreamId>(new TestEnumerators.SubscriptionRequest(_publisher, Position.Start, EventFilter: EventFilter.EventType.Prefixes(false, "type1")));

			TestEnumerators.SubscriptionResponse response = await enumerator.GetNext();
			Assert.True(response is TestEnumerators.SubscriptionConfirmation);
			while ((response = await enumerator.GetNext()) != TestEnumerators.SubscriptionResponse.None) {
				if (_eventIds.IsNotEmpty()) {
					Assert.AreEqual(Uuid.FromGuid(_eventIds[0]), ((TestEnumerators.Event)response).RecordedEvent.Id);
					_eventIds.RemoveAt(0);
				} else {
					Assert.True(response is TestEnumerators.CaughtUp);
					break;
				}
			}
		}
	}
	
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_filtered_all_from_end_<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {

		protected override void Given() {
			EnableReadAll();
			WriteEvent("test-stream", "type1", "{}", "{Data: 1}");
			WriteEvent("test-stream", "type2", "{}", "{Data: 2}");
			WriteEvent("test-stream", "type3", "{}", "{Data: 3}");
		}

		[Test]
		public async Task should_receive_live_caught_up_message_immediately() {
			var enumerator = new TestEnumerators.AllSubscriptionFiltered<TStreamId>(new TestEnumerators.SubscriptionRequest(_publisher, Position.End, EventFilter: EventFilter.EventType.Prefixes(false, "type1")));

			Assert.True(await enumerator.GetNext() is TestEnumerators.SubscriptionConfirmation);
			Assert.True(await enumerator.GetNext() is TestEnumerators.CaughtUp);
		}
	}
	
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_stream_from_start_<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {

		private readonly List<Guid> _eventIds = new();
		
		protected override void Given() {
			EnableReadAll();
			_eventIds.Add(WriteEvent("test-stream1", "type1", "{}", "{Data: 1}").Item1.EventId);
			WriteEvent("test-stream2", "type2", "{}", "{Data: 2}");
			WriteEvent("test-stream3", "type3", "{}", "{Data: 3}");
		}

		[Test]
		public async Task should_receive_live_caught_up_message_after_reading_existing_events() {
			var enumerator = new TestEnumerators.StreamSubscription<TStreamId>(new TestEnumerators.SubscriptionRequest(_publisher, StreamName: "test-stream1"));

			TestEnumerators.SubscriptionResponse response = await enumerator.GetNext();
			Assert.True(response is TestEnumerators.SubscriptionConfirmation);
			while ((response = await enumerator.GetNext()) != TestEnumerators.SubscriptionResponse.None) {
				if (_eventIds.IsNotEmpty()) {
					Assert.AreEqual(Uuid.FromGuid(_eventIds[0]), ((TestEnumerators.Event)response).RecordedEvent.Id);
					_eventIds.RemoveAt(0);
				} else {
					Assert.True(response is TestEnumerators.CaughtUp);
					break;
				}
			}
		}
	}
	
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_stream_from_end<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {

		private readonly List<Guid> _eventIds = new();
		
		protected override void Given() {
			EnableReadAll();
			_eventIds.Add(WriteEvent("test-stream1", "type1", "{}", "{Data: 1}").Item1.EventId);
			WriteEvent("test-stream2", "type2", "{}", "{Data: 2}");
			WriteEvent("test-stream3", "type3", "{}", "{Data: 3}");
		}

		[Test]
		public async Task should_receive_live_caught_up_message_immediately() {
			var enumerator = new TestEnumerators.StreamSubscription<TStreamId>(new TestEnumerators.SubscriptionRequest(_publisher, StreamName: "test-stream1", StartRevision: StreamRevision.End));

			Assert.True(await enumerator.GetNext() is TestEnumerators.SubscriptionConfirmation);
			Assert.True(await enumerator.GetNext() is TestEnumerators.CaughtUp);
		}
	}
}
