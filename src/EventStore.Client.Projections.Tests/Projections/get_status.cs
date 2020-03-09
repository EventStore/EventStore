using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Projections {
	public class get_status : IClassFixture<get_status.Fixture> {
		private readonly Fixture _fixture;

		public get_status(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task returns_expected_result() {
			var name = StandardProjections.Names.First();
			var result = await _fixture.Client.ProjectionsManager.GetStatusAsync(name, userCredentials: TestCredentials.TestUser1);

			Assert.Equal(name, result.Name);
		}

		public class Fixture : EventStoreProjectionManagerGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
