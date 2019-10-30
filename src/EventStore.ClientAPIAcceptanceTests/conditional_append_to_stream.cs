using System.Threading.Tasks;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class conditional_append_to_stream : EventStoreClientAPITest, IClassFixture<EventStoreClientAPIFixture> {
		private readonly EventStoreClientAPIFixture _fixture;

		public conditional_append_to_stream(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task returns_version_mismatch_with_wrong_expected_version(bool useSsl) {
			var stream = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync();

			var result = await connection.ConditionalAppendToStreamAsync(stream, 7, _fixture.CreateTestEvents());
			Assert.Equal(ConditionalWriteStatus.VersionMismatch, result.Status);
		}

		[Theory, MemberData(nameof(ExpectedVersionTestCases))]
		public async Task returns_succeeded_with_correct_expected_version(long expectedVersion, string displayName,
			bool useSsl) {
			var stream = $"{GetStreamName()}_{displayName}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync();

			var result = await connection
				.ConditionalAppendToStreamAsync(stream, expectedVersion, _fixture.CreateTestEvents());
			Assert.Equal(ConditionalWriteStatus.Succeeded, result.Status);
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task returns_succeeded_when_stream_deleted(bool useSsl) {
			var stream = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync();

			await connection.AppendToStreamAsync(stream, ExpectedVersion.Any, _fixture.CreateTestEvents());

			await connection.DeleteStreamAsync(stream, ExpectedVersion.Any, true);

			var result = await connection
				.ConditionalAppendToStreamAsync(stream, ExpectedVersion.Any, _fixture.CreateTestEvents());
			Assert.Equal(ConditionalWriteStatus.StreamDeleted, result.Status);
		}
	}
}
