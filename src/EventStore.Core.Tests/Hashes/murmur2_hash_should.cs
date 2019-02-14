using EventStore.Core.Index.Hashes;
using NUnit.Framework;

namespace EventStore.Core.Tests.Hashes {
	[TestFixture]
	public class murmur2_hash_should {
		public static uint Murmur2ReferenceVerificationValue = 0x27864C1E; // taken from SMHasher

		[Test]
		public void pass_smhasher_verification_test() {
			Assert.IsTrue(SMHasher.VerificationTest(new Murmur2Unsafe(), Murmur2ReferenceVerificationValue));
		}

		[Test, Category("LongRunning"), Explicit]
		public void pass_smhasher_sanity_test() {
			Assert.IsTrue(SMHasher.SanityTest(new Murmur2Unsafe()));
		}
	}
}
