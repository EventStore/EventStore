using EventStore.Core.LogAbstraction;
using Xunit;

namespace EventStore.Core.Tests.XUnit.LogAbstraction {
	public class CombinedHasherTests {
		[Theory]
		[InlineData(0)]
		[InlineData(5)]
		[InlineData(-5)]
		[InlineData(long.MaxValue)]
		[InlineData(long.MinValue)]
		public void identity(long x) {
			var low = (long)new IdentityLowHasher().Hash(x);
			var high = (long)new IdentityHighHasher().Hash(x);
			long actual = (high << 32) | low;
			Assert.Equal(x, actual);
		}
	}
}
