using System;
using System.Diagnostics;
using EventStore.Common.Log;
using EventStore.Core.Services.Monitoring.Stats;

namespace EventStore.Core.Services.Monitoring.Utils
{
    internal class PerfCounterHelper : IDisposable
    {
        private const int InvalidCounterResult = -1;

        private readonly ILogger _log;

        private readonly PerformanceCounter _totalCpuCounter;
        private readonly PerformanceCounter _totalMemCounter; //doesn't work on mono
        private readonly PerformanceCounter _procCpuCounter;
        private readonly PerformanceCounter _procThreadsCounter;

        private readonly PerformanceCounter _thrownExceptionsRateCounter;
        private readonly PerformanceCounter _contentionsRateCounter;

        private readonly PerformanceCounter _gcGen0ItemsCounter;
        private readonly PerformanceCounter _gcGen1ItemsCounter;
        private readonly PerformanceCounter _gcGen2ItemsCounter;
        private readonly PerformanceCounter _gcGen0SizeCounter;
        private readonly PerformanceCounter _gcGen1SizeCounter;
        private readonly PerformanceCounter _gcGen2SizeCounter;
        private readonly PerformanceCounter _gcLargeHeapSizeCounter;
        private readonly PerformanceCounter _gcAllocationSpeedCounter;
        private readonly PerformanceCounter _gcTimeInGcCounter;
        private readonly PerformanceCounter _gcTotalBytesInHeapsCounter;

        public PerfCounterHelper(ILogger log)
        {
            _log = log;

            _totalCpuCounter = CreatePerfCounter("Processor", "% Processor Time", "_Total");
            _totalMemCounter = CreatePerfCounter("Memory", "Available Bytes");
            _procCpuCounter = CreatePerfCounterForProcess("Process", "% Processor Time");
            _procThreadsCounter = CreatePerfCounterForProcess("Process", "Thread Count");
            _thrownExceptionsRateCounter = CreatePerfCounterForProcess(".NET CLR Exceptions", "# of Exceps Thrown / sec");
            _contentionsRateCounter = CreatePerfCounterForProcess(".NET CLR LocksAndThreads", "Contention Rate / sec");
            _gcGen0ItemsCounter = CreatePerfCounterForProcess(".NET CLR Memory", "# Gen 0 Collections");
            _gcGen1ItemsCounter = CreatePerfCounterForProcess(".NET CLR Memory", "# Gen 1 Collections");
            _gcGen2ItemsCounter = CreatePerfCounterForProcess(".NET CLR Memory", "# Gen 2 Collections");
            _gcGen0SizeCounter = CreatePerfCounterForProcess(".NET CLR Memory", "Gen 0 heap size");
            _gcGen1SizeCounter = CreatePerfCounterForProcess(".NET CLR Memory", "Gen 1 heap size");
            _gcGen2SizeCounter = CreatePerfCounterForProcess(".NET CLR Memory", "Gen 2 heap size");
            _gcLargeHeapSizeCounter = CreatePerfCounterForProcess(".NET CLR Memory", "Large Object Heap size");
            _gcAllocationSpeedCounter = CreatePerfCounterForProcess(".NET CLR Memory", "Allocated Bytes/sec");
            _gcTimeInGcCounter = CreatePerfCounterForProcess(".NET CLR Memory", "% Time in GC");
            _gcTotalBytesInHeapsCounter = CreatePerfCounterForProcess(".NET CLR Memory", "# Bytes in all Heaps");
        }

        private PerformanceCounter CreatePerfCounterForProcess(string category, string counter)
        {
            string processName = null;
            try
            {
                #if MONO
                processName = Process.GetCurrentProcess().Id.ToString();
                #else
                processName = Process.GetCurrentProcess().ProcessName;
                #endif
                return CreatePerfCounter(category, counter, processName);
            }
            catch (Exception ex)
            {
                _log.Trace("Could not create performance counter: category='{0}', counter='{1}', instance='{2}'. Error: {3}",
                           category, counter, processName ?? "<!error getting process name!>", ex.Message);
                return null;
            }
        }

        private PerformanceCounter CreatePerfCounter(string category, string counter, string instance = null)
        {
            try
            {
                return string.IsNullOrEmpty(instance)
                               ? new PerformanceCounter(category, counter)
                               : new PerformanceCounter(category, counter, instance);
            }
            catch (Exception ex)
            {
                _log.Trace("Could not create performance counter: category='{0}', counter='{1}', instance='{2}'. Error: {3}",
                           category, counter, instance ?? string.Empty, ex.Message);
                return null;
            }
        }

        public float GetTotalCpuUsage() => _totalCpuCounter?.NextValue() ?? InvalidCounterResult;
        public long GetFreeMemory() => _totalMemCounter?.NextSample().RawValue ?? InvalidCounterResult;
        public float GetProcCpuUsage() => _procCpuCounter?.NextValue() ?? InvalidCounterResult;
        public int GetProcThreadsCount() => (int?)_procThreadsCounter?.NextValue() ?? InvalidCounterResult;
        public float GetThrownExceptionsRate() => _thrownExceptionsRateCounter?.NextValue() ?? InvalidCounterResult;
        public float GetContentionsRateCount() => _contentionsRateCounter?.NextValue() ?? InvalidCounterResult;

        public GcStats GetGcStats()
        {
            return new GcStats(
                gcGen0Items:            _gcGen0ItemsCounter?.NextSample().RawValue          ?? InvalidCounterResult,
                gcGen1Items:            _gcGen1ItemsCounter?.NextSample().RawValue          ?? InvalidCounterResult,
                gcGen2Items:            _gcGen2ItemsCounter?.NextSample().RawValue          ?? InvalidCounterResult,
                gcGen0Size:             _gcGen0SizeCounter?.NextSample().RawValue           ?? InvalidCounterResult,
                gcGen1Size:             _gcGen1SizeCounter?.NextSample().RawValue           ?? InvalidCounterResult,
                gcGen2Size:             _gcGen2SizeCounter?.NextSample().RawValue           ?? InvalidCounterResult,
                gcLargeHeapSize:        _gcLargeHeapSizeCounter?.NextSample().RawValue      ?? InvalidCounterResult,
                gcAllocationSpeed:      _gcAllocationSpeedCounter?.NextValue()              ?? InvalidCounterResult,
                gcTimeInGc:             _gcTimeInGcCounter?.NextValue()                     ?? InvalidCounterResult,
                gcTotalBytesInHeaps:    _gcTotalBytesInHeapsCounter?.NextSample().RawValue  ?? InvalidCounterResult);
        }

        public void Dispose()
        {
            _totalCpuCounter?.Dispose();
            _totalMemCounter?.Dispose();
            _procCpuCounter?.Dispose();
            _procThreadsCounter?.Dispose();

            _thrownExceptionsRateCounter?.Dispose();
            _contentionsRateCounter?.Dispose();

            _gcGen0ItemsCounter?.Dispose();
            _gcGen1ItemsCounter?.Dispose();
            _gcGen2ItemsCounter?.Dispose();
            _gcGen0SizeCounter?.Dispose();
            _gcGen1SizeCounter?.Dispose();
            _gcGen2SizeCounter?.Dispose();
            _gcLargeHeapSizeCounter?.Dispose();
            _gcAllocationSpeedCounter?.Dispose();
            _gcTimeInGcCounter?.Dispose();
            _gcTotalBytesInHeapsCounter?.Dispose();
        }
    }
}
