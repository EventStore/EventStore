using EventStore.Core.Data;
using EventStore.Core.Tests.Index.Hashers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.ReadIndex {
	public abstract class GetStreamLastEventNumber_KnownCollisions : ReadIndexTestScenario<LogFormat.V2, string> {
		private const string Stream = "ab-1";
		private const string CollidingStream = "cb-1";
		private const string CollidingStream1 = "db-1";

		protected GetStreamLastEventNumber_KnownCollisions() : base(
			maxEntriesInMemTable: 3,
			lowHasher: new ConstantHasher(0),
			highHasher: new HumanReadableHasher32()) { }

		public class VerifyCollision : GetStreamLastEventNumber_KnownCollisions {
			protected override void WriteTestScenario() { }

			[Test]
			public void verify_that_streams_collide() {
				Assert.AreEqual(Hasher.Hash(Stream), Hasher.Hash(CollidingStream));
				Assert.AreEqual(Hasher.Hash(Stream), Hasher.Hash(CollidingStream1));
			}
		}

		public class WithNoEvents : GetStreamLastEventNumber_KnownCollisions {
			protected override void WriteTestScenario() {
				WriteSingleEvent(CollidingStream, 0, "test data");
				WriteSingleEvent(CollidingStream1, 0, "test data");
			}

			[Test]
			public void with_no_events() {
				Assert.AreEqual(ExpectedVersion.NoStream,
					ReadIndex.GetStreamLastEventNumber_KnownCollisions(
						Stream,
						long.MaxValue));
			}
		}

		public class WithOneEvent : GetStreamLastEventNumber_KnownCollisions {
			protected override void WriteTestScenario() {
				WriteSingleEvent(Stream, 2, "test data");
				WriteSingleEvent(CollidingStream, 3, "test data");
			}

			[Test]
			public void with_one_event() {
				Assert.AreEqual(2,
					ReadIndex.GetStreamLastEventNumber_KnownCollisions(
						Stream,
						long.MaxValue));

				Assert.AreEqual(3,
					ReadIndex.GetStreamLastEventNumber_KnownCollisions(
						CollidingStream,
						long.MaxValue));

			}
		}

		public class WithMultipleEvents : GetStreamLastEventNumber_KnownCollisions {
			private EventRecord _zeroth, _first, _second, _third;

			protected override void WriteTestScenario() {
				// PTable 1
				WriteSingleEvent(CollidingStream, 0, string.Empty);
				WriteSingleEvent(CollidingStream1, 0, string.Empty);
				_zeroth = WriteSingleEvent(Stream, 0, string.Empty);

				// PTable 2
				_first = WriteSingleEvent(Stream, 1, string.Empty);
				_second = WriteSingleEvent(Stream, 2, string.Empty);
				WriteSingleEvent(CollidingStream, 1, string.Empty);

				// MemTable
				_third = WriteSingleEvent(Stream, 3, string.Empty);
				WriteSingleEvent(CollidingStream, 2, string.Empty);
			}

			[Test]
			public void with_multiple_events() {
				Assert.AreEqual(3,
					ReadIndex.GetStreamLastEventNumber_KnownCollisions(
						Stream,
						long.MaxValue));

				Assert.AreEqual(2,
					ReadIndex.GetStreamLastEventNumber_KnownCollisions(
						CollidingStream,
						long.MaxValue));

				Assert.AreEqual(0,
					ReadIndex.GetStreamLastEventNumber_KnownCollisions(
						CollidingStream1,
						long.MaxValue));
			}

			[Test]
			public void with_multiple_events_and_before_position() {
				Assert.AreEqual(3,
					ReadIndex.GetStreamLastEventNumber_KnownCollisions(
						Stream,
						_third.LogPosition + 1));

				Assert.AreEqual(2,
					ReadIndex.GetStreamLastEventNumber_KnownCollisions(
						Stream,
						_third.LogPosition));

				Assert.AreEqual(1,
					ReadIndex.GetStreamLastEventNumber_KnownCollisions(
						Stream,
						_second.LogPosition));

				Assert.AreEqual(0,
					ReadIndex.GetStreamLastEventNumber_KnownCollisions(
						Stream,
						_first.LogPosition));

				Assert.AreEqual(ExpectedVersion.NoStream,
					ReadIndex.GetStreamLastEventNumber_KnownCollisions(
						Stream,
						_zeroth.LogPosition));
			}
		}

		public class WithDeletedStream : GetStreamLastEventNumber_KnownCollisions {
			protected override void WriteTestScenario() {
				WriteSingleEvent(Stream, 0, "test data");
				WriteSingleEvent(CollidingStream, 1, "test data");

				var prepare = WriteDeletePrepare(Stream);
				WriteDeleteCommit(prepare);
			}

			[Test]
			public void with_deleted_stream() {
				Assert.AreEqual(EventNumber.DeletedStream,
					ReadIndex.GetStreamLastEventNumber_KnownCollisions(
						Stream,
						long.MaxValue));

				Assert.AreEqual(1,
					ReadIndex.GetStreamLastEventNumber_KnownCollisions(
						CollidingStream,
						long.MaxValue));
			}
		}
	}

}
