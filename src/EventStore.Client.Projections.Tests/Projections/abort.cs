using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Projections {
	public class abort : IClassFixture<abort.Fixture> {
		private readonly Fixture _fixture;

		public abort(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task status_is_aborted() {
			var name = StandardProjections.Names.First();
			await _fixture.Client.ProjectionsManager.AbortAsync(name, TestCredentials.Root);
			var result = await _fixture.Client.ProjectionsManager.GetStatusAsync(name, TestCredentials.Root);
			Assert.Equal("Aborted/Stopped", result.Status);
		}

		public class Fixture : EventStoreProjectionManagerGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
