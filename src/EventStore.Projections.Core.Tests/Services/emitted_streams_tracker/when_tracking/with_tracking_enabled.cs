﻿using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Tests;

namespace EventStore.Projections.Core.Tests.Services.emitted_streams_tracker.when_tracking {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class with_tracking_enabled<TLogFormat, TStreamId> : SpecificationWithEmittedStreamsTrackerAndDeleter<TLogFormat, TStreamId> {
		private CountdownEvent _eventAppeared = new CountdownEvent(1);
		private UserCredentials _credentials = new UserCredentials("admin", "changeit");

		protected override async Task When() {
			var sub = await _conn.SubscribeToStreamAsync(_projectionNamesBuilder.GetEmittedStreamsName(), true, (s, evnt) => {
				_eventAppeared.Signal();
				return Task.CompletedTask;
			}, userCredentials: _credentials);

			_emittedStreamsTracker.TrackEmittedStream(new EmittedEvent[] {
				new EmittedDataEvent(
					"test_stream", Guid.NewGuid(), "type1", true,
					"data", null, CheckpointTag.FromPosition(0, 100, 50), null, null)
			});

			_eventAppeared.Wait(TimeSpan.FromSeconds(5));
			sub.Unsubscribe();
		}

		[Test]
		public async Task should_write_a_stream_tracked_event() {
			var result = await _conn.ReadStreamEventsForwardAsync(_projectionNamesBuilder.GetEmittedStreamsName(), 0, 200,
				false, _credentials);
			Assert.AreEqual(1, result.Events.Length);
			Assert.AreEqual("test_stream", Helper.UTF8NoBom.GetString(result.Events[0].Event.Data));
			Assert.AreEqual(0, _eventAppeared.CurrentCount);
		}
	}
}
