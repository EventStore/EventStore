using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	[Collection(nameof(EventStoreClientAPIFixture))]
	public class get_stream_metadata_as_raw_bytes : EventStoreClientAPITest, IClassFixture<EventStoreClientAPIFixture> {
		private readonly EventStoreClientAPIFixture _fixture;

		public get_stream_metadata_as_raw_bytes(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}
		
		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task for_non_existing_stream_returns_default(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));
			await connection.ConnectAsync();

			var meta = await connection.GetStreamMetadataAsRawBytesAsync(streamName);
			Assert.Equal(streamName, meta.Stream);
			Assert.False(meta.IsStreamDeleted);
			Assert.Equal(-1, meta.MetastreamVersion);
			Assert.Equal(Array.Empty<byte>(), meta.StreamMetadata);
		}
	}
}
