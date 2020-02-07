using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class set_stream_metadata : EventStoreClientAPITest {
		private readonly EventStoreClientAPIFixture _fixture;

		public set_stream_metadata(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		public static IEnumerable<object[]> MetadataTestCases() {
			var builder = StreamMetadata.Create(
					maxCount: 0xDEAD,
					maxAge: TimeSpan.FromSeconds(0xFAD),
					truncateBefore: 0xBEEF,
					cacheControl: TimeSpan.FromSeconds(0xF00L),
					acl: new StreamAcl(SystemRoles.All, SystemRoles.All, SystemRoles.All, SystemRoles.All,
						SystemRoles.All)).Copy()
				.SetCustomProperty(nameof(String), "f")
				.SetCustomProperty(nameof(Int32), 1)
				.SetCustomProperty(nameof(Double), 2.0)
				.SetCustomProperty(nameof(Int64), int.MaxValue + 1L)
				.SetCustomProperty(nameof(Boolean), true)
				.SetCustomProperty(nameof(Nullable), default(int?))
				.SetCustomPropertyWithValueAsRawJsonString(nameof(JObject), @"{ ""subProperty"": 999 }");

			var createStreams = new[] {true, false};
			var metadatas = new[] {builder, null};

			foreach (var (expectedVersion, displayName) in ExpectedVersions)
			foreach (var sslType in SslTypes)
			foreach (var createStream in createStreams)
			foreach (var metadata in metadatas) {
				yield return new object[] {expectedVersion, displayName, sslType, createStream, metadata};
			}
		}

		[Theory, MemberData(nameof(MetadataTestCases))]
		public async Task returns_expected_result(long expectedVersion, string displayName, SslType sslType,
			bool createStream, StreamMetadataBuilder builder) {
			var isEmpty = builder == null;
			var streamName = $"{GetStreamName()}_{displayName}_{sslType}_{createStream}_{isEmpty}";
			var connection = _fixture.Connections[sslType];
			if (createStream) {
				await connection.AppendToStreamAsync(streamName, ExpectedVersion.NoStream, _fixture.CreateTestEvents())
					.WithTimeout();
			}

			var expected = (builder ?? StreamMetadata.Build()).Build();
			await connection.SetStreamMetadataAsync(streamName, expectedVersion, expected).WithTimeout();

			var result = await connection.GetStreamMetadataAsync(streamName).WithTimeout();
			var actual = result.StreamMetadata;

			Assert.Equal(streamName, result.Stream);
			Assert.False(result.IsStreamDeleted);
			Assert.Equal(0, result.MetastreamVersion);
			Assert.Equal(expected.MaxCount, actual.MaxCount);
			Assert.Equal(expected.MaxAge, actual.MaxAge);
			Assert.Equal(expected.TruncateBefore, actual.TruncateBefore);
			Assert.Equal(expected.CacheControl, actual.CacheControl);
			Assert.Equal(expected.Acl?.ReadRoles, actual.Acl?.ReadRoles);
			Assert.Equal(expected.Acl?.WriteRoles, actual.Acl?.WriteRoles);
			Assert.Equal(expected.Acl?.DeleteRoles, actual.Acl?.DeleteRoles);
			Assert.Equal(expected.Acl?.MetaReadRoles, actual.Acl?.MetaReadRoles);
			Assert.Equal(expected.Acl?.MetaWriteRoles, actual.Acl?.MetaWriteRoles);
			Assert.Equal(expected.CustomKeys.Count(), actual.CustomKeys.Count());
			foreach (var key in actual.CustomKeys) {
				Assert.True(expected.TryGetValueAsRawJsonString(key, out var value));
				Assert.Equal(actual.GetValueAsRawJsonString(key), value);
			}
		}
	}
}
