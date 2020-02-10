using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Core;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "Network")]
	public class read_all_with_timeout : IClassFixture<read_all_with_timeout.Fixture> {
		private readonly Fixture _fixture;

		public read_all_with_timeout(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task fails_when_operation_expired() {
			var rpcException = await Assert.ThrowsAsync<RpcException>(() => _fixture.Client
				.ReadAllAsync(Direction.Backwards, Position.Start, 1,
					options => options.TimeoutAfter = TimeSpan.Zero)
				.ToArrayAsync().AsTask());

			Assert.Equal(StatusCode.DeadlineExceeded, rpcException.StatusCode);
		}

		public class Fixture : EventStoreGrpcFixture {
			public Fixture() : base(clientSettings: new EventStoreClientSettings(new UriBuilder().Uri) {
				CreateHttpClient = () =>
					new HttpClient(new ResponseVersionHandler {InnerHandler = new DelayedHandler(200)})
			}) {
			}

			protected override Task Given() => Task.CompletedTask;

			protected override Task When() => Task.CompletedTask;
		}
	}
}
