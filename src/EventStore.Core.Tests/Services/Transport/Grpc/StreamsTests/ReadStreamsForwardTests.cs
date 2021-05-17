using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Core.Tests.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Grpc.StreamsTests {
	[TestFixture]
	public class ReadStreamsForwardTests {
		private static ClaimsPrincipal CreateTestUser() {
			return new ClaimsPrincipal(new ClaimsIdentity(
				new[] {
					new Claim(ClaimTypes.Name, "admin"),
				}, "ES-Test"));
		}

		private static List<EventData> CreateEvents(int count) {
			var events = new List<EventData>();
			for (var i = 0; i < count; i++) {
				events.Add(new EventData(Guid.NewGuid(), "test", false, new byte[] { }, null));
			}

			return events;
		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(long))]
		public class when_reading_forward_from_stream_that_has_been_truncated<TLogFormat, TStreamId>
			: SpecificationWithMiniNode<TLogFormat, TStreamId> {
			private readonly string _streamName = Guid.NewGuid().ToString();
			private readonly List<Data.ResolvedEvent> _responses = new List<Data.ResolvedEvent>();
			private const ulong _maxCount = 10;

			protected override async Task Given() {
				await _conn.AppendToStreamAsync(_streamName, ExpectedVersion.Any, CreateEvents(100));
				var metadata = StreamMetadata.Build().SetTruncateBefore(81);
				await _conn.SetStreamMetadataAsync(_streamName, ExpectedVersion.Any, metadata);
			}

			protected override async Task When() {
				var enumerator = new Enumerators.ReadStreamForwards(_node.Node.MainQueue, _streamName,
					StreamRevision.Start,
					_maxCount, false, CreateTestUser(), false, DateTime.Now.AddMinutes(5), e => {
						Assert.Fail($"Failed to read: {e}");
						return Task.CompletedTask;
					}, CancellationToken.None);

				while (await enumerator.MoveNextAsync().ConfigureAwait(false)) {
					_responses.Add(enumerator.Current);
				}
			}

			[Test]
			public void should_not_receive_null_events() {
				Assert.False(_responses.Any(x => x.Event is null));
			}

			[Test]
			public void should_read_a_number_of_events_equal_to_the_max_count() {
				Assert.AreEqual(_maxCount, _responses.Count);
			}

			[Test]
			public void should_start_from_the_truncation_position() {
				Assert.AreEqual(81, _responses[0].OriginalEventNumber);
				Assert.AreEqual(90, _responses[^1].OriginalEventNumber);
			}
		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(long))]
		public class when_reading_forward_from_the_start_of_the_stream<TLogFormat, TStreamId>
			: SpecificationWithMiniNode<TLogFormat, TStreamId> {
			private readonly string _streamName = Guid.NewGuid().ToString();
			private readonly List<Data.ResolvedEvent> _responses = new List<Data.ResolvedEvent>();
			private const ulong _maxCount = 50;

			protected override async Task Given() {
				await _conn.AppendToStreamAsync(_streamName, ExpectedVersion.Any, CreateEvents(100));
			}

			protected override async Task When() {
				var enumerator = new Enumerators.ReadStreamForwards(_node.Node.MainQueue, _streamName,
					StreamRevision.Start,
					_maxCount, false, CreateTestUser(), false, DateTime.Now.AddMinutes(5), e => {
						Assert.Fail($"Failed to read: {e}");
						return Task.CompletedTask;
					}, CancellationToken.None);

				while (await enumerator.MoveNextAsync().ConfigureAwait(false)) {
					_responses.Add(enumerator.Current);
				}
			}

			[Test]
			public void should_not_receive_null_events() {
				Assert.False(_responses.Any(x => x.Event is null));
			}

			[Test]
			public void should_read_a_number_of_events_equal_to_the_max_count() {
				Assert.AreEqual(_maxCount, _responses.Count);
			}

			[Test]
			public void should_read_the_correct_events() {
				Assert.AreEqual(50, _responses.Count);
				Assert.AreEqual(49, _responses[^1].OriginalEventNumber);
			}
		}

		[TestFixture(typeof(LogFormat.V2), typeof(string))]
		[TestFixture(typeof(LogFormat.V3), typeof(long))]
		public class when_reading_forward_from_stream_with_no_events_after_position<TLogFormat, TStreamId>
			: SpecificationWithMiniNode<TLogFormat, TStreamId> {
			private readonly string _streamName = Guid.NewGuid().ToString();
			private readonly List<Data.ResolvedEvent> _responses = new List<Data.ResolvedEvent>();
			private const ulong _maxCount = 50;

			protected override async Task Given() {
				await _conn.AppendToStreamAsync(_streamName, ExpectedVersion.Any, CreateEvents(10));
			}

			protected override async Task When() {
				var enumerator = new Enumerators.ReadStreamForwards(_node.Node.MainQueue, _streamName,
					StreamRevision.FromInt64(11),
					_maxCount, false, CreateTestUser(), false, DateTime.Now.AddMinutes(5), e => {
						Assert.Fail($"Failed to read: {e}");
						return Task.CompletedTask;
					}, CancellationToken.None);

				while (await enumerator.MoveNextAsync().ConfigureAwait(false)) {
					_responses.Add(enumerator.Current);
				}
			}

			[Test]
			public void should_not_receive_events() {
				Assert.IsEmpty(_responses);
			}
		}
	}
}
