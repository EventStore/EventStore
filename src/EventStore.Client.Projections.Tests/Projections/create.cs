using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Projections {
	public class create : IClassFixture<create.Fixture> {
		private readonly Fixture _fixture;

		public create(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task one_time() {
			await _fixture.Client.ProjectionsManager.CreateOneTimeAsync(
				"fromAll().when({$init: function (state, ev) {return {};}});", TestCredentials.Root);
		}

		[Theory, InlineData(true), InlineData(false)]
		public async Task continuous(bool trackEmittedStreams) {
			await _fixture.Client.ProjectionsManager.CreateContinuousAsync(
				$"{nameof(continuous)}_{trackEmittedStreams}",
				"fromAll().when({$init: function (state, ev) {return {};}});", trackEmittedStreams,
				TestCredentials.Root);
		}

		[Fact]
		public async Task transient() {
			await _fixture.Client.ProjectionsManager.CreateTransientAsync(
				nameof(transient),
				"fromAll().when({$init: function (state, ev) {return {};}});", TestCredentials.Root);
		}

		public class Fixture : EventStoreProjectionManagerGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
