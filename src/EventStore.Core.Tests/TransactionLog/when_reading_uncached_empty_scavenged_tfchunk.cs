using EventStore.Core.TransactionLog.Chunks.TFChunk;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog {
	[TestFixture]
	public class when_reading_uncached_empty_scavenged_tfchunk : SpecificationWithFilePerTestFixture {
		private TFChunk _chunk;

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			_chunk = TFChunkHelper.CreateNewChunk(Filename, isScavenged: true);
			_chunk.CompleteScavenge(new PosMap[0]);
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
