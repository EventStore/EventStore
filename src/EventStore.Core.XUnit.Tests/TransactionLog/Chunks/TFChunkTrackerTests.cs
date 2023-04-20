using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using EventStore.Core.Telemetry;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.XUnit.Tests.Telemetry;
using Xunit;

namespace EventStore.Core.XUnit.Tests.TransactionLog.Chunks;

public class TFChunkTrackerTests : IDisposable {
	private readonly TFChunkTracker _sut;
	private readonly TestMeterListener<long> _listener;

	public TFChunkTrackerTests() {
		var meter = new Meter($"{typeof(TFChunkTrackerTests)}");
		_listener = new TestMeterListener<long>(meter);
		var byteMetric = meter.CreateCounter<long>("eventstore-io", unit: "bytes");
		var eventMetric = meter.CreateCounter<long>("eventstore-io", unit: "events");

		var readTag = new KeyValuePair<string, object>("activity", "read");
		_sut = new TFChunkTracker(
			readBytes: new CounterSubMetric<long>(byteMetric, readTag),
			readEvents: new CounterSubMetric<long>(eventMetric, readTag));
	}

	public void Dispose() {
		_listener.Dispose();
	}

	[Fact]
	public void can_observe_prepare_log() {
		var prepare = CreatePrepare(
			data: new byte[5],
			meta: new byte[5]);

		_sut.OnRead(prepare);
		_listener.Observe();

		AssertEventsRead(1);
		AssertBytesRead(10);
	}

	[Fact]
	public void disregard_system_log() {
		var system = CreateSystemRecord();
		_sut.OnRead(system);
		_listener.Observe();

		AssertEventsRead(null);
		AssertBytesRead(null);
	}

	[Fact]
	public void disregard_commit_log() {
		var system = CreateCommit();
		_sut.OnRead(system);
		_listener.Observe();

		AssertEventsRead(null);
		AssertBytesRead(null);
	}

	private void AssertEventsRead(long? expectedEventsRead) =>
		AssertMeasurements("eventstore-io-events", expectedEventsRead);

	private void AssertBytesRead(long? expectedBytesRead) =>
		AssertMeasurements("eventstore-io-bytes", expectedBytesRead);

	private void AssertMeasurements(string instrumentName, long? expectedValue) {
		_listener.Observe();
		var actual = _listener.RetrieveMeasurements(instrumentName);

		if (expectedValue is null) {
			Assert.Empty(actual);
		} else {
			Assert.Collection(
				actual,
				m => {
					Assert.Equal(expectedValue, m.Value);
					Assert.Collection(m.Tags.ToArray(), t => {
						Assert.Equal("activity", t.Key);
						Assert.Equal("read", t.Value);
					});
				});
		}
	}

	private static PrepareLogRecord CreatePrepare(byte[] data, byte[] meta) {
		return new PrepareLogRecord(42, Guid.NewGuid(), Guid.NewGuid(), 42, 42, "tests", 42, DateTime.Now,
			PrepareFlags.Data, "type-test", data, meta);
	}

	private static SystemLogRecord CreateSystemRecord() {
		return new SystemLogRecord(42, DateTime.Now, SystemRecordType.Epoch, SystemRecordSerialization.Binary, Array.Empty<byte>());
	}

	private static CommitLogRecord CreateCommit() {
		return new CommitLogRecord(42, Guid.NewGuid(), 42, DateTime.Now, 42);
	}
}
