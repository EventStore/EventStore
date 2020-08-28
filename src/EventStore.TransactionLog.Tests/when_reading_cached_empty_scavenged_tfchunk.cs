using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.TestHelpers;
using EventStore.Core.TransactionLog.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.TransactionLog.Tests {
	[TestFixture]
	public class when_reading_cached_empty_scavenged_tfchunk : SpecificationWithFilePerTestFixture {
		private TFChunk _chunk;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			_chunk = TFChunkHelper.CreateNewChunk(Filename, isScavenged: true);
			_chunk.CompleteScavenge(new PosMap[0]);
			_chunk.CacheInMemory();
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_chunk.Dispose();
			base.TestFixtureTearDown();
		}

		[Test]
		public void no_record_at_exact_position_can_be_read() {
			Assert.IsFalse(_chunk.TryReadAt(0).Success);
		}

		[Test]
		public void no_record_can_be_read_as_first_record() {
			Assert.IsFalse(_chunk.TryReadFirst().Success);
		}

		[Test]
		public void no_record_can_be_read_as_closest_forward_record() {
			Assert.IsFalse(_chunk.TryReadClosestForward(0).Success);
		}

		[Test]
		public void no_record_can_be_read_as_closest_backward_record() {
			Assert.IsFalse(_chunk.TryReadClosestBackward(0).Success);
		}

		[Test]
		public void no_record_can_be_read_as_last_record() {
			Assert.IsFalse(_chunk.TryReadLast().Success);
		}
	}
}
