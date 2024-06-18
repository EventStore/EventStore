using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Metrics;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.XUnit.Tests.Metrics;
using Xunit;

namespace EventStore.Core.TransactionLog.Services.VNode;

public class WritesTrackerTests : IDisposable {
	private readonly TestMeterListener<long> _listener;
	private readonly WritesTracker _sut;
	private readonly CounterMetric _writtenBytes;
	private readonly CounterMetric _writtenEvents;
	private readonly CounterMetric _writes;

	public WritesTrackerTests() {
		var meter = new Meter($"{typeof(WritesTrackerTests)}");
		_listener = new TestMeterListener<long>(meter);
		_writes = new CounterMetric(meter, "eventstore", "writes");
		_writtenBytes = new CounterMetric(meter, "eventstore", "bytes");
		_writtenEvents = new CounterMetric(meter, "eventstore", "events");

		_sut = new WritesTracker(new CounterSubMetric(_writtenBytes, []), new CounterSubMetric(_writtenEvents, []),
			new CounterSubMetric(_writes, []));
	}

	public void Dispose() {
		_listener?.Dispose();
	}

	[Fact]
	public void can_track() {
		var data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
		var metadata = new byte[] { 0, 1, 2 };
		var events = new List<Event> {
			new(Guid.NewGuid(), "foobar", false, data, metadata),
			new(Guid.NewGuid(), "foobar", false, data, metadata),
			new(Guid.NewGuid(), "foobar", false, data, metadata)
		};

		var message = new ClientMessage.WriteEvents(Guid.NewGuid(), Guid.NewGuid(), new CallbackEnvelope(_ => { }),
			true, "toto", 42, events.ToArray(), SystemAccounts.System);

		_sut.OnWrite(message);
		AssertMeasurements(writes: 1, events: 3, bytes: (data.Length + metadata.Length) * 3);
	}

	private void AssertMeasurements(long writes, long events, long bytes) {
		_listener.Observe();
		var writesMeasurements = _listener.RetrieveMeasurements("eventstore-writes");
		var eventsMeasurements = _listener.RetrieveMeasurements("eventstore-events");
		var bytesMeasurements = _listener.RetrieveMeasurements("eventstore-bytes");

		Assert.Equal(writes, writesMeasurements[0].Value);
		Assert.Equal(events, eventsMeasurements[0].Value);
		Assert.Equal(bytes, bytesMeasurements[0].Value);
	}
}
