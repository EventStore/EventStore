using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Operations {
	public class scavenge : IClassFixture<scavenge.Fixture> {
		private readonly Fixture _fixture;

		public scavenge(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task start() {
			var result = await _fixture.OperationsClient.StartScavengeAsync(userCredentials: TestCredentials.Root);
			Assert.Equal(DatabaseScavengeResult.Started(result.ScavengeId), result);
		}

		[Fact]
		public async Task start_without_credentials_throws() {
			await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.OperationsClient.StartScavengeAsync());
		}

		[Theory, InlineData(0), InlineData(-1), InlineData(int.MinValue)]
		public async Task start_with_thread_count_le_one_throws(int threadCount) {
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				_fixture.OperationsClient.StartScavengeAsync(
					threadCount: threadCount));
			Assert.Equal(nameof(threadCount), ex.ParamName);
		}

		[Theory, InlineData(-1), InlineData(-2), InlineData(int.MinValue)]
		public async Task start_with_start_from_chunk_lt_zero_throws(int startFromChunk) {
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
				_fixture.OperationsClient.StartScavengeAsync(
					startFromChunk: startFromChunk));
			Assert.Equal(nameof(startFromChunk), ex.ParamName);
		}

		[Fact(Skip = "Scavenge on an empty database finishes too quickly")]
		public async Task stop() {
			var startResult = await _fixture.OperationsClient.StartScavengeAsync(userCredentials: TestCredentials.Root);
			var stopResult = await _fixture.OperationsClient
				.StopScavengeAsync(startResult.ScavengeId, TestCredentials.Root);
			Assert.Equal(DatabaseScavengeResult.Stopped(startResult.ScavengeId), stopResult);
		}

		[Fact]
		public async Task stop_when_no_scavenge_is_running() {
			var scavengeId = Guid.NewGuid().ToString();
			var ex = await Assert.ThrowsAsync<ScavengeNotFoundException>(() =>
				_fixture.OperationsClient.StopScavengeAsync(scavengeId, TestCredentials.Root));
			Assert.Null(ex.ScavengeId);
		}

		[Fact]
		public async Task stop_without_credentials_throws() {
			await Assert.ThrowsAsync<AccessDeniedException>(() =>
				_fixture.OperationsClient.StopScavengeAsync(Guid.NewGuid().ToString()));
		}

		[Fact]
		public async Task stop_with_null_scavenge_id_throws() {
			string scavengeId = null;
			var ex = await Assert.ThrowsAsync<ArgumentNullException>(() =>
				_fixture.OperationsClient.StopScavengeAsync(scavengeId));

			Assert.Equal(nameof(scavengeId), ex.ParamName);
		}

		public class Fixture : EventStoreOperationsGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
