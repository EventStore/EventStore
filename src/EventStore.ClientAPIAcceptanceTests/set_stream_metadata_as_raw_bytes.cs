using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class set_stream_metadata_as_raw_bytes : EventStoreClientAPITest, IClassFixture<EventStoreClientAPIFixture> {
		private readonly EventStoreClientAPIFixture _fixture;

		public set_stream_metadata_as_raw_bytes(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(ExpectedVersionTestCases))]
		public async Task for_non_existing_stream(long expectedVersion, string displayName, bool useSsl) {
			var streamName = $"{GetStreamName()}_{displayName}_{useSsl}";

			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));
			await connection.ConnectAsync();

			await connection.SetStreamMetadataAsync(streamName, expectedVersion, Array.Empty<byte>());
		}

		[Theory, MemberData(nameof(ExpectedVersionTestCases))]
		public async Task for_existing_stream(long expectedVersion, string displayName, bool useSsl) {
			var streamName = $"{GetStreamName()}_{displayName}_{useSsl}";

			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));
			await connection.ConnectAsync();

			await connection.AppendToStreamAsync(streamName, expectedVersion, _fixture.CreateTestEvents());

			await connection.SetStreamMetadataAsync(streamName, expectedVersion, Array.Empty<byte>());
		}
	}
}
