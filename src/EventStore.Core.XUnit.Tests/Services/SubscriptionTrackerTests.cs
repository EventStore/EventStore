using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.XUnit.Tests.Metrics;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services;

public class SubscriptionTrackerTests : IDisposable {
	private const string StreamName = "stream";
	private const long EndOfAllStream = 783L;
	private const long EndOfStream = 10L;
	private const long LogPosition = 849L;
	private const long EventNumber = 11L;

	private readonly TestMeterListener<long> _subscriptionPositionListener;
	private readonly TestMeterListener<long> _streamPositionListener;
	private readonly Meter _meter;
	private readonly SubscriptionTracker _sut;
	private readonly Guid _subscriptionId;
	private readonly KeyValuePair<string, object>[] _streamTags;
	private readonly KeyValuePair<string, object>[] _allStreamTags;
	private readonly ResolvedEvent _resolvedEvent;
	private readonly FakeReadIndex _readIndex;
	private readonly TestMeterListener<long> _subscriptionCountListener;

	public SubscriptionTrackerTests() {
		_meter = new Meter("eventstore");

		var metric = new SubscriptionMetric(_meter, "test");
		_streamPositionListener = new TestMeterListener<long>(_meter);
		_subscriptionPositionListener = new TestMeterListener<long>(_meter);
		_subscriptionCountListener = new TestMeterListener<long>(_meter);

		_sut = new SubscriptionTracker(metric);

		_subscriptionId = Guid.NewGuid();

		_streamTags = [new("stream-name", StreamName), new("subscription-id", _subscriptionId)];
		_allStreamTags = [new("stream-name", SystemStreams.AllStream), new("subscription-id", _subscriptionId)];
		_resolvedEvent = ResolvedEvent.ForUnresolvedEvent(
			new EventRecord(EventNumber, LogPosition, Guid.NewGuid(), Guid.NewGuid(), LogPosition, 0, StreamName, 1L,
				DateTime.Now, PrepareFlags.SingleWrite, "-", [], []), LogPosition);
		_readIndex = new FakeReadIndex();
	}

