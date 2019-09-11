using System.Collections.Generic;
 using System.Linq;
 using EventStore.ClientAPI;
 using EventStore.Core.Services;
 using EventStore.Core.Tests.ClientAPI.Helpers;
 using NUnit.Framework;
 using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;
 using ResolvedEvent = EventStore.ClientAPI.ResolvedEvent;
 using StreamMetadata = EventStore.ClientAPI.StreamMetadata;

  namespace EventStore.Core.Tests.ClientAPI {
 	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
 	public class read_all_events_filtered_paging_should : SpecificationWithMiniNode {
 		private List<EventData> _testEvents = new List<EventData>();

  		private List<EventData> _testEventsA;

  		private List<EventData> _testEventsC;

  		protected override void When() {
 			_conn.SetStreamMetadataAsync("$all", -1,
 					StreamMetadata.Build().SetReadRole(SystemRoles.All),
 					DefaultData.AdminCredentials)
 				.Wait();

  			_testEventsA = Enumerable
 				.Range(0, 10)
 				.Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "AEvent"))
 				.ToList();

  			var testEventsB = Enumerable
 				.Range(0, 10000)
 				.Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "BEvent"))
 				.ToList();

  			_testEventsC = Enumerable
 				.Range(0, 10)
 				.Select(x => TestEvent.NewTestEvent(x.ToString(), eventName: "CEvent"))
 				.ToList();

  			_testEvents.AddRange(_testEventsA);
 			_testEvents.AddRange(testEventsB);
 			_testEvents.AddRange(_testEventsC);

  			_conn.AppendToStreamAsync("stream-a", ExpectedVersion.NoStream, _testEvents).Wait();
 		}

  		[Test, Category("LongRunning")]
 		public void handle_paging_between_events_forward() {
            var filter = Filter.EventType.Prefix("CE");

  			var sliceStart = Position.Start;
 			var read = new List<ResolvedEvent>();
 			AllEventsSlice slice;

  			do {
 				slice = _conn.ReadAllEventsForwardFilteredAsync(sliceStart, 4096, false, filter, maxSearchWindow: 4096)
 					.GetAwaiter()
 					.GetResult();
 				read.AddRange(slice.Events);
 				sliceStart = slice.NextPosition;
 			} while (!slice.IsEndOfStream);

  			Assert.That(EventDataComparer.Equal(
 				_testEventsC.ToArray(),
 				read.Select(x => x.Event).ToArray()));
 		}
        
        [Test, Category("LongRunning")]
        public void handle_paging_between_events_backward() {
	        var filter = Filter.EventType.Prefix("AE");

	        var sliceStart = Position.End;
	        var read = new List<ResolvedEvent>();
	        AllEventsSlice slice;

	        do {
		        slice = _conn.ReadAllEventsBackwardFilteredAsync(sliceStart, 4096, false, filter, maxSearchWindow: 4096)
			        .GetAwaiter()
			        .GetResult();
		        read.AddRange(slice.Events);
		        sliceStart = slice.NextPosition;
	        } while (!slice.IsEndOfStream);

	        Assert.That(EventDataComparer.Equal(
		        _testEventsA.ReverseEvents(),
		        read.Select(x => x.Event).ToArray()));
        }
 	}
 }
