using System.Threading.Tasks;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class conditional_append_to_stream : EventStoreClientAPITest {
		private readonly EventStoreClientAPIFixture _fixture;

		public conditional_append_to_stream(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task returns_version_mismatch_with_wrong_expected_version(bool useSsl) {
			var stream = $"{GetStreamName()}_{useSsl}";
			var connection = _fixture.Connections[useSsl];

			var result = await connection.ConditionalAppendToStreamAsync(stream, 7, _fixture.CreateTestEvents())
				.WithTimeout();
			Assert.Equal(ConditionalWriteStatus.VersionMismatch, result.Status);
		}

		[Theory, MemberData(nameof(ExpectedVersionTestCases))]
		public async Task returns_succeeded_with_correct_expected_version(long expectedVersion, string displayName,
			bool useSsl) {
			var stream = $"{GetStreamName()}_{displayName}_{useSsl}";
			var connection = _fixture.Connections[useSsl];

			var result = await connection
				.ConditionalAppendToStreamAsync(stream, expectedVersion, _fixture.CreateTestEvents()).WithTimeout();
			Assert.Equal(ConditionalWriteStatus.Succeeded, result.Status);
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task returns_succeeded_when_stream_deleted(bool useSsl) {
			var stream = $"{GetStreamName()}_{useSsl}";
			var connection = _fixture.Connections[useSsl];

			await connection.AppendToStreamAsync(stream, ExpectedVersion.Any, _fixture.CreateTestEvents())
				.WithTimeout();

			await connection.DeleteStreamAsync(stream, ExpectedVersion.Any, true).WithTimeout();

			var result = await connection
				.ConditionalAppendToStreamAsync(stream, ExpectedVersion.Any, _fixture.CreateTestEvents()).WithTimeout();
			Assert.Equal(ConditionalWriteStatus.StreamDeleted, result.Status);
		}
	}
}
