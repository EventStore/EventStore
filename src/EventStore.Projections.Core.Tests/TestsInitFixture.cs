using NUnit.Framework;

namespace EventStore.Projections.Core.Tests {
	[SetUpFixture]
	public class TestsInitFixture {
		private readonly EventStore.Core.Tests.TestsInitFixture _initFixture =
			new EventStore.Core.Tests.TestsInitFixture();

		[OneTimeSetUp]
		public void SetUp() {
			_initFixture.SetUp();
		}

		[OneTimeTearDown]
		public void TearDown() {
			_initFixture.TearDown();
		}
	}
}
