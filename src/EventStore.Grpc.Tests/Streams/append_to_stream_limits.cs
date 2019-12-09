using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.Streams {
	public class append_to_stream_limits : IClassFixture<append_to_stream_limits.Fixture> {
		private readonly Fixture _fixture;
		private const int MaxAppendSize = 1024;

		public append_to_stream_limits(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task succeeds_when_size_is_less_than_max_append_size() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.NoStream,
				_fixture.GetEvents(MaxAppendSize - 1));
		}

		[Fact]
		public async Task fails_when_size_exceeds_max_append_size() {
			var stream = _fixture.GetStreamName();

			await Assert.ThrowsAsync<MaximumAppendSizeExceededException>(() => _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.NoStream,
				_fixture.GetEvents(MaxAppendSize * 2)));
		}

		public class Fixture : EventStoreGrpcFixture {
			public Fixture() : base(builder => builder.WithMaxAppendSize(MaxAppendSize)) {
			}

			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;

			public IEnumerable<EventData> GetEvents(int maxSize) {
				int size = 0;
				foreach (var e in CreateTestEvents(int.MaxValue)) {
					size += e.Data.Length;
					if (size >= maxSize) {
						yield break;
					}

					yield return e;
				}
			}
		}
	}
}
