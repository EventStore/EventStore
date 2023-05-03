using System;
using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Linq;
using EventStore.Core.Telemetry;
using Xunit;
using Conf = EventStore.Common.Configuration.TelemetryConfiguration;

namespace EventStore.Core.XUnit.Tests.Telemetry;

public class IncomingGrpcCallsMetricTests {
	[EventSource(Name = nameof(IncomingGrpcCallsMetricTests))]
	private class TestEventSource : EventSource {
		[Event(1)] public void CallStart() => WriteEvent(1);
		[Event(2)] public void CallStop() => WriteEvent(2);
		[Event(3)] public void CallFailed() => WriteEvent(3);
		[Event(4)] public void CallDeadlineExceeded() => WriteEvent(4);
		[Event(5)] public void CallUnimplemented() => WriteEvent(5);
	}

	[Fact]
	public void can_collect() {
		using var testSource = new TestEventSource();
		using var meter = new Meter($"{typeof(IncomingGrpcCallsMetricTests)}");
		using var listener = new TestMeterListener<long>(meter);
		using var sut = new IncomingGrpcCallsMetric(meter, "grpc-calls", new[] {
			Conf.IncomingGrpcCall.Current,
			Conf.IncomingGrpcCall.Total,
			Conf.IncomingGrpcCall.Failed,
			Conf.IncomingGrpcCall.Unimplemented,
			Conf.IncomingGrpcCall.DeadlineExceeded
		});

		sut.EnableEvents(testSource, EventLevel.Verbose);
		AssertMeasurements(listener,
			AssertMeasurement("current", 0),
			AssertMeasurement("total", 0),
			AssertMeasurement("failed", 0),
			AssertMeasurement("unimplemented", 0),
			AssertMeasurement("deadline-exceeded", 0));

		testSource.CallStart();
		AssertMeasurements(listener,
			AssertMeasurement("current", 1),
			AssertMeasurement("total", 1),
			AssertMeasurement("failed", 0),
			AssertMeasurement("unimplemented", 0),
			AssertMeasurement("deadline-exceeded", 0));

		testSource.CallFailed();
		AssertMeasurements(listener,
			AssertMeasurement("current", 1),
			AssertMeasurement("total", 1),
			AssertMeasurement("failed", 1),
			AssertMeasurement("unimplemented", 0),
			AssertMeasurement("deadline-exceeded", 0));

		testSource.CallUnimplemented();
		AssertMeasurements(listener,
			AssertMeasurement("current", 1),
			AssertMeasurement("total", 1),
			AssertMeasurement("failed", 1),
			AssertMeasurement("unimplemented", 1),
			AssertMeasurement("deadline-exceeded", 0));

		testSource.CallDeadlineExceeded();
		AssertMeasurements(listener,
			AssertMeasurement("current", 1),
			AssertMeasurement("total", 1),
			AssertMeasurement("failed", 1),
			AssertMeasurement("unimplemented", 1),
			AssertMeasurement("deadline-exceeded", 1));

		testSource.CallStop();
		AssertMeasurements(listener,
			AssertMeasurement("current", 0),
			AssertMeasurement("total", 1),
			AssertMeasurement("failed", 1),
			AssertMeasurement("unimplemented", 1),
			AssertMeasurement("deadline-exceeded", 1));
	}

	[Fact]
	public void can_collect_filtered() {
		using var testSource = new TestEventSource();
		using var meter = new Meter($"{typeof(IncomingGrpcCallsMetricTests)}");
		using var listener = new TestMeterListener<long>(meter);
		using var sut = new IncomingGrpcCallsMetric(meter, "grpc-calls", new[] {
			Conf.IncomingGrpcCall.Total,
			Conf.IncomingGrpcCall.DeadlineExceeded
		});

		sut.EnableEvents(testSource, EventLevel.Verbose);
		AssertMeasurements(listener,
			AssertMeasurement("total", 0),
			AssertMeasurement("deadline-exceeded", 0));

		testSource.CallStart();
		AssertMeasurements(listener,
			AssertMeasurement("total", 1),
			AssertMeasurement("deadline-exceeded", 0));

		testSource.CallFailed();
		AssertMeasurements(listener,
			AssertMeasurement("total", 1),
			AssertMeasurement("deadline-exceeded", 0));

		testSource.CallUnimplemented();
		AssertMeasurements(listener,
			AssertMeasurement("total", 1),
			AssertMeasurement("deadline-exceeded", 0));

		testSource.CallDeadlineExceeded();
		AssertMeasurements(listener,
			AssertMeasurement("total", 1),
			AssertMeasurement("deadline-exceeded", 1));

		testSource.CallStop();
		AssertMeasurements(listener,
			AssertMeasurement("total", 1),
			AssertMeasurement("deadline-exceeded", 1));
	}

	static Action<TestMeterListener<long>.TestMeasurement> AssertMeasurement(
		string expectedKind,
		long expectedValue) =>

		actualMeasurement => {
			Assert.Equal(expectedValue, actualMeasurement.Value);
			Assert.Collection(
				actualMeasurement.Tags.ToArray(),
				tag => {
					Assert.Equal("kind", tag.Key);
					Assert.Equal(expectedKind, tag.Value);
				});
		};

	static void AssertMeasurements(
		TestMeterListener<long> listener,
		params Action<TestMeterListener<long>.TestMeasurement>[] actions) {

		listener.Observe();
		Assert.Collection(listener.RetrieveMeasurements("grpc-calls"), actions);
	}
}
