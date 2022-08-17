using EventStore.Common.Utils;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Utils {
	public class RoundingExtensionsTests {
		[Fact]
		public void CanRoundUp() {
			Assert.Equal(0, 0L.RoundUpToMultipleOf(4));
			Assert.Equal(4, 1L.RoundUpToMultipleOf(4));
			Assert.Equal(4, 2L.RoundUpToMultipleOf(4));
			Assert.Equal(4, 3L.RoundUpToMultipleOf(4));
			Assert.Equal(4, 4L.RoundUpToMultipleOf(4));
			Assert.Equal(8, 5L.RoundUpToMultipleOf(4));
		}

		[Fact]
		public void CanRoundDown() {
			Assert.Equal(0, 3L.RoundDownToMultipleOf(4));
			Assert.Equal(4, 4L.RoundDownToMultipleOf(4));
			Assert.Equal(4, 5L.RoundDownToMultipleOf(4));
			Assert.Equal(4, 6L.RoundDownToMultipleOf(4));
			Assert.Equal(4, 7L.RoundDownToMultipleOf(4));
			Assert.Equal(8, 8L.RoundDownToMultipleOf(4));
		}
	}
}
