using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Hashers {
	public class HumanReadableHasherTests {
		[Test]
		public void hashes_original_stream() {
			var sut = new HumanReadableHasher();
			Assert.AreEqual('a', sut.Hash("ma-1"));
		}

		[Test]
		public void hashes_meta_stream() {
			var sut = new HumanReadableHasher();
			Assert.AreEqual('m', sut.Hash("$$ma-1"));
		}

		[Test]
		public void hashes_empty_string() {
			var sut = new HumanReadableHasher();
			Assert.AreEqual(0UL, sut.Hash(""));
		}
	}
}
