using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.Projections {
	public class list_all_projections : IClassFixture<list_all_projections.Fixture> {
		private readonly Fixture _fixture;

		public list_all_projections(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task returns_expected_result() {
			var result = await _fixture.Client.ProjectionsManager.ListAllAsync(TestCredentials.Root)
				.ToArrayAsync();

			Assert.Equal(result.Select(x => x.Name).OrderBy(x => x), StandardProjections.Names.OrderBy(x => x));
		}

		public class Fixture : EventStoreProjectionManagerGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
