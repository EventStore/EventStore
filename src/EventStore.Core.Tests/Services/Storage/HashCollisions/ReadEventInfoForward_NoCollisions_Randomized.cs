using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Index.Hashers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.HashCollisions {
	[TestFixture(3)]
	[TestFixture(33)]
	[TestFixture(123)]
	[TestFixture(523)]
	public class ReadEventInfoForward_NoCollisions_Randomized : ReadIndexTestScenario<LogFormat.V2, string> {
		private const string Stream = "ab-1";
		private const ulong Hash = 98;
		private const string NonCollidingStream = "cd-1";

		private readonly Random _random = new Random();
		private readonly int _numEvents;
		private readonly List<EventRecord> _events;

		public ReadEventInfoForward_NoCollisions_Randomized(int maxEntriesInMemTable) : base(
			chunkSize: 1_000_000,
			maxEntriesInMemTable: maxEntriesInMemTable,
			lowHasher: new ConstantHasher(0),
			highHasher: new HumanReadableHasher32()) {
			_numEvents = _random.Next(100, 400);
			_events = new List<EventRecord>(_numEvents);
		}

		private static void CheckResult(EventRecord[] events, IndexReadEventInfoResult result) {
			Assert.AreEqual(events.Length, result.EventInfos.Length);
			for (int i = 0; i < events.Length; i++) {
				Assert.AreEqual(events[i].EventNumber, result.EventInfos[i].EventNumber);
				Assert.AreEqual(events[i].LogPosition, result.EventInfos[i].LogPosition);
			}
		}

		protected override void WriteTestScenario() {
			var streamLast = 0L;
			var nonCollidingStreamLast = 0L;

			for (int i = 0; i < _numEvents; i++) {
				if (_random.Next(2) == 0) {
					_events.Add(WriteSingleEvent(Stream, streamLast++, "test data"));
				} else {
					_events.Add(WriteSingleEvent(NonCollidingStream, nonCollidingStreamLast++, "testing"));
				}
			}
		}

		[Test]
		public void returns_correct_events_before_position() {
			var curEvents = new List<EventRecord>();

			foreach (var @event in _events) {
				var result = ReadIndex.ReadEventInfoForward_NoCollisions(Hash, 0, int.MaxValue, @event.LogPosition);
				CheckResult(curEvents.ToArray(), result);
				if (curEvents.Count == 0)
					Assert.True(result.IsEndOfStream);
				else
					Assert.AreEqual(int.MaxValue, result.NextEventNumber);

				if (@event.EventStreamId == Stream)
					curEvents.Add(@event);
			}
		}

		[Test]
		public void returns_correct_events_with_max_count() {
			var curEvents = new List<EventRecord>();

			foreach (var @event in _events) {
				if (@event.EventStreamId != Stream) continue;
				curEvents.Add(@event);

				int maxCount = Math.Min((int)@event.EventNumber + 1, _random.Next(10, 100));
				var fromEventNumber = @event.EventNumber - maxCount + 1;

				Assert.Greater(maxCount, 0);
				Assert.GreaterOrEqual(fromEventNumber, 0);

				var result = ReadIndex.ReadEventInfoForward_NoCollisions(
					Hash, fromEventNumber, maxCount, long.MaxValue);
				CheckResult(curEvents.Skip(curEvents.Count - maxCount).ToArray(), result);
				Assert.AreEqual(@event.EventNumber + 1, result.NextEventNumber);
			}
		}
	}

}
