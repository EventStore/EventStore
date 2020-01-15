using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "LongRunning")]
	public class appending_to_implicitly_created_stream
		: IClassFixture<appending_to_implicitly_created_stream.Fixture> {
		private readonly Fixture _fixture;

		public appending_to_implicitly_created_stream(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task sequence_0em1_1e0_2e1_3e2_4e3_5e4_0em1_idempotent() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents(6).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);
			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events.Take(1));

			var count = await _fixture.Client
				.ReadStreamForwardsAsync(stream, StreamRevision.Start, (ulong)events.Length + 1).CountAsync();

			Assert.Equal(events.Length, count);
		}

		[Fact]
		public async Task sequence_0em1_1e0_2e1_3e2_4e3_4e4_0any_idempotent() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents(6).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);
			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, events.Take(1));

			var count = await _fixture.Client
				.ReadStreamForwardsAsync(stream, StreamRevision.Start, (ulong)events.Length + 1).CountAsync();

			Assert.Equal(events.Length, count);
		}

		[Fact]
		public async Task sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e5_non_idempotent() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents(6).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);
			await _fixture.Client.AppendToStreamAsync(stream, new StreamRevision(5), events.Take(1));

			var count = await _fixture.Client
				.ReadStreamForwardsAsync(stream, StreamRevision.Start, (ulong)events.Length + 2).CountAsync();

			Assert.Equal(events.Length + 1, count);
		}

		[Fact]
		public async Task sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e6_wev() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents(6).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);

			await Assert.ThrowsAsync<WrongExpectedVersionException>(
				() => _fixture.Client.AppendToStreamAsync(stream, new StreamRevision(6), events.Take(1)));
		}

		[Fact]
		public async Task sequence_0em1_1e0_2e1_3e2_4e3_5e4_0e4_wev() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents(6).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);

			await Assert.ThrowsAsync<WrongExpectedVersionException>(
				() => _fixture.Client.AppendToStreamAsync(stream, new StreamRevision(4), events.Take(1)));
		}

		[Fact]
		public async Task sequence_0em1_0e0_non_idempotent() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents().ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);
			await _fixture.Client.AppendToStreamAsync(stream, StreamRevision.Start, events.Take(1));

			var count = await _fixture.Client
				.ReadStreamForwardsAsync(stream, StreamRevision.Start, (ulong)events.Length + 2).CountAsync();

			Assert.Equal(events.Length + 1, count);
		}

		[Fact]
		public async Task sequence_0em1_0any_idempotent() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents().ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);
			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, events.Take(1));

			var count = await _fixture.Client
				.ReadStreamForwardsAsync(stream, StreamRevision.Start, (ulong)events.Length + 1).CountAsync();

			Assert.Equal(events.Length, count);
		}

		[Fact]
		public async Task sequence_0em1_0em1_idempotent() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents().ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);
			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events.Take(1));

			var count = await _fixture.Client
				.ReadStreamForwardsAsync(stream, StreamRevision.Start, (ulong)events.Length + 1).CountAsync();

			Assert.Equal(events.Length, count);
		}

		[Fact]
		public async Task sequence_0em1_1e0_2e1_1any_1any_idempotent() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents(3).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);
			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, events.Skip(1).Take(1));
			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, events.Skip(1).Take(1));

			var count = await _fixture.Client
				.ReadStreamForwardsAsync(stream, StreamRevision.Start, (ulong)events.Length + 1).CountAsync();

			Assert.Equal(events.Length, count);
		}

		[Fact]
		public async Task sequence_S_0em1_1em1_E_S_0em1_E_idempotent() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents(2).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);
			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events.Take(1));

			var count = await _fixture.Client
				.ReadStreamForwardsAsync(stream, StreamRevision.Start, (ulong)events.Length + 1).CountAsync();

			Assert.Equal(events.Length, count);
		}

		[Fact]
		public async Task sequence_S_0em1_1em1_E_S_0any_E_idempotent() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents(2).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);
			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, events.Take(1));

			var count = await _fixture.Client
				.ReadStreamForwardsAsync(stream, StreamRevision.Start, (ulong)events.Length + 1).CountAsync();

			Assert.Equal(events.Length, count);
		}

		[Fact]
		public async Task sequence_S_0em1_1em1_E_S_1e0_E_idempotent() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents(2).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);

			await _fixture.Client.AppendToStreamAsync(stream, StreamRevision.Start, events.Skip(1));

			var count = await _fixture.Client
				.ReadStreamForwardsAsync(stream, StreamRevision.Start, (ulong)events.Length + 1).CountAsync();

			Assert.Equal(events.Length, count);
		}

		[Fact]
		public async Task sequence_S_0em1_1em1_E_S_1any_E_idempotent() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents(2).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events);
			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.Any, events.Skip(1).Take(1));

			var count = await _fixture.Client
				.ReadStreamForwardsAsync(stream, StreamRevision.Start, (ulong)events.Length + 1).CountAsync();

			Assert.Equal(events.Length, count);
		}

		[Fact]
		public async Task sequence_S_0em1_1em1_E_S_0em1_1em1_2em1_E_idempotancy_fail() {
			var stream = _fixture.GetStreamName();

			var events = _fixture.CreateTestEvents(3).ToArray();

			await _fixture.Client.AppendToStreamAsync(stream, AnyStreamRevision.NoStream, events.Take(2));

			await Assert.ThrowsAsync<WrongExpectedVersionException>(
				() => _fixture.Client.AppendToStreamAsync(
					stream,
					AnyStreamRevision.NoStream,
					events));
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
