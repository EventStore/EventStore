using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using EventStore.Core.Tests;
using EventStore.Projections.Core.Metrics;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using Xunit;

namespace EventStore.Projections.Core.XUnit.Tests.Metrics;

public class ProjectionTrackerTests: IDisposable {

	private readonly Disposables _disposables = new();
	private readonly ProjectionStatistics[] _fakeStatsList = new ProjectionStatistics[2];

	public ProjectionTrackerTests() {
		 var projectionOneStats = new ProjectionStatistics {
			 EffectiveName = "TestProjectionOne",
			 ProjectionId = 1234,
			 Epoch = -1,
			 Version = -1,
			 Mode = ProjectionMode.Continuous,
			 Status = "Running",
			 LeaderStatus = ManagedProjectionState.Running,
			 Progress = 100,
			 EventsProcessedAfterRestart = 50
		 };

		 var projectionTwoStats = new ProjectionStatistics {
			 EffectiveName = "TestProjectionTwo",
			 ProjectionId = 5678,
			 Epoch = -1,
			 Version = -1,
			 Mode = ProjectionMode.Continuous,
			 Status = "Stopped",
			 LeaderStatus = ManagedProjectionState.Running,
			 Progress = 50,
			 EventsProcessedAfterRestart = 10
		 };

		 _fakeStatsList[0] = projectionOneStats;
		 _fakeStatsList[1] = projectionTwoStats;
	}

	public void Dispose() {
		_disposables.Dispose();
	}

	private (ProjectionTracker, TestMeterListener<long>) GenSut(
		[CallerMemberName] string callerName = "") {

		var meter = new Meter($"{typeof(ProjectionTrackerTests)}-{callerName}").DisposeWith(_disposables);
		var listener = new TestMeterListener<long>(meter).DisposeWith(_disposables);
		var metrics = new ProjectionMetrics(meter, "test-metric");
		var sut = new ProjectionTracker(metrics);
		return (sut, listener);
	}

	[Fact]
	public void number_of_instruments_to_observe() {
		var (sut, listener) = GenSut();

		sut.Register(_fakeStatsList);
		listener.Observe();

		var measurements = listener.RetrieveMeasurements("test-metric");
		Assert.Equal(8, measurements.Count);
	}

	[Fact]
	public void observe_all_metrics() {
		var (sut, listener) = GenSut();

		sut.Register(_fakeStatsList);

		AssertMeasurements(listener,
			AssertMeasurement("TestProjectionOne", "eventstore-projection-status", "Running", 1),
			AssertMeasurement("TestProjectionOne", "eventstore-projection-running", null, 1),
			AssertMeasurement("TestProjectionOne", "eventstore-projection-progress", null,1),
			AssertMeasurement("TestProjectionOne", "eventstore-projection-events-processed-after-restart-total",null, 50),
			AssertMeasurement("TestProjectionTwo", "eventstore-projection-status", "Stopped", 1),
			AssertMeasurement("TestProjectionTwo", "eventstore-projection-running", null,0),
			AssertMeasurement("TestProjectionTwo", "eventstore-projection-progress", null,(long)0.5),
			AssertMeasurement("TestProjectionTwo", "eventstore-projection-events-processed-after-restart-total", null, 10));
	}

	static void AssertMeasurements(
		TestMeterListener<long> listener,
		params Action<TestMeterListener<long>.TestMeasurement>[] actions) {

		listener.Observe();

		Assert.Collection(listener.RetrieveMeasurements("test-metric"), actions);
	}

	static Action<TestMeterListener<long>.TestMeasurement> AssertMeasurement(
		string projectionName,
		string metricName,
		string? projectionStatus,
		long expectedValue) =>

		actualMeasurement => {
			Assert.Equal(expectedValue, actualMeasurement.Value);
			if (actualMeasurement.Tags == null) return;
			if (projectionStatus != null) {
				Assert.Collection(
					actualMeasurement.Tags.ToArray(),
					tag => {
						Assert.Equal("projection", tag.Key);
						Assert.Equal(projectionName, tag.Value);
					},
					tag => {
						Assert.Equal("kind", tag.Key);
						Assert.Equal(metricName, tag.Value);
					},
					tag => {
						Assert.Equal("status", tag.Key);
						Assert.Equal(projectionStatus, tag.Value);
					});
			} else {
				Assert.Collection(
					actualMeasurement.Tags.ToArray(),
					tag => {
						Assert.Equal("projection", tag.Key);
						Assert.Equal(projectionName, tag.Value);
					},
					tag => {
						Assert.Equal("kind", tag.Key);
						Assert.Equal(metricName, tag.Value);
					});
			}
		};
}
