using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Projections {
	public class enable : IClassFixture<enable.Fixture> {
		private readonly Fixture _fixture;

		public enable(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task status_is_running() {
			var name = StandardProjections.Names.First();
			await _fixture.Client.ProjectionsManager.EnableAsync(name, TestCredentials.Root);
			var result = await _fixture.Client.ProjectionsManager.GetStatusAsync(name, TestCredentials.Root);
			Assert.Equal("Running", result.Status);
		}

		public class Fixture : EventStoreProjectionManagerGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
			protected override bool RunStandardProjections => false;
		}
	}
}
