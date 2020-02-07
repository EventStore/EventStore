using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class set_stream_metadata_as_raw_bytes : EventStoreClientAPITest {
		private readonly EventStoreClientAPIFixture _fixture;

		public set_stream_metadata_as_raw_bytes(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(ExpectedVersionTestCases))]
		public async Task for_non_existing_stream(long expectedVersion, string displayName, SslType sslType) {
			var streamName = $"{GetStreamName()}_{displayName}_{sslType}";

			var connection = _fixture.Connections[sslType];
			await connection.SetStreamMetadataAsync(streamName, expectedVersion, Array.Empty<byte>()).WithTimeout();
		}

		[Theory, MemberData(nameof(ExpectedVersionTestCases))]
		public async Task for_existing_stream(long expectedVersion, string displayName, SslType sslType) {
			var streamName = $"{GetStreamName()}_{displayName}_{sslType}";

			var connection = _fixture.Connections[sslType];
			await connection.AppendToStreamAsync(streamName, expectedVersion, _fixture.CreateTestEvents())
				.WithTimeout();

			await connection.SetStreamMetadataAsync(streamName, expectedVersion, Array.Empty<byte>()).WithTimeout();
		}
	}
}
