using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "Network")]
	public class deleting_stream : IClassFixture<deleting_stream.Fixture> {
		private readonly Fixture _fixture;

		public deleting_stream(Fixture fixture) {
			_fixture = fixture;
		}

		public static IEnumerable<object[]> ExpectedVersionCases() {
			yield return new object[] {AnyStreamRevision.Any, nameof(AnyStreamRevision.Any)};
			yield return new object[] {AnyStreamRevision.NoStream, nameof(AnyStreamRevision.NoStream)};
		}

		[Theory, MemberData(nameof(ExpectedVersionCases))]
		public async Task hard_deleting_a_stream_that_does_not_exist_with_expected_version_does_not_throw(
			AnyStreamRevision expectedVersion, string name) {
			var stream = $"{_fixture.GetStreamName()}_{name}";

			await _fixture.Client.TombstoneAsync(stream, expectedVersion);
		}

		[Theory, MemberData(nameof(ExpectedVersionCases))]
		public async Task soft_deleting_a_stream_that_does_not_exist_with_expected_version_does_not_throw(
			AnyStreamRevision expectedVersion, string name) {
			var stream = $"{_fixture.GetStreamName()}_{name}";

			await _fixture.Client.SoftDeleteAsync(stream, expectedVersion);
		}


		[Fact]
		public async Task hard_deleting_a_stream_that_does_not_exist_with_wrong_expected_version_throws() {
			var stream = _fixture.GetStreamName();

			await Assert.ThrowsAsync<WrongExpectedVersionException>(
				() => _fixture.Client.TombstoneAsync(stream, StreamRevision.Start));
		}

		[Fact]
		public async Task soft_deleting_a_stream_that_does_not_exist_with_wrong_expected_version_throws() {
			var stream = _fixture.GetStreamName();

			await Assert.ThrowsAsync<WrongExpectedVersionException>(
				() => _fixture.Client.SoftDeleteAsync(stream, StreamRevision.Start));
		}

		[Fact]
		public async Task hard_deleting_a_stream_should_return_log_position() {
			var stream = _fixture.GetStreamName();

			var writeResult = await _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents());

			var deleteResult = await _fixture.Client.TombstoneAsync(stream, StreamRevision.FromInt64(writeResult.NextExpectedVersion));

			Assert.True(deleteResult.LogPosition > writeResult.LogPosition);
		}

		[Fact]
		public async Task soft_deleting_a_stream_should_return_log_position() {
			var stream = _fixture.GetStreamName();

			var writeResult = await _fixture.Client.AppendToStreamAsync(
				stream,
				AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents());

			var deleteResult = await _fixture.Client.SoftDeleteAsync(stream, StreamRevision.FromInt64(writeResult.NextExpectedVersion));

			Assert.True(deleteResult.LogPosition > writeResult.LogPosition);
		}

		[Fact]
		public async Task hard_deleting_a_deleted_stream_should_throw() {
			var stream = _fixture.GetStreamName();

			await _fixture.Client.TombstoneAsync(stream, AnyStreamRevision.NoStream);

			await Assert.ThrowsAsync<StreamDeletedException>(
				() => _fixture.Client.TombstoneAsync(stream, AnyStreamRevision.NoStream));
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
