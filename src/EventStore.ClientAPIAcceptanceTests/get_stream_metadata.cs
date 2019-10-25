using System;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Services;
using Xunit;
using ExpectedVersion = EventStore.ClientAPI.ExpectedVersion;
using StreamMetadata = EventStore.ClientAPI.StreamMetadata;
using StreamAcl = EventStore.ClientAPI.StreamAcl;

namespace EventStore.ClientAPIAcceptanceTests {
	[Collection(nameof(EventStoreClientAPIFixture))]
	public class get_stream_metadata : EventStoreClientAPITest, IClassFixture<EventStoreClientAPIFixture> {
		private readonly EventStoreClientAPIFixture _fixture;

		public get_stream_metadata(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task for_non_existing_stream_returns_default(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));
			await connection.ConnectAsync();

			var meta = await connection.GetStreamMetadataAsync(streamName);
			Assert.Equal(streamName, meta.Stream);
			Assert.Equal(false, meta.IsStreamDeleted);
			Assert.Equal(-1, meta.MetastreamVersion);
			Assert.Equal(null, meta.StreamMetadata.MaxCount);
			Assert.Equal(null, meta.StreamMetadata.MaxAge);
			Assert.Equal(null, meta.StreamMetadata.TruncateBefore);
			Assert.Equal(null, meta.StreamMetadata.CacheControl);
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task for_hard_deleted_stream_returns_default_with_stream_deletion(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));
			await connection.ConnectAsync();

			await connection.SetStreamMetadataAsync(streamName, ExpectedVersion.NoStream, StreamMetadata.Create());

			await connection.DeleteStreamAsync(streamName, ExpectedVersion.Any, true);

			var meta = await connection.GetStreamMetadataAsync(streamName);
			Assert.Equal(streamName, meta.Stream);
			Assert.Equal(true, meta.IsStreamDeleted);
			Assert.Equal(EventNumber.DeletedStream, meta.MetastreamVersion);
			Assert.Equal(null, meta.StreamMetadata.MaxCount);
			Assert.Equal(null, meta.StreamMetadata.MaxAge);
			Assert.Equal(null, meta.StreamMetadata.TruncateBefore);
			Assert.Equal(null, meta.StreamMetadata.CacheControl);
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task for_existing_stream_returns_set_metadata(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));
			await connection.ConnectAsync();

			var metadata = StreamMetadata.Create(
				maxCount: 0xDEAD,
				maxAge: TimeSpan.FromSeconds(0xFAD),
				truncateBefore: 0xBEEF,
				cacheControl: TimeSpan.FromSeconds(0xF00L),
				acl: new StreamAcl(SystemRoles.All, SystemRoles.All, SystemRoles.All, SystemRoles.All,
					SystemRoles.All));
			await connection.SetStreamMetadataAsync(streamName, ExpectedVersion.NoStream, metadata);

			var meta = await connection.GetStreamMetadataAsync(streamName);
			Assert.Equal(streamName, meta.Stream);
			Assert.Equal(false, meta.IsStreamDeleted);
			Assert.Equal(0, meta.MetastreamVersion);
			Assert.Equal(metadata.MaxCount, meta.StreamMetadata.MaxCount);
			Assert.Equal(metadata.MaxAge, meta.StreamMetadata.MaxAge);
			Assert.Equal(metadata.TruncateBefore, meta.StreamMetadata.TruncateBefore);
			Assert.Equal(metadata.CacheControl, meta.StreamMetadata.CacheControl);
			Assert.Equal(metadata.Acl.ReadRoles, meta.StreamMetadata.Acl.ReadRoles);
			Assert.Equal(metadata.Acl.WriteRoles, meta.StreamMetadata.Acl.WriteRoles);
			Assert.Equal(metadata.Acl.DeleteRoles, meta.StreamMetadata.Acl.DeleteRoles);
			Assert.Equal(metadata.Acl.MetaReadRoles, meta.StreamMetadata.Acl.MetaReadRoles);
			Assert.Equal(metadata.Acl.MetaWriteRoles, meta.StreamMetadata.Acl.MetaWriteRoles);
		}
	}
}
