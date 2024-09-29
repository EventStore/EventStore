// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
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
			[Test]
			public void verify_that_streams_collide() {
				Assert.AreEqual(Hasher.Hash(Stream), Hasher.Hash(CollidingStream));
				Assert.AreEqual(Hasher.Hash(Stream), Hasher.Hash(CollidingStream1));
			}
		}

		public class WithNoEvents : GetStreamLastEventNumber_KnownCollisions {
			protected override async ValueTask WriteTestScenario(CancellationToken token) {
				await WriteSingleEvent(CollidingStream, 0, "test data", token: token);
				await WriteSingleEvent(CollidingStream1, 0, "test data", token: token);
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
			protected override async ValueTask WriteTestScenario(CancellationToken token) {
				await WriteSingleEvent(Stream, 2, "test data", token: token);
				await WriteSingleEvent(CollidingStream, 3, "test data", token: token);
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

			protected override async ValueTask WriteTestScenario(CancellationToken token) {
				// PTable 1
				await WriteSingleEvent(CollidingStream, 0, string.Empty, token: token);
				await WriteSingleEvent(CollidingStream1, 0, string.Empty, token: token);
				_zeroth = await WriteSingleEvent(Stream, 0, string.Empty, token: token);

				// PTable 2
				_first = await WriteSingleEvent(Stream, 1, string.Empty, token: token);
				_second = await WriteSingleEvent(Stream, 2, string.Empty, token: token);
				await WriteSingleEvent(CollidingStream, 1, string.Empty, token: token);

				// MemTable
				_third = await WriteSingleEvent(Stream, 3, string.Empty, token: token);
				await WriteSingleEvent(CollidingStream, 2, string.Empty, token: token);
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
			protected override async ValueTask WriteTestScenario(CancellationToken token) {
				await WriteSingleEvent(Stream, 0, "test data", token: token);
				await WriteSingleEvent(CollidingStream, 1, "test data", token: token);

				var prepare = await WriteDeletePrepare(Stream, token);
				await WriteDeleteCommit(prepare, token);
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
