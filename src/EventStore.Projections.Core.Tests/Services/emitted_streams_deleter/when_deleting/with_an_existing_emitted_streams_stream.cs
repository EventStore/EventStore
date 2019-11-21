﻿using EventStore.ClientAPI;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Projections.Core.Tests.Services.emitted_streams_deleter.when_deleting {
	[TestFixture]
	public class with_an_existing_emitted_streams_stream : SpecificationWithEmittedStreamsTrackerAndDeleter {
		protected Action _onDeleteStreamCompleted;
		protected ManualResetEvent _resetEvent = new ManualResetEvent(false);
		private string _testStreamName = "test_stream";
		private ManualResetEvent _eventAppeared = new ManualResetEvent(false);
		private EventStore.ClientAPI.SystemData.UserCredentials _credentials;

		protected override async Task Given() {
			_credentials = new EventStore.ClientAPI.SystemData.UserCredentials("admin", "changeit");
			_onDeleteStreamCompleted = () => { _resetEvent.Set(); };

			await base.Given();
			var sub = await _conn.SubscribeToStreamAsync(_projectionNamesBuilder.GetEmittedStreamsName(), true, (s, evnt) => {
				_eventAppeared.Set();
				return Task.CompletedTask;
			}, userCredentials: _credentials);

			_emittedStreamsTracker.TrackEmittedStream(new EmittedEvent[] {
				new EmittedDataEvent(
					_testStreamName, Guid.NewGuid(), "type1", true,
					"data", null, CheckpointTag.FromPosition(0, 100, 50), null),
			});

			if (!_eventAppeared.WaitOne(TimeSpan.FromSeconds(5))) {
				Assert.Fail("Timed out waiting for emitted stream event");
			}

			sub.Unsubscribe();

			var emittedStreamResult =
				await _conn.ReadStreamEventsForwardAsync(_projectionNamesBuilder.GetEmittedStreamsName(), 0, 1, false,
					_credentials);
			Assert.AreEqual(1, emittedStreamResult.Events.Length);
			Assert.AreEqual(SliceReadStatus.Success, emittedStreamResult.Status);
		}

		protected override Task When() {
			_emittedStreamsDeleter.DeleteEmittedStreams(_onDeleteStreamCompleted);
			if (!_resetEvent.WaitOne(TimeSpan.FromSeconds(10))) {
				throw new Exception("Timed out waiting callback.");
			}

			return Task.CompletedTask;
		}

		[Test]
		public async Task should_have_deleted_the_tracked_emitted_stream() {
			var result = await _conn.ReadStreamEventsForwardAsync(_testStreamName, 0, 1, false,
				new EventStore.ClientAPI.SystemData.UserCredentials("admin", "changeit"));
			Assert.AreEqual(SliceReadStatus.StreamNotFound, result.Status);
		}


		[Test]
		public async Task should_have_deleted_the_checkpoint_stream() {
			var result = await _conn.ReadStreamEventsForwardAsync(_projectionNamesBuilder.GetEmittedStreamsCheckpointName(),
				0, 1, false, new EventStore.ClientAPI.SystemData.UserCredentials("admin", "changeit"));
			Assert.AreEqual(SliceReadStatus.StreamNotFound, result.Status);
		}

		[Test]
		public async Task should_have_deleted_the_emitted_streams_stream() {
			var result = await _conn.ReadStreamEventsForwardAsync(_projectionNamesBuilder.GetEmittedStreamsName(), 0, 1,
				false, new EventStore.ClientAPI.SystemData.UserCredentials("admin", "changeit"));
			Assert.AreEqual(SliceReadStatus.StreamNotFound, result.Status);
		}
	}
}
