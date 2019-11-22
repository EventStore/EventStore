using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.Streams {
	public class append_to_stream_expected_version_no_stream :
		IClassFixture<append_to_stream_expected_version_no_stream.Fixture> {
		private readonly Fixture _fixture;

		public append_to_stream_expected_version_no_stream(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public void succeeds() {
			Assert.Equal(0, _fixture.Result.NextExpectedVersion);
		}

		[Fact]
		public void returns_position() {
			Assert.NotEqual(default, _fixture.Result.LogPosition);
		}

		public class Fixture : EventStoreGrpcFixture {
			public WriteResult Result { get; private set; }

			protected override Task Given() => Task.CompletedTask;

			protected override async Task When() {
				Result = await Client.AppendToStreamAsync("stream-1", AnyStreamRevision.NoStream,
					CreateTestEvents());
			}
		}
	}
}
