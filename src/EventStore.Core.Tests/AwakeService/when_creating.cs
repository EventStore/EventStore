using NUnit.Framework;

namespace EventStore.Core.Tests.AwakeService {
	[TestFixture]
	public class when_creating {
		[Test]
		public void it_can_ce_created() {
			var it = new Core.Services.AwakeReaderService.AwakeService();
			Assert.NotNull(it);
		}
	}
}
