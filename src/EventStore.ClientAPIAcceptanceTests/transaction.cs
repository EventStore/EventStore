using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using Xunit;

namespace EventStore.ClientAPIAcceptanceTests {
	[Collection(nameof(EventStoreClientAPIFixture))]
	public class transaction : EventStoreClientAPITest, IClassFixture<EventStoreClientAPIFixture> {
		private readonly EventStoreClientAPIFixture _fixture;

		public transaction(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(ExpectedVersionTestCases))]
		public async Task expected_version(long expectedVersion, string displayName, bool useSsl) {
			var streamName = $"{GetStreamName()}_{displayName}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync();

			using var transaction = await connection.StartTransactionAsync(streamName, expectedVersion);

			await transaction.WriteAsync(_fixture.CreateTestEvents());
			var result = await transaction.CommitAsync();
			Assert.Equal(0, result.NextExpectedVersion);
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task wrong_expected_version(bool useSsl) {
			var streamName = $"{GetStreamName()}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));

			await connection.ConnectAsync();

			using var transaction = await connection.StartTransactionAsync(streamName, 1);
			await transaction.WriteAsync(_fixture.CreateTestEvents());
			var ex = await Assert.ThrowsAsync<WrongExpectedVersionException>(() => transaction.CommitAsync());
			Assert.False(ex.ExpectedVersion.HasValue);
			Assert.False(ex.ActualVersion.HasValue);
			//Assert.Equal(ExpectedVersion.StreamExists, ex.ExpectedVersion); TODO JPB seems like a bug?
			//Assert.Equal(ExpectedVersion.NoStream, ex.ActualVersion);
		}
	}
}
