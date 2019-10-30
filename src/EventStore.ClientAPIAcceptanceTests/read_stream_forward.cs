using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Xunit;

namespace EventStore.ClientAPIAcceptanceTests {
	[Collection(nameof(EventStoreClientAPIFixture))]
	public class read_stream_forward : EventStoreClientAPITest, IClassFixture<EventStoreClientAPIFixture> {
		private readonly EventStoreClientAPIFixture _fixture;

		public read_stream_forward(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task when_the_stream_exists(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync();

			var testEvents = _fixture.CreateTestEvents(3).ToArray();

			await connection.AppendToStreamAsync(streamName, ExpectedVersion.NoStream, testEvents);

			var result = await connection.ReadStreamEventsForwardAsync(streamName, 0, testEvents.Length, false);

			Assert.Equal(SliceReadStatus.Success, result.Status);
			Assert.True(result.IsEndOfStream);
			Assert.Equal(ReadDirection.Forward, result.ReadDirection);
			Assert.Equal(testEvents.Select(x => x.EventId), result.Events.Select(x => x.OriginalEvent.EventId));
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task when_the_stream_does_not_exist(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync();

			var result = await connection.ReadStreamEventsForwardAsync(streamName, 0, 5, false);

			Assert.Equal(SliceReadStatus.StreamNotFound, result.Status);
			Assert.True(result.IsEndOfStream);
			Assert.Equal(ReadDirection.Forward, result.ReadDirection);
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task when_the_stream_is_deleted(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync();

			await connection.AppendToStreamAsync(streamName, ExpectedVersion.NoStream, _fixture.CreateTestEvents(3));
			await connection.DeleteStreamAsync(streamName, ExpectedVersion.Any);

			var result = await connection.ReadStreamEventsForwardAsync(streamName, 0, 5, false);

			Assert.Equal(SliceReadStatus.StreamNotFound, result.Status);
			Assert.True(result.IsEndOfStream);
			Assert.Equal(ReadDirection.Forward, result.ReadDirection);
		}
	}
}
