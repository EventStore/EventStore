using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Projections {
	public class list_one_time_projections : IClassFixture<list_one_time_projections.Fixture> {
		private readonly Fixture _fixture;

		public list_one_time_projections(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task returns_expected_result() {
			var result = await _fixture.Client.ProjectionsManager.ListOneTimeAsync(TestCredentials.Root)
				.ToArrayAsync();

			var details = Assert.Single(result);
			Assert.Equal("OneTime", details.Mode);
		}

		public class Fixture : EventStoreProjectionManagerGrpcFixture {
			protected override Task Given() =>
				Client.ProjectionsManager.CreateOneTimeAsync(
					"fromAll().when({$init: function (state, ev) {return {};}});", TestCredentials.Root);

			protected override Task When() => Task.CompletedTask;
		}
	}
}
