using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class append_to_stream : EventStoreClientAPITest {
		private readonly EventStoreClientAPIFixture _fixture;

		public append_to_stream(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(ExpectedVersionTestCases))]
		public async Task expected_version(long expectedVersion, string displayName, bool useSsl) {
			var streamName = $"{GetStreamName()}_{displayName}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync().WithTimeout();

			var result = await connection.AppendToStreamAsync(streamName, expectedVersion, _fixture.CreateTestEvents())
				.WithTimeout();
			Assert.Equal(0, result.NextExpectedVersion);
			await connection.AppendToStreamAsync(streamName, result.NextExpectedVersion, _fixture.CreateTestEvents())
				.WithTimeout();
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task wrong_expected_version(bool useSsl) {
			var streamName = GetStreamName();
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync().WithTimeout();

			var ex = await Assert.ThrowsAsync<WrongExpectedVersionException>(() =>
					connection.AppendToStreamAsync(streamName, ExpectedVersion.StreamExists,
						_fixture.CreateTestEvents()))
				.WithTimeout();

			Assert.Equal(ExpectedVersion.StreamExists, ex.ExpectedVersion);
			Assert.Equal(ExpectedVersion.NoStream, ex.ActualVersion);
		}
	}
}
