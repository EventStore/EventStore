using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Index.Hashers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.ReadIndex {
	[TestFixture]
	public abstract class ReadEventInfoBackward_KnownCollisions : ReadIndexTestScenario<LogFormat.V2, string> {
		private const string Stream = "ab-1";
		private const string CollidingStream = "cb-1";
		private const string CollidingStream1 = "db-1";

		protected ReadEventInfoBackward_KnownCollisions() : base(
			maxEntriesInMemTable: 3,
			lowHasher: new ConstantHasher(0),
			highHasher: new HumanReadableHasher32()) { }

		private static void CheckResult(EventRecord[] events, IndexReadEventInfoResult result) {
			var eventInfos = result.EventInfos.Reverse().ToArray();
			Assert.AreEqual(events.Length, eventInfos.Length);
			for (int i = 0; i < events.Length; i++) {
				Assert.AreEqual(events[i].EventNumber, eventInfos[i].EventNumber);
				Assert.AreEqual(events[i].LogPosition, eventInfos[i].LogPosition);
			}
		}

		public class VerifyCollision : ReadEventInfoBackward_KnownCollisions {
			protected override void WriteTestScenario() { }

			[Test]
			public void verify_that_streams_collide() {
				Assert.AreEqual(Hasher.Hash(Stream), Hasher.Hash(CollidingStream));
				Assert.AreEqual(Hasher.Hash(Stream), Hasher.Hash(CollidingStream1));
			}
		}

		public class WithNoEvents : ReadEventInfoBackward_KnownCollisions {
			protected override void WriteTestScenario() {
				WriteSingleEvent(CollidingStream, 0, "test data");
				WriteSingleEvent(CollidingStream1, 0, "test data");
			}

			[Test]
			public void with_no_events() {
				var result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
					Stream,
					0,
					int.MaxValue,
					long.MaxValue);

				Assert.AreEqual(0, result.EventInfos.Length);
				Assert.True(result.IsEndOfStream);

				result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
					Stream,
					-1,
					int.MaxValue,
					long.MaxValue);

				Assert.AreEqual(0, result.EventInfos.Length);
				Assert.True(result.IsEndOfStream);

			}
		}

		public class WithOneEvent : ReadEventInfoBackward_KnownCollisions {
			private EventRecord _event;

			protected override void WriteTestScenario() {
				_event = WriteSingleEvent(Stream, 0, "test data");
				WriteSingleEvent(CollidingStream, 1, "test data");
			}

			[Test]
			public void with_one_event() {
				var result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
					Stream,
					0,
					int.MaxValue,
					long.MaxValue);

				Assert.AreEqual(1, result.EventInfos.Length);
				CheckResult(new[] { _event }, result);
				Assert.True(result.IsEndOfStream);

				result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
					Stream,
					1,
					int.MaxValue,
					long.MaxValue);

				CheckResult(new[] { _event }, result);
				Assert.True(result.IsEndOfStream);

				result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
					Stream,
					-1,
					int.MaxValue,
					long.MaxValue);

				Assert.AreEqual(1, result.EventInfos.Length);
				CheckResult(new[] { _event }, result);
				Assert.True(result.IsEndOfStream);
			}
		}

		public class WithMultipleEvents : ReadEventInfoBackward_KnownCollisions {
			private readonly List<EventRecord> _events = new List<EventRecord>();

			protected override void WriteTestScenario() {
				// PTable 1
				WriteSingleEvent(CollidingStream, 0, string.Empty);
				WriteSingleEvent(CollidingStream, 1, string.Empty);
				_events.Add(WriteSingleEvent(Stream, 0, string.Empty));

				// PTable 2
				_events.Add(WriteSingleEvent(Stream, 1, string.Empty));
				_events.Add(WriteSingleEvent(Stream, 2, string.Empty));
				WriteSingleEvent(CollidingStream, 2, string.Empty);

				// MemTable
				_events.Add(WriteSingleEvent(Stream, 3, string.Empty));
				WriteSingleEvent(CollidingStream, 3, string.Empty);
			}

			[Test]
			public void with_multiple_events() {
				for (int fromEventNumber = 0; fromEventNumber <= 4; fromEventNumber++) {
					var result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
						Stream,
						fromEventNumber,
						int.MaxValue,
						long.MaxValue);

					CheckResult(_events.Take(fromEventNumber + 1).ToArray(), result);
					Assert.True(result.IsEndOfStream);
				}
			}

			[Test]
			public void with_multiple_events_and_max_count() {
				for (int fromEventNumber = 0; fromEventNumber <= 4; fromEventNumber++) {
					var result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
						Stream,
						fromEventNumber,
						2,
						long.MaxValue);

					CheckResult(_events.Take(fromEventNumber + 1).Skip(fromEventNumber + 1 - 2).ToArray(), result);
					if (fromEventNumber - 2 < 0)
						Assert.True(result.IsEndOfStream);
					else
						Assert.AreEqual(fromEventNumber - 2, result.NextEventNumber);
				}
			}

			[Test]
			public void with_multiple_events_and_before_position() {
				for (int fromEventNumber = 0; fromEventNumber + 1 < _events.Count; fromEventNumber++) {
					var result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
						Stream,
						fromEventNumber,
						int.MaxValue,
						_events[fromEventNumber + 1].LogPosition);

					CheckResult(_events.Take(fromEventNumber + 1).ToArray(), result);
					Assert.True(result.IsEndOfStream);

					result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
						Stream,
						-1,
						int.MaxValue,
						_events[fromEventNumber + 1].LogPosition);

					CheckResult(_events.Take(fromEventNumber + 1).ToArray(), result);
					Assert.True(result.IsEndOfStream);
				}
			}
		}

		public class WithDeletedStream : ReadEventInfoBackward_KnownCollisions {
			private readonly List<EventRecord> _events = new List<EventRecord>();

			protected override void WriteTestScenario() {
				_events.Add(WriteSingleEvent(Stream, 0, "test data"));
				WriteSingleEvent(CollidingStream, 1, "test data");
				_events.Add(WriteSingleEvent(Stream, 1, "test"));

				var prepare = WriteDeletePrepare(Stream);
				WriteDeleteCommit(prepare);
			}

			[Test]
			public void can_read_events() {
				var result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
					Stream,
					1,
					int.MaxValue,
					long.MaxValue);

				CheckResult(_events.ToArray(), result);
				Assert.True(result.IsEndOfStream);
			}

			[Test]
			public void can_read_tombstone_event() {
				var result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
					Stream,
					EventNumber.DeletedStream,
					int.MaxValue,
					long.MaxValue);

				Assert.AreEqual(1, result.EventInfos.Length);
				Assert.AreEqual(EventNumber.DeletedStream, result.EventInfos[0].EventNumber);
				Assert.AreEqual(long.MaxValue - int.MaxValue, result.NextEventNumber);

				result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
					Stream,
					-1,
					int.MaxValue,
					long.MaxValue);

				Assert.AreEqual(1, result.EventInfos.Length);
				Assert.AreEqual(EventNumber.DeletedStream, result.EventInfos[0].EventNumber);
				Assert.AreEqual(long.MaxValue - int.MaxValue, result.NextEventNumber);

				result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
					Stream,
					EventNumber.DeletedStream - 1,
					1,
					long.MaxValue);

				Assert.AreEqual(0, result.EventInfos.Length);
				Assert.AreEqual(1, result.NextEventNumber);
			}
		}

		public class WithGapsBetweenEvents : ReadEventInfoBackward_KnownCollisions {
			private readonly List<EventRecord> _events = new List<EventRecord>();

			protected override void WriteTestScenario() {
				// PTable 1
				WriteSingleEvent(CollidingStream, 0, string.Empty);
				WriteSingleEvent(CollidingStream, 1, string.Empty);
				_events.Add(WriteSingleEvent(Stream, 0, string.Empty));

				// PTable 2
				_events.Add(WriteSingleEvent(Stream, 4, string.Empty));
				_events.Add(WriteSingleEvent(Stream, 5, string.Empty));
				WriteSingleEvent(CollidingStream, 2, string.Empty);

				// MemTable
				_events.Add(WriteSingleEvent(Stream, 7, string.Empty));
				WriteSingleEvent(CollidingStream, 3, string.Empty);
			}

			[Test]
			public void strictly_returns_up_to_max_count_consecutive_events_from_start_event_number() {
				var result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
					Stream,
					7,
					int.MaxValue,
					long.MaxValue);

				CheckResult(_events.ToArray(), result);
				Assert.True(result.IsEndOfStream);

				result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
					Stream,
					7,
					4,
					long.MaxValue);

				CheckResult(_events.Skip(1).ToArray(), result);
				Assert.AreEqual(3, result.NextEventNumber);

				result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
					Stream,
					3,
					1,
					long.MaxValue);

				Assert.AreEqual(0, result.EventInfos.Length);
				Assert.AreEqual(2, result.NextEventNumber); // from colliding stream, but doesn't matter much

				result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
					Stream,
					2,
					1,
					long.MaxValue);

				Assert.AreEqual(0, result.EventInfos.Length);
				Assert.AreEqual(1, result.NextEventNumber); // from colliding stream, but doesn't matter much

			}
		}


		public class WithDuplicateEvents : ReadEventInfoBackward_KnownCollisions {
			private readonly List<EventRecord> _events = new List<EventRecord>();

			protected override void WriteTestScenario() {
				// PTable 1
				WriteSingleEvent(CollidingStream, 0, string.Empty);
				WriteSingleEvent(CollidingStream, 1, string.Empty);
				_events.Add(WriteSingleEvent(Stream, 0, string.Empty));

				// PTable 2
				_events.Add(WriteSingleEvent(Stream, 1, string.Empty));
				_events.Add(WriteSingleEvent(Stream, 1, string.Empty));
				WriteSingleEvent(CollidingStream, 2, string.Empty);

				// MemTable
				_events.Add(WriteSingleEvent(Stream, 2, string.Empty));
				_events.Add(WriteSingleEvent(Stream, 2, string.Empty));
				_events.Add(WriteSingleEvent(Stream, 2, string.Empty));
			}

			[Test]
			public void result_is_deduplicated_keeping_oldest_duplicates() {
				var result = ReadIndex.ReadEventInfoBackward_KnownCollisions(
					Stream,
					3,
					int.MaxValue,
					long.MaxValue);

				CheckResult(
					_events
						.GroupBy(x => x.EventNumber)
						.Select(x => x.First())
						.OrderBy(x => x.EventNumber)
						.ToArray(),
					result);
				Assert.True(result.IsEndOfStream);
			}
		}
	}

}
