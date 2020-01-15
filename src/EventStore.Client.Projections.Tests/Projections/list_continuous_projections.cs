using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Projections {
	public class list_continuous_projections : IClassFixture<list_continuous_projections.Fixture> {
		private readonly Fixture _fixture;

		public list_continuous_projections(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task returns_expected_result() {
			var result = await _fixture.Client.ProjectionsManager.ListContinuousAsync(TestCredentials.Root)
				.ToArrayAsync();

			Assert.Equal(result.Select(x => x.Name).OrderBy(x => x),
				StandardProjections.Names.Concat(new[] {nameof(list_continuous_projections)}).OrderBy(x => x));
			Assert.True(result.All(x => x.Mode == "Continuous"));
		}

		public class Fixture : EventStoreProjectionManagerGrpcFixture {
			protected override Task Given() =>
				Client.ProjectionsManager.CreateContinuousAsync(
					nameof(list_continuous_projections),
					"fromAll().when({$init: function (state, ev) {return {};}});",
					userCredentials: TestCredentials.Root);

			protected override Task When() => Task.CompletedTask;
		}
	}
}
