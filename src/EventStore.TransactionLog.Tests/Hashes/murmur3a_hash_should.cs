using EventStore.Core.TransactionLog.Hashes;
using NUnit.Framework;

namespace EventStore.Core.TransactionLog.Tests.Hashes {
	[TestFixture]
	public class murmur3a_hash_should {
		public static uint Murmur3AReferenceVerificationValue = 0xB0F57EE3; // taken from SMHasher

		[Test]
		public void pass_smhasher_verification_test() {
			Assert.IsTrue(SMHasher.VerificationTest(new Murmur3AUnsafe(), Murmur3AReferenceVerificationValue));
		}

		[Test, Category("LongRunning"), Explicit]
		public void pass_smhasher_sanity_test() {
			Assert.IsTrue(SMHasher.SanityTest(new Murmur3AUnsafe()));
		}
	}
}
