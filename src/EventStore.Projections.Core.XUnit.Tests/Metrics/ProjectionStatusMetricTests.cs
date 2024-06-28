using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using EventStore.Core.Tests;
using EventStore.Projections.Core.Metrics;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using Xunit;

namespace EventStore.Projections.Core.XUnit.Tests.Metrics;

public class ProjectionStatusMetricTests : IDisposable {
	private readonly Disposables _disposables = new();

	private readonly ProjectionStatistics[] _fakeStatistics = [new() {
		Name = "TestProjection",
		ProjectionId = 1234,
		Epoch = -1,
		Version = -1,
		Mode = ProjectionMode.Continuous,
		Status = "Running",
		LeaderStatus = ManagedProjectionState.Running,
		Progress = 50,
		EventsProcessedAfterRestart = 50
	}];

	public void Dispose() {
		_disposables.Dispose();
	}

	private (ProjectionStatusMetric, TestMeterListener<long>) GenSut(
		[CallerMemberName] string callerName = "") {

		var meter = new Meter($"{typeof(ProjectionStatusMetricTests)}-{callerName}").DisposeWith(_disposables);
		var listener = new TestMeterListener<long>(meter).DisposeWith(_disposables);
		var sut = new ProjectionStatusMetric(meter, "test-metric");
		return (sut, listener);
	}

	[Fact]
	public void number_of_instruments_to_observe() {
		var (sut, listener) = GenSut();

		sut.Register(() => _fakeStatistics);
		listener.Observe();

		var measurements = listener.RetrieveMeasurements("test-metric");
		Assert.Equal(3, measurements.Count);
	}

	[Fact]
	public void observe_metrics_data() {
		var (sut, listener) = GenSut();

		sut.Register(() => _fakeStatistics);

		AssertMeasurements(listener,
			AssertMeasurement("TestProjection", "Running", 1),
			AssertMeasurement("TestProjection", "Faulted", 0),
			AssertMeasurement("TestProjection", "Stopped",0));
	}

	static void AssertMeasurements(
		TestMeterListener<long> listener,
		params Action<TestMeterListener<long>.TestMeasurement>[] actions) {

		listener.Observe();

		Assert.Collection(listener.RetrieveMeasurements("test-metric"), actions);
	}

	static Action<TestMeterListener<long>.TestMeasurement> AssertMeasurement(
		string projectionName,
		string projectionStatus,
		long expectedValue) =>

		actualMeasurement => {
			Assert.Equal(expectedValue, actualMeasurement.Value);
			if (actualMeasurement.Tags == null) return;

			Assert.Collection(
				actualMeasurement.Tags.ToArray(),
				tag => {
					Assert.Equal("projection", tag.Key);
					Assert.Equal(projectionName, tag.Value);
				},
				tag => {
					Assert.Equal("status", tag.Key);
					Assert.Equal(projectionStatus, tag.Value);
				});
		};
}
