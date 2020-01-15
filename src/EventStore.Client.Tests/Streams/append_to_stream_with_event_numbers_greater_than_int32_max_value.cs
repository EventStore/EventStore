using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	public class append_to_stream_with_event_numbers_greater_than_int32_max_value : IAsyncLifetime {
		private readonly Fixture _fixture;
		private const string Stream = nameof(append_to_stream_with_event_numbers_greater_than_int32_max_value);

		public append_to_stream_with_event_numbers_greater_than_int32_max_value() {
			_fixture = new Fixture();
		}

		[Fact]
		public async Task should_be_able_to_append_to_stream() {
			var expected = _fixture.CreateTestEvents().ToArray();
			var writeResult = await _fixture.Client.AppendToStreamAsync(Stream,
				new StreamRevision(int.MaxValue + 5L), expected);
			Assert.Equal(int.MaxValue + 6L, writeResult.NextExpectedVersion);

			var actual = await _fixture.Client.ReadStreamForwardsAsync(Stream,
					new StreamRevision(int.MaxValue + 6L), 1, false, userCredentials: TestCredentials.Root)
				.SingleAsync();
			Assert.Equal(expected.Single().EventId, actual.Event.EventId);
		}

		[Fact]
		public async Task should_throw_wrong_expected_version_when_version_incorrect() {
			await Assert.ThrowsAsync<WrongExpectedVersionException>(
				() => _fixture.Client.AppendToStreamAsync(Stream, new StreamRevision(int.MaxValue + 12L),
					_fixture.CreateTestEvents()));
		}


		public class Fixture : StreamRevisionGreaterThanIntMaxValueFixture {
			public Fixture() : base((checkpoint, writer) => {
				foreach (var offset in Enumerable.Range(1, 5).Select(Convert.ToInt64)) {
					WriteSingleEvent(
						checkpoint,
						writer,
						Stream,
						int.MaxValue + offset,
						Encoding.UTF8.GetBytes(new string('.', 3000)));
				}
			}) {
			}

			protected override string StreamName => Stream;
			protected override Task When() => Task.CompletedTask;
		}

		public Task InitializeAsync() => _fixture.InitializeAsync();
		public Task DisposeAsync() => _fixture.DisposeAsync();
	}
}