	[Fact]
	public void initial_state() {
		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new[] { new { Value = 0L } }, actual);
	}

	[Fact]
	public void add_subscription_stream() {
		_sut.AddSubscription(_subscriptionId, StreamName, EndOfStream);

		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new[] {
			new { Value = 0L, Tags = _streamTags },
			new { Value = EndOfStream, Tags = _streamTags },
			new { Value = 1L, Tags = Array.Empty<KeyValuePair<string, object>>() }
		}, actual);
	}

	[Fact]
	public void remove_subscription_stream() {
		_sut.AddSubscription(_subscriptionId, StreamName, EndOfStream);
		_sut.RemoveSubscription(_subscriptionId);

		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new[] { new { Value = 0L } }, actual);
	}

	[Fact]
	public void add_subscription_all() {
		_sut.AddSubscription(_subscriptionId, null, EndOfAllStream);

		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new[] {
			new { Value = 0L, Tags = _allStreamTags },
			new { Value = EndOfAllStream, Tags = _allStreamTags },
			new { Value = 1L, Tags = Array.Empty<KeyValuePair<string, object>>() }
		}, actual);
	}

	[Fact]
	public void remove_subscription_all() {
		_sut.AddSubscription(_subscriptionId, null, EndOfAllStream);
		_sut.RemoveSubscription(_subscriptionId);

		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new[] { new { Value = 0L } }, actual);
	}
	[Fact]
	public void event_committed_stream() {
		_sut.AddSubscription(_subscriptionId, StreamName, EndOfStream);
		_sut.UpdateStreamPositions(_readIndex);

		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new[] {
			new { Value = 0L, Tags = _streamTags },
			new { Value = EventNumber, Tags = _streamTags },
			new { Value = 1L, Tags = Array.Empty<KeyValuePair<string, object>>() }
		}, actual);
	}

	[Fact]
	public void event_committed_all() {
		_sut.AddSubscription(_subscriptionId, null, EndOfAllStream);
		_sut.UpdateStreamPositions(_readIndex);

		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new[] {
			new { Value = 0L, Tags = _allStreamTags },
			new { Value = LogPosition, Tags = _allStreamTags },
			new { Value = 1L, Tags = Array.Empty<KeyValuePair<string, object>>() }
		}, actual);
	}

	[Fact]
	public void event_processed_stream() {
		_sut.AddSubscription(_subscriptionId, StreamName, EndOfStream);
		_sut.UpdateStreamPositions(_readIndex);
		_sut.ProcessEvent(_subscriptionId, _resolvedEvent);

		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new[] {
			new { Value = EventNumber, Tags = _streamTags },
			new { Value = EventNumber, Tags = _streamTags },
			new { Value = 1L, Tags = Array.Empty<KeyValuePair<string, object>>() }
		}, actual);
	}

	[Fact]
	public void event_processed_all() {
		_sut.AddSubscription(_subscriptionId, null, EndOfAllStream);
		_sut.UpdateStreamPositions(_readIndex);
		_sut.ProcessEvent(_subscriptionId, _resolvedEvent);

		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new[] {
			new { Value = LogPosition, Tags = _allStreamTags },
			new { Value = LogPosition, Tags = _allStreamTags },
			new { Value = 1L, Tags = Array.Empty<KeyValuePair<string, object>>() }
		}, actual);
	}

	private void Observe() {
		_subscriptionPositionListener.Observe();
		_streamPositionListener.Observe();
		_subscriptionCountListener.Observe();
	}

	private TestMeterListener<long>.TestMeasurement[] RetrieveMeasurements() => [
		.._subscriptionPositionListener.RetrieveMeasurements("test-subscription-position"),
		.._streamPositionListener.RetrieveMeasurements("test-stream-position"),
		.._subscriptionCountListener.RetrieveMeasurements("test-subscription-count")
	];

	public void Dispose() {
		_subscriptionPositionListener?.Dispose();
		_streamPositionListener?.Dispose();
		_meter?.Dispose();
	}

	private class FakeReadIndex : IReadIndex<string> {
		public long LastIndexedPosition => LogPosition;
		public string GetStreamId(string streamName) => streamName;
		public long GetStreamLastEventNumber(string streamId) => EventNumber;

		#region Not Implemented

		public ReadIndexStats GetStatistics() {
			throw new NotImplementedException();
		}

		public IndexReadAllResult ReadAllEventsForward(TFPos pos, int maxCount) {
			throw new NotImplementedException();
		}

		public IndexReadAllResult ReadAllEventsBackward(TFPos pos, int maxCount) {
			throw new NotImplementedException();
		}

		public IndexReadAllResult ReadAllEventsForwardFiltered(TFPos pos, int maxCount, int maxSearchWindow,
			IEventFilter eventFilter) {
			throw new NotImplementedException();
		}

		public IndexReadAllResult
			ReadAllEventsBackwardFiltered(TFPos pos, int maxCount, int maxSearchWindow, IEventFilter eventFilter) {
			throw new NotImplementedException();
		}

		public void Close() {
			throw new NotImplementedException();
		}

		public void Dispose() {
			throw new NotImplementedException();
		}

		public IIndexWriter<string> IndexWriter { get; }

		public IndexReadEventResult ReadEvent(string streamName, string streamId, long eventNumber) {
			throw new NotImplementedException();
		}

		public IndexReadStreamResult ReadStreamEventsBackward(string streamName, string streamId, long fromEventNumber,
			int maxCount) {
			throw new NotImplementedException();
		}

		public IndexReadStreamResult ReadStreamEventsForward(string streamName, string streamId, long fromEventNumber,
			int maxCount) {
			throw new NotImplementedException();
		}

		public IndexReadEventInfoResult ReadEventInfo_KeepDuplicates(string streamId, long eventNumber) {
			throw new NotImplementedException();
		}

		public IndexReadEventInfoResult ReadEventInfoForward_KnownCollisions(string streamId, long fromEventNumber,
			int maxCount,
			long beforePosition) {
			throw new NotImplementedException();
		}

		public IndexReadEventInfoResult ReadEventInfoForward_NoCollisions(ulong stream, long fromEventNumber,
			int maxCount,
			long beforePosition) {
			throw new NotImplementedException();
		}

		public IndexReadEventInfoResult ReadEventInfoBackward_KnownCollisions(string streamId, long fromEventNumber,
			int maxCount,
			long beforePosition) {
			throw new NotImplementedException();
		}

		public IndexReadEventInfoResult ReadEventInfoBackward_NoCollisions(ulong stream,
			Func<ulong, string> getStreamId, long fromEventNumber,
			int maxCount, long beforePosition) {
			throw new NotImplementedException();
		}

		public bool IsStreamDeleted(string streamId) {
			throw new NotImplementedException();
		}

		public long GetStreamLastEventNumber_KnownCollisions(string streamId, long beforePosition) {
			throw new NotImplementedException();
		}

		public long GetStreamLastEventNumber_NoCollisions(ulong stream, Func<ulong, string> getStreamId,
			long beforePosition) {
			throw new NotImplementedException();
		}

		public StreamMetadata GetStreamMetadata(string streamId) {
			throw new NotImplementedException();
		}

		public StorageMessage.EffectiveAcl GetEffectiveAcl(string streamId) {
			throw new NotImplementedException();
		}

		public string GetEventStreamIdByTransactionId(long transactionId) {
			throw new NotImplementedException();
		}


		public string GetStreamName(string streamId) {
			throw new NotImplementedException();
		}

		#endregion
	}
}
