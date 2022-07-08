using System;
using EventStore.Core.Data;
using EventStore.Core.Tests.Index.Hashers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.HashCollisions {
	[TestFixture]
	public abstract class GetStreamLastEventNumber_NoCollisions : ReadIndexTestScenario {
		private const string Stream = "ab-1";
		private const ulong Hash = 98;
		private const string NonCollidingStream = "cd-1";

		private string GetStreamId(ulong hash) => hash == Hash ? Stream : throw new ArgumentException();

		protected GetStreamLastEventNumber_NoCollisions() : base(
			maxEntriesInMemTable: 3,
			lowHasher: new ConstantHasher(0),
			highHasher: new HumanReadableHasher32()) { }

		public class VerifyNoCollision : GetStreamLastEventNumber_NoCollisions {
			protected override void WriteTestScenario() { }

			[Test]
			public void verify_that_streams_do_not_collide() {
				Assert.AreNotEqual(Hasher.Hash(Stream), Hasher.Hash(NonCollidingStream));
			}
		}

		public class WithNoEvents : GetStreamLastEventNumber_NoCollisions {
			protected override void WriteTestScenario() { }

			[Test]
			public void with_no_events() {
				Assert.AreEqual(ExpectedVersion.NoStream,
					ReadIndex.GetStreamLastEventNumber_NoCollisions(
						Hash,
						GetStreamId,
						long.MaxValue));
			}
		}

		public class WithOneEvent : GetStreamLastEventNumber_NoCollisions {
			protected override void WriteTestScenario() {
				WriteSingleEvent(Stream, 0, "test data");
			}

			[Test]
			public void with_one_event() {
				Assert.AreEqual(0,
					ReadIndex.GetStreamLastEventNumber_NoCollisions(
						Hash,
						GetStreamId,
						long.MaxValue));
			}
		}

		public class WithMultipleEvents : GetStreamLastEventNumber_NoCollisions {
			private EventRecord _zeroth, _first, _second, _third;

			protected override void WriteTestScenario() {
				// PTable 1
				WriteSingleEvent(NonCollidingStream, 0, string.Empty);
				WriteSingleEvent(NonCollidingStream, 1, string.Empty);
				_zeroth = WriteSingleEvent(Stream, 0, string.Empty);

				// PTable 2
				_first = WriteSingleEvent(Stream, 1, string.Empty);
				_second = WriteSingleEvent(Stream, 2, string.Empty);
				WriteSingleEvent(NonCollidingStream, 2, string.Empty);

				// MemTable
				_third = WriteSingleEvent(Stream, 3, string.Empty);
				WriteSingleEvent(NonCollidingStream, 3, string.Empty);
			}

			[Test]
			public void with_multiple_events() {
				Assert.AreEqual(3,
					ReadIndex.GetStreamLastEventNumber_NoCollisions(
						Hash,
						GetStreamId,
						long.MaxValue));
			}

			[Test]
			public void with_multiple_events_and_before_position() {
				Assert.AreEqual(3,
					ReadIndex.GetStreamLastEventNumber_NoCollisions(
						Hash,
						GetStreamId,
						_third.LogPosition + 1));

				Assert.AreEqual(2,
					ReadIndex.GetStreamLastEventNumber_NoCollisions(
						Hash,
						GetStreamId,
						_third.LogPosition));

				Assert.AreEqual(1,
					ReadIndex.GetStreamLastEventNumber_NoCollisions(
						Hash,
						GetStreamId,
						_second.LogPosition));

				Assert.AreEqual(0,
					ReadIndex.GetStreamLastEventNumber_NoCollisions(
						Hash,
						GetStreamId,
						_first.LogPosition));

				Assert.AreEqual(ExpectedVersion.NoStream,
					ReadIndex.GetStreamLastEventNumber_NoCollisions(
						Hash,
						GetStreamId,
						_zeroth.LogPosition));
			}
		}

		public class WithDeletedStream : GetStreamLastEventNumber_NoCollisions {
			protected override void WriteTestScenario() {
				WriteSingleEvent(Stream, 0, "test data");
				WriteSingleEvent(Stream, 1, "test data");

				var prepare = WriteDeletePrepare(Stream);
				WriteDeleteCommit(prepare);
			}

			[Test]
			public void with_deleted_stream() {
				Assert.AreEqual(EventNumber.DeletedStream,
					ReadIndex.GetStreamLastEventNumber_NoCollisions(
						Hash,
						GetStreamId,
						long.MaxValue));
			}
		}
	}

}
