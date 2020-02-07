using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class get_stream_metadata_as_raw_bytes : EventStoreClientAPITest {
		private readonly EventStoreClientAPIFixture _fixture;

		public get_stream_metadata_as_raw_bytes(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task for_non_existing_stream_returns_default(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];
			var meta = await connection.GetStreamMetadataAsRawBytesAsync(streamName).WithTimeout();
			Assert.Equal(streamName, meta.Stream);
			Assert.False(meta.IsStreamDeleted);
			Assert.Equal(-1, meta.MetastreamVersion);
			Assert.Equal(Array.Empty<byte>(), meta.StreamMetadata);
		}
	}
}
