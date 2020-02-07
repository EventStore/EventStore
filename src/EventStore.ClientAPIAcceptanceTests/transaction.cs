using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class transaction : EventStoreClientAPITest {
		private readonly EventStoreClientAPIFixture _fixture;

		public transaction(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(ExpectedVersionTestCases))]
		public async Task expected_version(long expectedVersion, string displayName, SslType sslType) {
			var streamName = $"{GetStreamName()}_{displayName}_{sslType}";
			var connection = _fixture.Connections[sslType];

			using var transaction = await connection.StartTransactionAsync(streamName, expectedVersion).WithTimeout();

			await transaction.WriteAsync(_fixture.CreateTestEvents()).WithTimeout();
			var result = await transaction.CommitAsync().WithTimeout();
			Assert.Equal(0, result.NextExpectedVersion);
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task wrong_expected_version(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];

			using var transaction = await connection.StartTransactionAsync(streamName, 1).WithTimeout();
			await transaction.WriteAsync(_fixture.CreateTestEvents()).WithTimeout();
			var ex = await Assert.ThrowsAsync<WrongExpectedVersionException>(() =>
				transaction.CommitAsync().WithTimeout());
			Assert.False(ex.ExpectedVersion.HasValue);
			Assert.False(ex.ActualVersion.HasValue);
			//Assert.Equal(ExpectedVersion.StreamExists, ex.ExpectedVersion); TODO JPB seems like a bug?
			//Assert.Equal(ExpectedVersion.NoStream, ex.ActualVersion);
		}
	}
}
