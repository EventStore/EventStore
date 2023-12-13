using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Common;
using EventStore.Core.Services.Transport.Enumerators;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.Tests.Helpers;
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
			Assert.AreEqual(_eventIds[0], ((Event)await sub.GetNext()).Id);
			Assert.AreEqual(_eventIds[1], ((Event)await sub.GetNext()).Id);
			Assert.AreEqual(_eventIds[2], ((Event)await sub.GetNext()).Id);
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
	public class subscribe_all_from_position<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
		private readonly List<Guid> _eventIds = new();
		private TFPos _subscribeFrom;

		protected override void Given() {
			EnableReadAll();
			WriteEvent("test-stream", "type1", "{}", "{Data: 1}");
			WriteEvent("test-stream", "type2", "{}", "{Data: 2}");
			(_, _subscribeFrom) = WriteEvent("test-stream", "type3", "{}", "{Data: 3}");
			_eventIds.Add(WriteEvent("test-stream", "type4", "{}", "{Data: 4}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type5", "{}", "{Data: 5}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type6", "{}", "{Data: 6}").Item1.EventId);
		}

		[Test]
		public async Task should_receive_events_after_start_position() {
			await using var sub = CreateAllSubscription<TLogFormat, TStreamId>(
				_publisher,
				new Position((ulong)_subscribeFrom.CommitPosition, (ulong)_subscribeFrom.PreparePosition));

			Assert.True(await sub.GetNext() is SubscriptionConfirmation);
			Assert.AreEqual(_eventIds[0], ((Event)await sub.GetNext()).Id);
			Assert.AreEqual(_eventIds[1], ((Event)await sub.GetNext()).Id);
			Assert.AreEqual(_eventIds[2], ((Event)await sub.GetNext()).Id);
			Assert.True(await sub.GetNext() is CaughtUp);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_filtered_all_from_start<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
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
			Assert.AreEqual(_eventIds[0], ((Event)await sub.GetNext()).Id);
			Assert.True(await sub.GetNext() is CaughtUp);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class subscribe_filtered_all_from_end<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
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
	public class subscribe_filtered_all_from_position<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
		private readonly List<Guid> _eventIds = new();
		private TFPos _subscribeFrom;

		protected override void Given() {
			EnableReadAll();
			WriteEvent("test-stream", "theType", "{}", "{Data: 1}");
			WriteEvent("test-stream", "type2", "{}", "{Data: 2}");
			(_, _subscribeFrom) = WriteEvent("test-stream", "theType", "{}", "{Data: 2}");
			_eventIds.Add(WriteEvent("test-stream", "theType", "{}", "{Data: 3}").Item1.EventId);
			WriteEvent("test-stream", "type3", "{}", "{Data: 3}");
			WriteEvent("test-stream", "type4", "{}", "{Data: 4}");
		}

		[Test]
		public async Task should_receive_matching_events_after_start_position() {
			await using var sub = CreateAllSubscriptionFiltered<TLogFormat, TStreamId>(
				_publisher,
				new Position((ulong)_subscribeFrom.CommitPosition, (ulong)_subscribeFrom.PreparePosition),
				EventFilter.EventType.Prefixes(false, "theType"));

			Assert.True(await sub.GetNext() is SubscriptionConfirmation);
			Assert.AreEqual(_eventIds[0], ((Event)await sub.GetNext()).Id);
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
			Assert.AreEqual(_eventIds[0], ((Event)await sub.GetNext()).Id);
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

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class read_all_forwards<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
		private readonly List<Guid> _eventIds = new();

		protected override void Given() {
			EnableReadAll();
			_eventIds.Add(WriteEvent("test-stream", "type1", "{}", "{Data: 1}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type2", "{}", "{Data: 2}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type3", "{}", "{Data: 3}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream-all", "type3", "{}", "{Data: 3}").Item1.EventId);
		}

		[Test]
		public async Task should_read_all_the_events() {
			await using var enumerator = ReadAllForwards(_publisher, Position.Start);

			Assert.AreEqual(_eventIds[0], ((Event)await enumerator.GetNext()).Id);
			Assert.AreEqual(_eventIds[1], ((Event)await enumerator.GetNext()).Id);
			Assert.AreEqual(_eventIds[2], ((Event)await enumerator.GetNext()).Id);
			Assert.AreEqual(_eventIds[3], ((Event)await enumerator.GetNext()).Id);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class read_all_backwards<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
		private readonly List<Guid> _eventIds = new();

		protected override void Given() {
			EnableReadAll();
			_eventIds.Add(WriteEvent("test-stream", "type1", "{}", "{Data: 1}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type2", "{}", "{Data: 2}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type3", "{}", "{Data: 3}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream-all", "type3", "{}", "{Data: 3}").Item1.EventId);
		}

		[Test]
		public async Task should_read_all_the_events() {
			await using var enumerator = ReadAllBackwards(_publisher, Position.End);

			Assert.AreEqual(_eventIds[3], ((Event)await enumerator.GetNext()).Id);
			Assert.AreEqual(_eventIds[2], ((Event)await enumerator.GetNext()).Id);
			Assert.AreEqual(_eventIds[1], ((Event)await enumerator.GetNext()).Id);
			Assert.AreEqual(_eventIds[0], ((Event)await enumerator.GetNext()).Id);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class read_all_forwards_filtered<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
		private readonly List<Guid> _eventIds = new();

		protected override void Given() {
			EnableReadAll();
			_eventIds.Add(WriteEvent("test-stream", "type1", "{}", "{Data: 1}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type2", "{}", "{Data: 2}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type3", "{}", "{Data: 3}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream-all", "type3", "{}", "{Data: 3}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream-all", "type2", "{}", "{Data: 4}").Item1.EventId);
		}

		[Test]
		public async Task should_read_all_the_events() {
			await using var enumerator =
				ReadAllForwardsFiltered<TLogFormat, TStreamId>(_publisher, Position.Start, EventFilter.EventType.Prefixes(false, "type2"));

			Assert.AreEqual(_eventIds[1], ((Event)await enumerator.GetNext()).Id);
			Assert.AreEqual(_eventIds[4], ((Event)await enumerator.GetNext()).Id);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class read_all_backwards_filtered<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
		private readonly List<Guid> _eventIds = new();

		protected override void Given() {
			EnableReadAll();
			_eventIds.Add(WriteEvent("test-stream", "type1", "{}", "{Data: 1}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type2", "{}", "{Data: 2}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type3", "{}", "{Data: 3}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream-all", "type3", "{}", "{Data: 3}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream-all", "type2", "{}", "{Data: 4}").Item1.EventId);
		}

		[Test]
		public async Task should_read_all_the_filtered_events() {
			await using var enumerator = ReadAllBackwardsFiltered<TLogFormat, TStreamId>(_publisher, Position.End,
				EventFilter.EventType.Prefixes(false, "type2"));

			Assert.AreEqual(_eventIds[4], ((Event)await enumerator.GetNext()).Id);
			Assert.AreEqual(_eventIds[1], ((Event)await enumerator.GetNext()).Id);

		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class read_stream_forwards<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
		private readonly List<Guid> _eventIds = new();

		protected override void Given() {
			EnableReadAll();
			_eventIds.Add(WriteEvent("test-stream", "type1", "{}", "{Data: 1}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream-all", "type1", "{}", "{Data: 1}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type2", "{}", "{Data: 2}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type3", "{}", "{Data: 3}").Item1.EventId);
		}

		[Test]
		public async Task should_read_forwards_from_a_particular_stream() {
			await using var enumerator = ReadStreamForwards(_publisher, "test-stream");

			Assert.AreEqual(_eventIds[0], ((Event)await enumerator.GetNext()).Id);
			Assert.AreEqual(_eventIds[2], ((Event)await enumerator.GetNext()).Id);
			Assert.AreEqual(_eventIds[3], ((Event)await enumerator.GetNext()).Id);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class read_stream_backwards<TLogFormat, TStreamId> : TestFixtureWithExistingEvents<TLogFormat, TStreamId> {
		private readonly List<Guid> _eventIds = new();

		protected override void Given() {
			EnableReadAll();
			_eventIds.Add(WriteEvent("test-stream", "type1", "{}", "{Data: 1}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream-all", "type1", "{}", "{Data: 1}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type2", "{}", "{Data: 2}").Item1.EventId);
			_eventIds.Add(WriteEvent("test-stream", "type3", "{}", "{Data: 3}").Item1.EventId);
		}

		[Test]
		public async Task should_read_backwards_from_a_particular_stream() {
			await using var enumerator = ReadStreamBackwards(_publisher, "test-stream");

			Assert.AreEqual(_eventIds[3], ((Event)await enumerator.GetNext()).Id);
			Assert.AreEqual(_eventIds[2], ((Event)await enumerator.GetNext()).Id);
			Assert.AreEqual(_eventIds[0], ((Event)await enumerator.GetNext()).Id);
		}
	}

	public static EnumeratorWrapper CreateAllSubscription<TLogFormat, TStreamId>(
		IPublisher publisher,
		Position startPosition) {

		return new EnumeratorWrapper(new Enumerator.AllSubscription(
			bus: publisher,
			expiryStrategy: new DefaultExpiryStrategy(),
			startPosition: startPosition,
			resolveLinks: false,
			user: SystemAccounts.System,
			requiresLeader: false,
			cancellationToken: CancellationToken.None));
	}

	public static EnumeratorWrapper CreateAllSubscriptionFiltered<TLogFormat, TStreamId>(
		IPublisher publisher,
		Position startPosition,
		IEventFilter eventFilter = null) {

		return new EnumeratorWrapper(new Enumerator.AllSubscriptionFiltered(
			bus: publisher,
			expiryStrategy: new DefaultExpiryStrategy(),
			startPosition: startPosition,
			resolveLinks: false,
			eventFilter: eventFilter,
			user: SystemAccounts.System,
			requiresLeader: false,
			maxSearchWindow: null,
			checkpointIntervalMultiplier: 1,
			cancellationToken: CancellationToken.None));
	}

	public static EnumeratorWrapper CreateStreamSubscription<TStreamId>(
		IPublisher publisher,
		string streamName,
		StreamRevision? startRevision = null) {

		return new EnumeratorWrapper(new Enumerator.StreamSubscription<TStreamId>(
			bus: publisher,
			expiryStrategy: new DefaultExpiryStrategy(),
			streamName: streamName,
			startRevision: startRevision,
			resolveLinks: false,
			user: SystemAccounts.System,
			requiresLeader: false,
			cancellationToken: CancellationToken.None));
	}

	public static EnumeratorWrapper ReadAllForwards(IPublisher publisher, Position position) {
		return new EnumeratorWrapper(new Enumerator.ReadAllForwards(
			bus: publisher,
			position: position,
			maxCount: 10,
			resolveLinks: false,
			user: SystemAccounts.System,
			requiresLeader: false,
			deadline: DateTime.Now,
			cancellationToken: CancellationToken.None));
	}

	public static EnumeratorWrapper ReadAllBackwards(IPublisher publisher, Position position) {
		return new EnumeratorWrapper(new Enumerator.ReadAllBackwards(
			 bus: publisher,
			 position: position,
			 maxCount: 10,
			 resolveLinks: false,
			 user: SystemAccounts.System,
			 requiresLeader: false,
			 deadline: DateTime.Now,
			 cancellationToken: CancellationToken.None));
	}

	public static EnumeratorWrapper ReadAllForwardsFiltered<TLogFormat, TStreamId>(IPublisher publisher, Position position, IEventFilter filter = null) {
		return new EnumeratorWrapper(new Enumerator.ReadAllForwardsFiltered(
			bus: publisher,
			position: position,
			maxCount: 10,
			resolveLinks: false,
			eventFilter: filter,
			user: SystemAccounts.System,
			requiresLeader: false,
			maxSearchWindow: null,
			deadline: DateTime.Now,
			cancellationToken: CancellationToken.None));
	}

	public static EnumeratorWrapper ReadAllBackwardsFiltered<TLogFormat, TStreamId>(IPublisher publisher, Position position, IEventFilter filter = null) {
		return new EnumeratorWrapper(new Enumerator.ReadAllBackwardsFiltered(
			bus: publisher,
			position: position,
			maxCount: 10,
			resolveLinks: false,
			eventFilter: filter,
			user: SystemAccounts.System,
			requiresLeader: false,
			maxSearchWindow: null,
			deadline: DateTime.Now,
			cancellationToken: CancellationToken.None));
	}

	public static EnumeratorWrapper ReadStreamForwards(IPublisher publisher, string streamName) {
		return new EnumeratorWrapper(new Enumerator.ReadStreamForwards(
			bus: publisher,
			streamName: streamName,
			startRevision: StreamRevision.Start,
			maxCount: 10,
			resolveLinks: false,
			user: SystemAccounts.System,
			requiresLeader: false,
			deadline: DateTime.Now,
			compatibility: 1,
			cancellationToken: CancellationToken.None));
	}

	public static EnumeratorWrapper ReadStreamBackwards(IPublisher publisher, string streamName) {
		return new EnumeratorWrapper(new Enumerator.ReadStreamBackwards(
			bus: publisher,
			streamName: streamName,
			startRevision: StreamRevision.End,
			maxCount: 10,
			resolveLinks: false,
			user: SystemAccounts.System,
			requiresLeader: false,
			deadline: DateTime.Now,
			compatibility: 1,
			cancellationToken: CancellationToken.None));
	}

	public record SubscriptionResponse { }
	public record Event(Guid Id) : SubscriptionResponse { }
	public record SubscriptionConfirmation() : SubscriptionResponse { }
	public record CaughtUp : SubscriptionResponse { }

	public class EnumeratorWrapper : IAsyncDisposable {
		private readonly IAsyncEnumerator<ReadResponse> _enumerator;

		public EnumeratorWrapper(IAsyncEnumerator<ReadResponse> enumerator) {
			_enumerator = enumerator;
		}

		public ValueTask DisposeAsync() => _enumerator.DisposeAsync();

		public async Task<SubscriptionResponse> GetNext() {
			if (!await _enumerator.MoveNextAsync()) {
				throw new Exception("No more items in enumerator");
			}

			var resp = _enumerator.Current;

			return resp switch {
				ReadResponse.EventReceived eventReceived => new Event(eventReceived.Event.Event.EventId),
				ReadResponse.SubscriptionConfirmed => new SubscriptionConfirmation(),
				ReadResponse.SubscriptionCaughtUp => new CaughtUp(),
				_ => throw new ArgumentOutOfRangeException(nameof(resp), resp, null),
			};
		}
	}
}
