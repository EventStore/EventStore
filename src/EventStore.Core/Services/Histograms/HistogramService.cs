
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Util;
using HdrHistogram.NET;

namespace EventStore.Core.Services.Histograms
{
    //histogram service is just a static class used by everyone else if histograms are enabled.
    public static class HistogramService
    {
        private const long NUMBEROFNS = 1000000000L;

        private static readonly Dictionary<string, Histogram> Histograms = new Dictionary<string, Histogram>();
 
        public static Histogram GetHistogram(string name)
        {
            Histogram ret;
            Histograms.TryGetValue(name, out ret);
            return ret;
        }

        public static void CreateHistogram(string name)
        {
            Histograms.Add(name, new Histogram(NUMBEROFNS, 3));
        }

        public static void CreateHistograms()
        {
            CreateHistogram("writer-flush");
            CreateHistogram("chaser-wait");
            CreateHistogram("chaser-flush");
        }

        public static void StartJitterMonitor()
        {
            CreateHistograms();
            CreateHistogram("jitter");
            var hist = GetHistogram("jitter");
            Task.Factory.StartNew(x =>
            {
                var watch = new Stopwatch();
                watch.Start();
                while (true)
                {
                    using (hist.Measure())
                    {
                        Thread.Sleep(1);
                    }
                }
            }, null, TaskCreationOptions.LongRunning);
        }
    }
}