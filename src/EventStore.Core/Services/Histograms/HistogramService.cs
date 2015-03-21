using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using HdrHistogram.NET;

namespace EventStore.Core.Services.Histograms
{
    //histogram service is just a static class used by everyone else if histograms are enabled.
    public static class HistogramService
    {
        private const long NUMBEROFNS = 1000000000L;
        private static readonly Stopwatch _stopwatch = new Stopwatch();
        private static readonly Dictionary<string, Histogram> Histograms = new Dictionary<string, Histogram>();

        static HistogramService()
        {
            _stopwatch.Start();
        }

        public static Histogram GetHistogram(string name)
        {
            Histogram ret;
            Histograms.TryGetValue(name, out ret);
            return ret;
        }

        public static Measurement Measure(string name)
        {
            var histogram = GetHistogram(name);
            return new Measurement {watch = _stopwatch, Start = _stopwatch.ElapsedTicks, Histogram=histogram};
        }

        public static void SetValue(string name, long value)
        {
            if (value >= NUMBEROFNS) return;
            if(name == null) return;
            Histogram hist;
            if(!Histograms.TryGetValue(name, out hist)) {
                return;
            }
            lock (hist)
            {
                hist.recordValue(value);
            }
        }

        public static void CreateHistogram(string name)
        {
            if (!Histograms.ContainsKey(name))
            {
                Histograms.Add(name, new Histogram(NUMBEROFNS, 3));
            }
        }

        public static void CreateHistograms()
        {
            CreateHistogram("writer-flush");
            CreateHistogram("chaser-wait");
            CreateHistogram("chaser-flush");
            CreateHistogram("reader-readevent");
            CreateHistogram("reader-streamrange");
            CreateHistogram("reader-allrange");
        }

        public static void StartJitterMonitor()
        {
            CreateHistogram("jitter");
            Task.Factory.StartNew(x =>
            {
                var watch = new Stopwatch();
                watch.Start();
                while (true)
                {
                    using (Measure("jitter"))
                    {
                        Thread.Sleep(1);
                    }
                }
            }, null, TaskCreationOptions.LongRunning);
        }
    }

     
        public struct Measurement : IDisposable
        {
            public Stopwatch watch;
            public Histogram Histogram;
            public long Start;

            public void Dispose()
            {
                if (Histogram == null) return;
                lock (Histogram)
                {
                    var valueToRecord = (((double)watch.ElapsedTicks - Start) / Stopwatch.Frequency) * 1000000000;
                    if (valueToRecord < HighestPowerOf2(Histogram.getHighestTrackableValue()))
                    {
                        Histogram.recordValue((long)valueToRecord);
                    }
                }
            }
            private static long HighestPowerOf2(long x)
            {
                x--;
                x |= (x >> 1);
                x |= (x >> 2);
                x |= (x >> 4);
                x |= (x >> 8);
                x |= (x >> 16);
                return (x + 1);
            }

        }
}
