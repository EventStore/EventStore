
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

        public static void StartJitterMonitor()
        {
            CreateHistogram("jitter");
            var hist = GetHistogram("jitter");
            Task.Factory.StartNew(x =>
            {
                var watch = new Stopwatch();
                watch.Start();
                while (true)
                {
                    var start = watch.ElapsedTicks;
                    Thread.Sleep(1);
                    hist.recordValue((long)((((double)watch.ElapsedTicks - start) / Stopwatch.Frequency) * 1000000000));
                }
            }, null, TaskCreationOptions.LongRunning);
        }
    }
}