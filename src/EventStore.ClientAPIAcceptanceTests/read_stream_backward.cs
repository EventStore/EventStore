using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class read_stream_backward : EventStoreClientAPITest {
		private readonly EventStoreClientAPIFixture _fixture;

		public read_stream_backward(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task when_the_stream_exists(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];

			var testEvents = _fixture.CreateTestEvents(3).ToArray();

			await connection.AppendToStreamAsync(streamName, ExpectedVersion.NoStream, testEvents).WithTimeout();

			var result = await connection.ReadStreamEventsBackwardAsync(streamName, -1, testEvents.Length, false)
				.WithTimeout();

			Assert.Equal(SliceReadStatus.Success, result.Status);
			Assert.True(result.IsEndOfStream);
			Assert.Equal(ReadDirection.Backward, result.ReadDirection);
			Assert.Equal(testEvents.Reverse().Select(x => x.EventId),
				result.Events.Select(x => x.OriginalEvent.EventId));
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task when_the_stream_does_not_exist(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];

			var result = await connection.ReadStreamEventsBackwardAsync(streamName, -1, 5, false).WithTimeout();

			Assert.Equal(SliceReadStatus.StreamNotFound, result.Status);
			Assert.True(result.IsEndOfStream);
			Assert.Equal(ReadDirection.Backward, result.ReadDirection);
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task when_the_stream_is_deleted(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];

			await connection.AppendToStreamAsync(streamName, ExpectedVersion.NoStream, _fixture.CreateTestEvents(3));
			await connection.DeleteStreamAsync(streamName, ExpectedVersion.Any).WithTimeout();

			var result = await connection.ReadStreamEventsBackwardAsync(streamName, -1, 5, false).WithTimeout();

			Assert.Equal(SliceReadStatus.StreamNotFound, result.Status);
			Assert.True(result.IsEndOfStream);
			Assert.Equal(ReadDirection.Backward, result.ReadDirection);
		}
	}
}
