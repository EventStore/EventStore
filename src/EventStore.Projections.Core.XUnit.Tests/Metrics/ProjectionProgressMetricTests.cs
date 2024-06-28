using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using EventStore.Core.Tests;
using EventStore.Projections.Core.Metrics;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using Xunit;

namespace EventStore.Projections.Core.XUnit.Tests.Metrics;

public class ProjectionProgressMetricTests : IDisposable {
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

	private (ProjectionProgressMetric, TestMeterListener<float>) GenSut(
		[CallerMemberName] string callerName = "") {

		var meter = new Meter($"{typeof(ProjectionProgressMetricTests)}-{callerName}").DisposeWith(_disposables);
		var listener = new TestMeterListener<float>(meter).DisposeWith(_disposables);
		var sut = new ProjectionProgressMetric(meter, "test-metric");
		return (sut, listener);
	}

	[Fact]
	public void number_of_instruments_to_observe() {
		var (sut, listener) = GenSut();

		sut.Register(() => _fakeStatistics);
		listener.Observe();

		var measurements = listener.RetrieveMeasurements("test-metric");
		Assert.Single(measurements);
	}

	[Fact]
	public void observe_metrics_data() {
		var (sut, listener) = GenSut();

		sut.Register(() => _fakeStatistics);

		AssertMeasurements(listener,
			AssertMeasurement("TestProjection", 0.5f));
	}

	static void AssertMeasurements(
		TestMeterListener<float> listener,
		params Action<TestMeterListener<float>.TestMeasurement>[] actions) {

		listener.Observe();

		Assert.Collection(listener.RetrieveMeasurements("test-metric"), actions);
	}

	static Action<TestMeterListener<float>.TestMeasurement> AssertMeasurement(
		string projectionName,
		float expectedValue) =>

		actualMeasurement => {
			Assert.Equal(expectedValue, actualMeasurement.Value);
			if (actualMeasurement.Tags == null) return;

			Assert.Collection(
				actualMeasurement.Tags.ToArray(),
				tag => {
					Assert.Equal("projection", tag.Key);
					Assert.Equal(projectionName, tag.Value);
				});
		};
}
