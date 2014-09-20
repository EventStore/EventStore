using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionStats
    {
        
        private long _totalItems;
        private TimeSpan _lastTotalTime;
        private long _lastTotalItems;
        private int _lastEventNumber = -1;
        private int _lastKnownEventNumber = -1;
        private readonly PersistentSubscription _parent;
        private readonly Stopwatch _totalTimeWatch;

        public PersistentSubscriptionStats(PersistentSubscription parent, Stopwatch totalTimeWatch)
        {
            _parent = parent;
            _totalTimeWatch = totalTimeWatch;
        }

        public void IncrementProcessed()
        {
            Interlocked.Increment(ref _totalItems);
        }

        public void SetLastCheckPoint(int lastEventNumber)
        {
            _lastEventNumber = lastEventNumber;
        }

        public void SetLastKnownEventNumber(int knownEventNumber)
        {
            if (knownEventNumber > _lastKnownEventNumber)
                _lastKnownEventNumber = knownEventNumber;
        }

        public MonitoringMessage.SubscriptionInfo GetStatistics()
        {
            var totalTime = _totalTimeWatch.Elapsed;
            var totalItems = Interlocked.Read(ref _totalItems);

            var lastRunMs = totalTime - _lastTotalTime;
            var lastItems = totalItems - _lastTotalItems;

            var avgItemsPerSecond = lastRunMs.Ticks != 0 ? (int)(TimeSpan.TicksPerSecond * lastItems / lastRunMs.Ticks) : 0;
            _lastTotalTime = totalTime;
            _lastTotalItems = totalItems;
            var connections = new List<MonitoringMessage.ConnectionInfo>();
            foreach (var conn in _parent._pushClients.GetAll())
            {
                var connItems = conn.TotalItems;
                var connLastItems = connItems - conn.LastTotalItems;
                conn.LastTotalItems = connItems;
                var connAvgItemsPerSecond = lastRunMs.Ticks != 0 ? (int)(TimeSpan.TicksPerSecond * connLastItems / lastRunMs.Ticks) : 0;
                var latencyStats = conn.GetLatencyStats();
                var stats = latencyStats == null ? null : latencyStats.Measurements;
                connections.Add(new MonitoringMessage.ConnectionInfo
                {
                    From = conn.From,
                    Username = conn.Username,
                    AverageItemsPerSecond = connAvgItemsPerSecond,
                    TotalItems = conn.TotalItems,
                    CountSinceLastMeasurement = connLastItems,
                    LatencyStats = stats
                });
            }

            return new MonitoringMessage.SubscriptionInfo()
            {
                EventStreamId = _parent.EventStreamId,
                GroupName = _parent.GroupName,
                Status = _parent.State.ToString(),
                Connections = connections,
                AveragePerSecond = avgItemsPerSecond,
                LastProcessedEventNumber = _lastEventNumber,
                LastKnownMessage = _lastKnownEventNumber,
                TotalItems = totalItems,
                CountSinceLastMeasurement = lastItems
            };
        }
    }
}