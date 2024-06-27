using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.CompilerServices;
using EventStore.Core.Messages;
using EventStore.Core.Metrics;
using EventStore.Core.Services.PersistentSubscription;
using EventStore.Core.Tests;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Metrics;

public class PersistentSubscriptionParkedMessagesMetricTests : IDisposable {
	private readonly Disposables _disposables = new();
	private readonly List<MonitoringMessage.PersistentSubscriptionInfo> _fakeStatistics = [];

	public PersistentSubscriptionParkedMessagesMetricTests() {
		var statsSampleOne = new MonitoringMessage.PersistentSubscriptionInfo() {
			EventSource = "test",
			GroupName = "testGroup",
			Status = PersistentSubscriptionState.Live.ToString(),
			Connections = new List<MonitoringMessage.ConnectionInfo>(),
			AveragePerSecond = 1,
			LastCheckpointedEventPosition = 10.ToString(),
			LastKnownEventPosition = 20.ToString(),
			TotalItems = 30,
			CountSinceLastMeasurement = 5,
			CheckPointAfterMilliseconds = 1000,
			BufferSize = 500,
			LiveBufferSize = 500,
			MaxCheckPointCount = 500,
			MaxRetryCount = 5,
			MessageTimeoutMilliseconds = 1000,
			MinCheckPointCount = 10,
			ReadBatchSize = 20,
			ResolveLinktos = true,
			StartFrom = 0.ToString(),
			ReadBufferCount = 0,
			RetryBufferCount = 0,
			LiveBufferCount = 0,
			ExtraStatistics = false,
			TotalInFlightMessages = 10,
			OutstandingMessagesCount = 2,
			NamedConsumerStrategy = "Round Robin",
			MaxSubscriberCount = 10,
			ParkedMessageCount = 10,
			OldestParkedMessage = 50
		};

		var statsSampleTwo = new MonitoringMessage.PersistentSubscriptionInfo() {
			EventSource = "$all",
			GroupName = "testGroup",
			Status = PersistentSubscriptionState.Live.ToString(),
			Connections = new List<MonitoringMessage.ConnectionInfo>(),
			AveragePerSecond = 1,
			LastCheckpointedEventPosition = "C:211845/P:211845",
			LastKnownEventPosition = "C:211200/P:211200",
			TotalItems = 30,
			CountSinceLastMeasurement = 5,
			CheckPointAfterMilliseconds = 1000,
			BufferSize = 500,
			LiveBufferSize = 500,
			MaxCheckPointCount = 500,
			MaxRetryCount = 5,
			MessageTimeoutMilliseconds = 1000,
			MinCheckPointCount = 10,
			ReadBatchSize = 20,
			ResolveLinktos = true,
			StartFrom = 0.ToString(),
			ReadBufferCount = 0,
			RetryBufferCount = 0,
			LiveBufferCount = 0,
			ExtraStatistics = false,
			TotalInFlightMessages = 5,
			OutstandingMessagesCount = 2,
			NamedConsumerStrategy = "Round Robin",
			MaxSubscriberCount = 10,
			ParkedMessageCount = 10,
			OldestParkedMessage = 50
		};

		_fakeStatistics.Add(statsSampleOne);
		_fakeStatistics.Add(statsSampleTwo);
	}

	public void Dispose() {
		_disposables?.Dispose();
	}

	private (PersistentSubscriptionParkedMessagesMetric, TestMeterListener<long>) GenSut(
		[CallerMemberName] string callerName = "") {

		var meter = new Meter($"{typeof(PersistentSubscriptionConnectionCountMetricTests)}-{callerName}").DisposeWith(_disposables);
		var listener = new TestMeterListener<long>(meter).DisposeWith(_disposables);
		var sut = new PersistentSubscriptionParkedMessagesMetric(meter, "test-metric");

		return (sut, listener);
	}

	[Fact]
	public void number_of_instruments_to_observe() {
		var (sut, listener) = GenSut();
		sut.Register(() => _fakeStatistics);

		listener.Observe();

		var measurements = listener.RetrieveMeasurements("test-metric");
		Assert.Equal(2, measurements.Count);
	}

	[Fact]
	public void observe_metrics_data() {
		var (sut, listener) = GenSut();
		sut.Register(() => _fakeStatistics);

		AssertMeasurements(listener,
			AssertMeasurement("test", "testGroup", 10),
			AssertMeasurement("$all", "testGroup", 10)
		);
	}

	static void AssertMeasurements(
		TestMeterListener<long> listener,
		params Action<TestMeterListener<long>.TestMeasurement>[] actions) {

		listener.Observe();

		Assert.Collection(listener.RetrieveMeasurements("test-metric"), actions);
	}

	static Action<TestMeterListener<long>.TestMeasurement> AssertMeasurement(
		string sourceName,
		string groupName,
		long expectedValue) =>

		actualMeasurement => {
			Assert.Equal(expectedValue, actualMeasurement.Value);
			Assert.Collection(
				actualMeasurement.Tags.ToArray(),
				tag => {
					Assert.Equal("event_stream_id", tag.Key);
					Assert.Equal(sourceName, tag.Value);
				},
				tag => {
					Assert.Equal("group_name", tag.Key);
					Assert.Equal(groupName, tag.Value);
				}
			);
		};
}
