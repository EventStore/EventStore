using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Projections {
	public class get_result : IClassFixture<get_result.Fixture> {
		private readonly Fixture _fixture;

		public get_result(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task returns_expected_result() {
			var result = await _fixture.Client.ProjectionsManager.GetResultAsync<Result>(nameof(get_result), userCredentials: TestCredentials.TestUser1);
			Assert.Equal(1, result.Count);
		}

		private class Result {
			public int Count { get; set; }
		}

		public class Fixture : EventStoreProjectionManagerGrpcFixture {
			private static readonly string Projection = $@"
fromStream('{nameof(get_result)}').when({{
	""$init"": function() {{ return {{ Count: 0 }}; }},
	""$any"": function(s, e) {{ s.Count++; return s; }}
}});
";

			protected override Task Given() => Client.ProjectionsManager.CreateContinuousAsync(nameof(get_result),
				Projection, userCredentials: TestCredentials.Root);

			protected override async Task When() {
				await Client.AppendToStreamAsync(nameof(get_result), AnyStreamRevision.NoStream,
					CreateTestEvents());
			}
		}
	}
}
