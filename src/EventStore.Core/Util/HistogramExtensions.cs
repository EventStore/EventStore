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
                lock (Histogram)
                {
                    Histogram.recordValue(
                        (long) ((((double) watch.ElapsedTicks - Start)/Stopwatch.Frequency)*1000000000));
                }
            }
        }
    }
}