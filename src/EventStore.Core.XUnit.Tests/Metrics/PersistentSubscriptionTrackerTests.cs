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


public class PersistentSubscriptionTrackerTests : IDisposable {
	private readonly Disposables _disposables = new();
	private readonly List<MonitoringMessage.PersistentSubscriptionInfo> _fakeStatsList = [];

	public PersistentSubscriptionTrackerTests() {
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
			TotalInFlightMessages = 10,
			OutstandingMessagesCount = 2,
			NamedConsumerStrategy = "Round Robin",
			MaxSubscriberCount = 10,
			ParkedMessageCount = 10,
			OldestParkedMessage = 50
		};

		_fakeStatsList.Add(statsSampleOne);
		_fakeStatsList.Add(statsSampleTwo);
	}

	public void Dispose() {
		_disposables?.Dispose();
	}

	private (PersistentSubscriptionTracker, TestMeterListener<long>) GenSut(
		[CallerMemberName] string callerName = "") {

		var meter = new Meter($"{typeof(PersistentSubscriptionTrackerTests)}-{callerName}").DisposeWith(_disposables);
		var listener = new TestMeterListener<long>(meter).DisposeWith(_disposables);
		var metric = new PersistentSubscriptionMetric(meter, "test-metric");
		var sut = new PersistentSubscriptionTracker(metric);

		return (sut, listener);
	}

	[Fact]
	public void number_of_instruments_to_observe() {
		var (sut, listener) = GenSut();
		sut.Register(_fakeStatsList);

		listener.Observe();

		var measurements = listener.RetrieveMeasurements("test-metric");
		Assert.Equal(14 , measurements.Count);
	}

	[Fact]
	public void observe_all_metrics() {
		var (sut, listener) = GenSut();
		sut.Register(_fakeStatsList);

		AssertMeasurements(listener,
			AssertMeasurement("test/testGroup", "subscription-total-items-processed", 30),
			AssertMeasurement("test/testGroup", "subscription-connection-count", 0),
			AssertMeasurement("test/testGroup", "subscription-total-in-flight-messages", 10),
			AssertMeasurement("test/testGroup", "subscription-total-number-of-parked-messages", 10),
			AssertMeasurement("test/testGroup", "subscription-oldest-parked-message", 50),
			AssertMeasurement("test/testGroup", "subscription-last-processed-event-number", 10),
			AssertMeasurement("test/testGroup", "subscription-last-known-event-number", 20),
			AssertMeasurement("$all/testGroup", "subscription-total-items-processed", 30),
			AssertMeasurement("$all/testGroup", "subscription-connection-count", 0),
			AssertMeasurement("$all/testGroup", "subscription-total-in-flight-messages", 10),
			AssertMeasurement("$all/testGroup", "subscription-total-number-of-parked-messages", 10),
			AssertMeasurement("$all/testGroup", "subscription-oldest-parked-message", 50),
			AssertMeasurement("$all/testGroup", "subscription-last-checkpointed-event-commit-position", 211845),
			AssertMeasurement("$all/testGroup", "subscription-last-known-event-commit-position", 211200));
	}

	static Action<TestMeterListener<long>.TestMeasurement> AssertMeasurement(
		string subscriptionName,
		string metricName,
		long expectedValue) =>

		actualMeasurement => {
			Assert.Equal(expectedValue, actualMeasurement.Value);
			Assert.Collection(
				actualMeasurement.Tags.ToArray(),
				tag => {
					Assert.Equal("subscription", tag.Key);
					Assert.Equal(subscriptionName, tag.Value);
				},
				tag => {
					Assert.Equal("kind", tag.Key);
					Assert.Equal(metricName, tag.Value);
				}
			);
		};


	static void AssertMeasurements(
		TestMeterListener<long> listener,
		params Action<TestMeterListener<long>.TestMeasurement>[] actions) {

		listener.Observe();

		Assert.Collection(listener.RetrieveMeasurements("test-metric"), actions);
	}
}
