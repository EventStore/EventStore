using EventStore.Core.LogAbstraction;
using Xunit;

namespace EventStore.Core.Tests.XUnit.LogAbstraction {
	public class IdentityLowHasherTests {
		[Theory]
		[InlineData(0, 0)]
		[InlineData(5, 5)]
		[InlineData(0xAAAA_BBBB_CCCC_DDDD, 0xCCCC_DDDD)]
		[InlineData(0xFFFF_FFFF_FFFF_FFFF, 0xFFFF_FFFF)]
		public void hashes_correctly(ulong x, uint expected) {
			var sut = new IdentityLowHasher();
			Assert.Equal(expected, sut.Hash((long)x));
		}
	}
}
