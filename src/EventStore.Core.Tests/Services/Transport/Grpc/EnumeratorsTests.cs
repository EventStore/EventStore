using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using EventStore.Core.Bus;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.TransactionLog;
using EventStore.Core.TransactionLog;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Transport.Grpc;

[TestFixture]
public class EnumeratorsTests {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_all_from_start<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
		private readonly List<Guid> _eventIds = new();

		protected override void Given() {
			EnableReadAll();
			_eventIds.Add(WriteEvent("test-stream", "type1", "{}", "{Data: 1}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type2", "{}", "{Data: 2}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type3", "{}", "{Data: 3}").Item1.EventId);
		}

		[Test]
		public async Task should_receive_live_caught_up_message_after_reading_existing_events() {
			await using var sub = CreateAllSubscription<TLogFormat, TStreamId>(_publisher, Position.Start);

			Assert.True(await sub.GetNext() is SubscriptionConfirmation);
			Assert.AreEqual(Uuid.FromGuid(_eventIds[0]), ((Event)await sub.GetNext()).Id);
			Assert.AreEqual(Uuid.FromGuid(_eventIds[1]), ((Event)await sub.GetNext()).Id);
			Assert.AreEqual(Uuid.FromGuid(_eventIds[2]), ((Event)await sub.GetNext()).Id);
			Assert.True(await sub.GetNext() is CaughtUp);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_all_from_end<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
		protected override void Given() {
			EnableReadAll();
			WriteEvent("test-stream", "type1", "{}", "{Data: 1}");
			WriteEvent("test-stream", "type2", "{}", "{Data: 2}");
			WriteEvent("test-stream", "type3", "{}", "{Data: 3}");
		}

		[Test]
		public async Task should_receive_live_caught_up_message_immediately() {
			await using var sub = CreateAllSubscription<TLogFormat, TStreamId>(_publisher, Position.End);

			Assert.True(await sub.GetNext() is SubscriptionConfirmation);
			Assert.True(await sub.GetNext() is CaughtUp);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_filtered_all_from_start_<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
		private readonly List<Guid> _eventIds = new();

		protected override void Given() {
			EnableReadAll();
			_eventIds.Add(WriteEvent("test-stream", "type1", "{}", "{Data: 1}").Item1.EventId);
			WriteEvent("test-stream", "type2", "{}", "{Data: 2}");
			WriteEvent("test-stream", "type3", "{}", "{Data: 3}");
		}

		[Test]
		public async Task should_receive_live_caught_up_message_after_reading_existing_events() {
			await using var sub = CreateAllSubscriptionFiltered<TLogFormat, TStreamId>(
				_publisher, Position.Start, EventFilter.EventType.Prefixes(false, "type1"));

			Assert.True(await sub.GetNext() is SubscriptionConfirmation);
			Assert.AreEqual(Uuid.FromGuid(_eventIds[0]), ((Event)await sub.GetNext()).Id);
			Assert.True(await sub.GetNext() is CaughtUp);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_filtered_all_from_end_<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
		protected override void Given() {
			EnableReadAll();
			WriteEvent("test-stream", "type1", "{}", "{Data: 1}");
			WriteEvent("test-stream", "type2", "{}", "{Data: 2}");
			WriteEvent("test-stream", "type3", "{}", "{Data: 3}");
		}

		[Test]
		public async Task should_receive_live_caught_up_message_immediately() {
			await using var sub = CreateAllSubscriptionFiltered<TLogFormat, TStreamId>(
				_publisher, Position.End, EventFilter.EventType.Prefixes(false, "type1"));

			Assert.True(await sub.GetNext() is SubscriptionConfirmation);
			Assert.True(await sub.GetNext() is CaughtUp);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_stream_from_start_<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
		private readonly List<Guid> _eventIds = new();

		protected override void Given() {
			EnableReadAll();
			_eventIds.Add(WriteEvent("test-stream1", "type1", "{}", "{Data: 1}").Item1.EventId);
			WriteEvent("test-stream2", "type2", "{}", "{Data: 2}");
			WriteEvent("test-stream3", "type3", "{}", "{Data: 3}");
		}

		[Test]
		public async Task should_receive_live_caught_up_message_after_reading_existing_events() {
			await using var sub = CreateStreamSubscription<TStreamId>(
				_publisher, streamName: "test-stream1");

			Assert.True(await sub.GetNext() is SubscriptionConfirmation);
			Assert.AreEqual(Uuid.FromGuid(_eventIds[0]), ((Event)await sub.GetNext()).Id);
			Assert.True(await sub.GetNext() is CaughtUp);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_stream_from_end<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
		private readonly List<Guid> _eventIds = new();

		protected override void Given() {
			EnableReadAll();
			_eventIds.Add(WriteEvent("test-stream1", "type1", "{}", "{Data: 1}").Item1.EventId);
			WriteEvent("test-stream2", "type2", "{}", "{Data: 2}");
			WriteEvent("test-stream3", "type3", "{}", "{Data: 3}");
		}

		[Test]
		public async Task should_receive_live_caught_up_message_immediately() {
			await using var enumerator = CreateStreamSubscription<TStreamId>(
				_publisher, streamName: "test-stream1", StreamRevision.End);

			Assert.True(await enumerator.GetNext() is SubscriptionConfirmation);
			Assert.True(await enumerator.GetNext() is CaughtUp);
		}
	}

	public static SubscriptionWrapper CreateAllSubscription<TLogFormat, TStreamId>(
		IPublisher publisher,
		Position startPosition) {

		return new SubscriptionWrapper(new Enumerators.AllSubscription(
			bus: publisher,
			expiryStrategy: new DefaultExpiryStrategy(),
			startPosition: startPosition,
			resolveLinks: false,
			user: SystemAccounts.System,
			requiresLeader: false,
			readIndex: new FakeReadIndex<TLogFormat, TStreamId>(_ => false, null),
			uuidOption: new ReadReq.Types.Options.Types.UUIDOption(),
			tracker: ITransactionFileTracker.NoOp,
			cancellationToken: CancellationToken.None));
	}

	public static SubscriptionWrapper CreateAllSubscriptionFiltered<TLogFormat, TStreamId>(
		IPublisher publisher,
		Position startPosition,
		IEventFilter eventFilter = null) {

		return new SubscriptionWrapper(new Enumerators.AllSubscriptionFiltered(
			bus: publisher,
			expiryStrategy: new DefaultExpiryStrategy(),
			startPosition: startPosition,
			resolveLinks: false,
			eventFilter: eventFilter,
			user: SystemAccounts.System,
			requiresLeader: false,
			readIndex: new FakeReadIndex<TLogFormat, TStreamId>(_ => false, null),
			maxSearchWindow: null,
			checkpointIntervalMultiplier: 1,
			uuidOption: new ReadReq.Types.Options.Types.UUIDOption(),
			tfTracker: ITransactionFileTracker.NoOp,
			cancellationToken: CancellationToken.None));
	}

	public static SubscriptionWrapper CreateStreamSubscription<TStreamId>(
		IPublisher publisher,
		string streamName,
		StreamRevision? startRevision = null) {

		return new SubscriptionWrapper(new Enumerators.StreamSubscription<TStreamId>(
			bus: publisher,
			expiryStrategy: new DefaultExpiryStrategy(),
			streamName: streamName,
			startRevision: startRevision,
			resolveLinks: false,
			user: SystemAccounts.System,
			requiresLeader: false,
			uuidOption: new ReadReq.Types.Options.Types.UUIDOption(),
			cancellationToken: CancellationToken.None));
	}

	public record SubscriptionResponse { }
	public record Event(Uuid Id) : SubscriptionResponse { }
	public record SubscriptionConfirmation() : SubscriptionResponse { }
	public record CaughtUp : SubscriptionResponse { }

	public class SubscriptionWrapper : IAsyncDisposable {
		private readonly IAsyncEnumerator<ReadResp> _enumerator;

		public SubscriptionWrapper(IAsyncEnumerator<ReadResp> enumerator) {
			_enumerator = enumerator;
		}

		public ValueTask DisposeAsync() => _enumerator.DisposeAsync();

		public async Task<SubscriptionResponse> GetNext() {
			if (!await _enumerator.MoveNextAsync().ConfigureAwait(false)) {
				throw new Exception("No more items in enumerator");
			}

			var resp = _enumerator.Current;
			return resp.ContentCase switch {
				ReadResp.ContentOneofCase.Event => new Event(Uuid.FromDto(resp.Event.Event.Id)),
				ReadResp.ContentOneofCase.Confirmation => new SubscriptionConfirmation(),
				ReadResp.ContentOneofCase.CaughtUp => new CaughtUp(),
				_ => throw new ArgumentOutOfRangeException(nameof(resp), resp.ContentCase, null),
			};
		}
	}
}
