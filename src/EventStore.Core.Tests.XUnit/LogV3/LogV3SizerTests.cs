using EventStore.Core.LogV3;
using Xunit;

namespace EventStore.Core.Tests.XUnit.LogV3 {
	public class LogV3SizerTests {
		[Fact]
		public void can_get_size() {
			var sut = new LogV3Sizer();
			Assert.Equal(4, sut.GetSizeInBytes(12345));
		}
	}
}
