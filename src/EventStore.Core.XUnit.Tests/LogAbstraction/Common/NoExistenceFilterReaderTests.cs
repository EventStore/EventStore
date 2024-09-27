using EventStore.Core.LogAbstraction.Common;
using Xunit;

namespace EventStore.Core.XUnit.Tests.LogAbstraction.Common {
	public class NoExistenceFilterReaderTests {
		[Fact]
		public void might_contain_anything() {
			var sut = new NoExistenceFilterReader();
			Assert.True(sut.MightContain(4)); // chosen by fair dice roll
		}
	}
}
