using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using HdrHistogram.NET;

namespace EventStore.TestClient.Commands
{
    internal class RequestMonitor
    {
        private static long NUMBEROFNS = 10000000000L;
        private Histogram _histogram  = new Histogram(NUMBEROFNS, 3);
        private readonly ConcurrentDictionary<Guid, Operation> _operations = new ConcurrentDictionary<Guid, Operation>();
        readonly Stopwatch _watch = new Stopwatch();

        public RequestMonitor() {
            _watch.Start();
        }

        public void Clear()
        {
            _histogram =new Histogram(NUMBEROFNS, 3);
            _operations.Clear();
        }

        public void StartOperation(Guid id) {

            var record = new Operation {Start = _watch.ElapsedTicks};
            _operations.AddOrUpdate(id, record, (q, val) => record); 
        }

        public void EndOperation(Guid id) {
            Operation record;
            if (!_operations.TryRemove(id, out record)) return;
            var current = _watch.ElapsedTicks;

            var ns = (long)((((double)current - record.Start) / Stopwatch.Frequency) * 1000000000);
            _histogram.recordValue(ns);
        }

        public void GetMeasurementDetails() {
            _histogram.outputPercentileDistribution(Console.Out, outputValueUnitScalingRatio: 1000.0 * 1000.0);
        }
        
        struct Operation {
            public long Start;
        }
    }
}