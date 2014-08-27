using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace EventStore.Core.Services.PersistentSubscription
{
    internal class RequestStatistics
    {
        private readonly Queue<int> _measurements;
        private readonly ConcurrentDictionary<Guid, Operation> _operations = new ConcurrentDictionary<Guid, Operation>();
        readonly Stopwatch _watch;
        private readonly int _windowSize;

        public RequestStatistics(Stopwatch watch, int windowSize)
        {
            _watch = watch;
            _windowSize = windowSize;
            _measurements = new Queue<int>(windowSize);
        }

        public void StartOperation(Guid id)
        {
            var record = new Operation {Start = _watch.ElapsedTicks};
            _operations.AddOrUpdate(id, record, (q, val) => record);
        }

        public void EndOperation(Guid id)
        {
            Operation record;
            if (_operations.TryRemove(id, out record))
            {
                var current = _watch.ElapsedTicks;
                var time = current - record.Start;
                var ms = time / TimeSpan.TicksPerMillisecond;
                if (_measurements.Count >= _windowSize)
                {
                    _measurements.Dequeue();
                }
                _measurements.Enqueue((int)ms);
            }
        }

        public LatencyMeausrement GetMeasurementDetails()
        {
            var ret = new LatencyMeausrement();
            if (_measurements == null || _measurements.Count == 0) return ret;
            var items = _measurements.ToArray();
            Array.Sort(items);
            ret.Measurements.Add("Mean", items.Sum()/items.Length);
            ret.Measurements.Add("Meadian", items[items.Length/2]);
            ret.Measurements.Add("fastest", items[0]);
            for (var i = 0; i < 5; i++)
            {
                ret.Measurements.Add(i + "%", items[GetPercentile(i*20, items.Length)]);
            }
            ret.Measurements.Add("90%", items[GetPercentile(90m, items.Length)]);
            ret.Measurements.Add("95%", items[GetPercentile(95m, items.Length)]);
            ret.Measurements.Add("99%", items[GetPercentile(99m, items.Length)]);
            ret.Measurements.Add("99.5%", items[GetPercentile(99.5m, items.Length)]);
            ret.Measurements.Add("99.9%", items[GetPercentile(99.9m, items.Length)]);
            ret.Measurements.Add("Highest", items[items.Length - 1]);
            return ret;
        }

        public void ClearMeasurements()
        {
            _measurements.Clear();
        }

        private int GetPercentile(decimal percentile, int size)
        {
            decimal percent = 0;
            percent = percentile / 100m;
            var ret = (int)(percent * size);
            if (ret == size) ret -= 1;
            return ret;
        }

        struct Operation
        {
            public long Start;
        }
    }

    internal class LatencyMeausrement
    {
        public readonly Dictionary<string, int> Measurements = new Dictionary<string, int>();
    }
}