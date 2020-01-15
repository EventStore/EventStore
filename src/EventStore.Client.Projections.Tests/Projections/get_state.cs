using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Projections {
	public class get_state : IClassFixture<get_state.Fixture> {
		private readonly Fixture _fixture;

		public get_state(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task returns_expected_result() {
			var result = await _fixture.Client.ProjectionsManager.GetStateAsync<Result>(nameof(get_state));
			Assert.Equal(1, result.Count);
		}

		private class Result {
			public int Count { get; set; }
		}

		public class Fixture : EventStoreProjectionManagerGrpcFixture {
			private static readonly string Projection = $@"
fromStream('{nameof(get_state)}').when({{
	""$init"": function() {{ return {{ Count: 0 }}; }},
	""$any"": function(s, e) {{ s.Count++; return s; }}
}});
";

			protected override Task Given() => Client.ProjectionsManager.CreateContinuousAsync(nameof(get_state),
				Projection, userCredentials: TestCredentials.Root);

			protected override async Task When() {
				await Client.AppendToStreamAsync(nameof(get_state), AnyStreamRevision.NoStream,
					CreateTestEvents());
			}
		}
	}
}
