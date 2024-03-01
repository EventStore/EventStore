using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using EventStore.Core.Metrics;
using EventStore.Core.Tests;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Metrics;

public class ElectionsCounterTrackerTests : IDisposable {
	private readonly Disposables _disposables = new();

	public void Dispose() {
		_disposables?.Dispose();
	}

	private (ElectionsCounterTracker, TestMeterListener<long>) GenSut(
		[CallerMemberName]string callerName = "") {

		var meter = new Meter($"{typeof(ElectionsCounterTrackerTests)}--{callerName}").DisposeWith(_disposables);
		var listener = new TestMeterListener<long>(meter).DisposeWith(_disposables);
		var metric = new CounterMetric(meter, "test-metric", null);
		var sut = new ElectionsCounterTracker(new CounterSubMetric(metric, Array.Empty<KeyValuePair<string, object>>()));

		return (sut, listener);
	}

	[Fact]
	public void test_election_count_for_one_election() {
		var (sut, listener) = GenSut();
		sut.IncrementCounter();

		AssertMeasurements(listener, AssertMeasurement(1));
	}

	[Fact]
	public void test_election_count_for_five_elections() {
		var (sut, listener) = GenSut();
		sut.IncrementCounter();
		sut.IncrementCounter();
		sut.IncrementCounter();
		sut.IncrementCounter();
		sut.IncrementCounter();

		AssertMeasurements(listener, AssertMeasurement(5));
	}

	static Action<TestMeterListener<long>.TestMeasurement> AssertMeasurement(
		int expectedValue) =>
		actualMeasurement => {
			Assert.Equal(expectedValue, actualMeasurement.Value);
		};

	static void AssertMeasurements(
		TestMeterListener<long> listener,
		params Action<TestMeterListener<long>.TestMeasurement>[] actions) {

		listener.Observe();
		Assert.Collection(listener.RetrieveMeasurements("test-metric"), actions);
	}
}
