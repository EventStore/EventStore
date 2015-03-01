using System;
using System.Diagnostics;

namespace EventStore.Core.Util
{
    public static class HistogramExtensions
    {
        private static readonly Stopwatch watch;

        static HistogramExtensions()
        {
            watch = new Stopwatch();
            watch.Start();
        }

        public static Measurement Measure(this HdrHistogram.NET.Histogram histogram)
        {
            return new Measurement() {Start = watch.ElapsedTicks, Histogram=histogram};
        }

        public struct Measurement : IDisposable
        {
            public long Start;
            public HdrHistogram.NET.Histogram Histogram;

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