using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.PersistentSubscription {
	public class PersistentSubscriptionStats {
		private long _totalItems;
		private TimeSpan _lastTotalTime;
		private long _lastTotalItems;
		private long _lastEventNumber = -1;
		private long _lastKnownEventNumber = -1;
		private readonly PersistentSubscription _parent;
		private readonly Stopwatch _totalTimeWatch;
		private readonly PersistentSubscriptionParams _settings;

		public PersistentSubscriptionStats(PersistentSubscription parent, PersistentSubscriptionParams settings,
			Stopwatch totalTimeWatch) {
			_settings = settings;
			_parent = parent;
			_totalTimeWatch = totalTimeWatch;
		}

		public void IncrementProcessed() {
			Interlocked.Increment(ref _totalItems);
		}

		public void SetLastCheckPoint(long lastEventNumber) {
			_lastEventNumber = lastEventNumber;
		}

		public void SetLastKnownEventNumber(long knownEventNumber) {
			if (knownEventNumber > _lastKnownEventNumber)
				_lastKnownEventNumber = knownEventNumber;
		}

		public MonitoringMessage.SubscriptionInfo GetStatistics() {
			var totalTime = _totalTimeWatch.Elapsed;
			var totalItems = Interlocked.Read(ref _totalItems);

			var lastRunMs = totalTime - _lastTotalTime;
			var lastItems = totalItems - _lastTotalItems;

			var avgItemsPerSecond =
				lastRunMs.Ticks != 0 ? (int)(TimeSpan.TicksPerSecond * lastItems / lastRunMs.Ticks) : 0;
			_lastTotalTime = totalTime;
			_lastTotalItems = totalItems;
			var connections = new List<MonitoringMessage.ConnectionInfo>();
			var totalInflight = 0;
			foreach (var conn in _parent._pushClients.GetAll()) {
				var connItems = conn.TotalItems;
				var connLastItems = connItems - conn.LastTotalItems;
				conn.LastTotalItems = connItems;
				var connAvgItemsPerSecond = lastRunMs.Ticks != 0
					? (int)(TimeSpan.TicksPerSecond * connLastItems / lastRunMs.Ticks)
					: 0;
				var extraStats = conn.GetExtraStats();
				var stats = extraStats == null ? null : extraStats.Measurements;
				totalInflight += conn.InflightMessages;
				connections.Add(new MonitoringMessage.ConnectionInfo {
					From = conn.From,
					Username = conn.Username,
					AverageItemsPerSecond = connAvgItemsPerSecond,
					TotalItems = conn.TotalItems,
					InFlightMessages = conn.InflightMessages,
					AvailableSlots = conn.AvailableSlots,
					CountSinceLastMeasurement = connLastItems,
					ObservedMeasurements = stats
				});
			}

			return new MonitoringMessage.SubscriptionInfo() {
				EventStreamId = _parent.EventStreamId,
				GroupName = _parent.GroupName,
				Status = _parent.State.ToString(),
				Connections = connections,
				AveragePerSecond = avgItemsPerSecond,
				LastProcessedEventNumber = _lastEventNumber,
				LastKnownMessage = _lastKnownEventNumber,
				TotalItems = totalItems,
				CountSinceLastMeasurement = lastItems,
				CheckPointAfterMilliseconds = (int)_settings.CheckPointAfter.TotalMilliseconds,
				BufferSize = _settings.BufferSize,
				LiveBufferSize = _settings.LiveBufferSize,
				MaxCheckPointCount = _settings.MaxCheckPointCount,
				MaxRetryCount = _settings.MaxRetryCount,
				MessageTimeoutMilliseconds = (int)_settings.MessageTimeout.TotalMilliseconds,
				MinCheckPointCount = _settings.MinCheckPointCount,
				ReadBatchSize = _settings.ReadBatchSize,
				ResolveLinktos = _settings.ResolveLinkTos,
				StartFrom = _settings.StartFrom,
				ReadBufferCount = _parent._streamBuffer.ReadBufferCount,
				RetryBufferCount = _parent._streamBuffer.RetryBufferCount,
				LiveBufferCount = _parent._streamBuffer.LiveBufferCount,
				ExtraStatistics = _settings.ExtraStatistics,
				TotalInFlightMessages = _parent.OutstandingMessageCount,
				NamedConsumerStrategy = _settings.ConsumerStrategy.Name,
				MaxSubscriberCount = _settings.MaxSubscriberCount
			};
		}
	}
}
