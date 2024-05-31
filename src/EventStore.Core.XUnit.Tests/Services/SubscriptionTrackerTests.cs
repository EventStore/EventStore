using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.XUnit.Tests.Metrics;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Services;

public class SubscriptionTrackerTests : IDisposable {
	private const string StreamName = "stream";
	private const long EndOfAllStream = 783L;
	private const long EndOfStream = 10L;

	private readonly TestMeterListener<double> _completionListener;
	private readonly TestMeterListener<long> _subscriptionPositionListener;
	private readonly TestMeterListener<long> _streamPositionListener;
	private readonly Meter _meter;
	private readonly SubscriptionTracker _sut;
	private readonly Guid _subscriptionId;
	private readonly KeyValuePair<string, object>[] _streamTags;
	private readonly KeyValuePair<string, object>[] _allStreamTags;
	private readonly EventRecord _eventRecord;
	private readonly ResolvedEvent _resolvedEvent;

	public SubscriptionTrackerTests() {
		_meter = new Meter("eventstore");

		var metric = new SubscriptionMetric(_meter, "test");
		_completionListener = new TestMeterListener<double>(_meter);
		_streamPositionListener = new TestMeterListener<long>(_meter);
		_subscriptionPositionListener = new TestMeterListener<long>(_meter);

		_sut = new SubscriptionTracker(metric);

		_subscriptionId = Guid.NewGuid();

		_streamTags = [new("streamName", StreamName), new("subscriptionId", _subscriptionId)];
		_allStreamTags = [new("streamName", SystemStreams.AllStream), new("subscriptionId", _subscriptionId)];
		_eventRecord = new EventRecord(11L, 849L, Guid.NewGuid(), Guid.NewGuid(), 849L, 0, StreamName, 1L,
			DateTime.Now, PrepareFlags.SingleWrite, "-", [], []);
		_resolvedEvent = ResolvedEvent.ForUnresolvedEvent(_eventRecord, _eventRecord.LogPosition);
	}

	[Fact]
	public void add_subscription_stream() {
		_sut.AddSubscription(_subscriptionId, StreamName, EndOfStream);

		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new TestMeterListener<double>.TestMeasurement[] {
			new() { Value = 0.0, Tags = _streamTags },
			new() { Value = 0.0, Tags = _streamTags },
			new() { Value = EndOfStream, Tags = _streamTags }
		}, actual);
	}

	[Fact]
	public void add_subscription_all() {
		_sut.AddSubscription(_subscriptionId, null, EndOfAllStream);

		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new TestMeterListener<double>.TestMeasurement[] {
			new() { Value = 0.0, Tags = _allStreamTags },
			new() { Value = 0.0, Tags = _allStreamTags },
			new() { Value = EndOfAllStream, Tags = _allStreamTags }
		}, actual);
	}

	[Fact]
	public void event_committed_stream() {
		_sut.AddSubscription(_subscriptionId, StreamName, EndOfStream);
		_sut.RecordEvent(_eventRecord);

		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new TestMeterListener<double>.TestMeasurement[] {
			new() { Value = 0.0, Tags = _streamTags },
			new() { Value = 0.0, Tags = _streamTags },
			new() { Value = _eventRecord.EventNumber, Tags = _streamTags }
		}, actual);
	}

	[Fact]
	public void event_committed_all() {
		_sut.AddSubscription(_subscriptionId, null, EndOfAllStream);
		_sut.RecordEvent(_eventRecord);

		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new TestMeterListener<double>.TestMeasurement[] {
			new() { Value = 0.0, Tags = _allStreamTags },
			new() { Value = 0.0, Tags = _allStreamTags },
			new() { Value = _eventRecord.LogPosition, Tags = _allStreamTags }
		}, actual);
	}

	[Fact]
	public void event_processed_stream() {
		_sut.AddSubscription(_subscriptionId, StreamName, EndOfStream);
		_sut.RecordEvent(_eventRecord);
		_sut.ProcessEvent(_resolvedEvent);

		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new TestMeterListener<double>.TestMeasurement[] {
			new() { Value = 1.0, Tags = _streamTags },
			new() { Value = _eventRecord.EventNumber, Tags = _streamTags },
			new() { Value = _eventRecord.EventNumber, Tags = _streamTags }
		}, actual);
	}

	[Fact]
	public void event_processed_all() {
		_sut.AddSubscription(_subscriptionId, null, EndOfAllStream);
		_sut.RecordEvent(_eventRecord);
		_sut.ProcessEvent(_resolvedEvent);

		Observe();

		var actual = RetrieveMeasurements();

		Assert.Equivalent(new TestMeterListener<double>.TestMeasurement[] {
			new() { Value = 1.0, Tags = _allStreamTags },
			new() { Value = _eventRecord.LogPosition, Tags = _allStreamTags },
			new() { Value = _eventRecord.LogPosition, Tags = _allStreamTags }
		}, actual);
	}

	private void Observe() {
		_completionListener.Observe();
		_subscriptionPositionListener.Observe();
		_streamPositionListener.Observe();
	}

	private TestMeterListener<double>.TestMeasurement[] RetrieveMeasurements() => [
		_completionListener.RetrieveMeasurements("test-completed")
			.Single(),
		_subscriptionPositionListener.RetrieveMeasurements("test-subscription-position")
			.Select(x => new TestMeterListener<double>.TestMeasurement {
				Tags = x.Tags,
				Value = x.Value
			})
			.Single(),
		_streamPositionListener.RetrieveMeasurements("test-stream-position")
			.Select(x => new TestMeterListener<double>.TestMeasurement {
				Tags = x.Tags,
				Value = x.Value
			})
			.Single()
	];

	public void Dispose() {
		_completionListener?.Dispose();
		_subscriptionPositionListener?.Dispose();
		_streamPositionListener?.Dispose();
		_meter?.Dispose();
	}
}
