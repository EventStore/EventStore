// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
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
        private bool _disposed = false;

        private readonly PerformanceCounter totalCpuCounter;
        private readonly PerformanceCounter totalMemCounter; //doesn't work on mono
        private readonly PerformanceCounter procCpuCounter;
        private readonly PerformanceCounter procThreadsCounter;

        private readonly PerformanceCounter exceptionsCounter;
        private readonly PerformanceCounter exceptionsRateCounter;

        private readonly PerformanceCounter contentionsCounter;
        private readonly PerformanceCounter contentionsRateCounter;

        private readonly PerformanceCounter gcGen0ItemsCounter;
        private readonly PerformanceCounter gcGen1ItemsCounter;
        private readonly PerformanceCounter gcGen2ItemsCounter;
        private readonly PerformanceCounter gcGen0SizeCounter;
        private readonly PerformanceCounter gcGen1SizeCounter;
        private readonly PerformanceCounter gcGen2SizeCounter;
        private readonly PerformanceCounter gcLargeHeapSizeCounter;
        private readonly PerformanceCounter gcAllocationSpeedCounter;
        private readonly PerformanceCounter gcTimeInGcCounter;
        private readonly PerformanceCounter gcTotalBytesInHeapsCounter;

        private bool _allCountersEnabled;

        public PerfCounterHelper(ILogger log)
        {
            _log = log;
            _allCountersEnabled = true;
           // return;

            SafeAssignPerfCounter(out totalCpuCounter, "Processor", "% Processor Time", "_Total");
            SafeAssignPerfCounter(out totalMemCounter, "Memory", "Available Bytes");
            SafeAssignPerfCounterForProcess(out procCpuCounter, "Process", "% Processor Time");
            SafeAssignPerfCounterForProcess(out procThreadsCounter, "Process", "Thread Count");
            SafeAssignPerfCounterForProcess(out exceptionsCounter, ".NET CLR Exceptions", "# of Exceps Thrown");
            SafeAssignPerfCounterForProcess(out exceptionsRateCounter, ".NET CLR Exceptions", "# of Exceps Thrown / sec");
            SafeAssignPerfCounterForProcess(out contentionsCounter, ".NET CLR LocksAndThreads", "Total # of Contentions");
            SafeAssignPerfCounterForProcess(out contentionsRateCounter, ".NET CLR LocksAndThreads", "Contention Rate / sec");
            SafeAssignPerfCounterForProcess(out gcGen0ItemsCounter, ".NET CLR Memory", "# Gen 0 Collections");
            SafeAssignPerfCounterForProcess(out gcGen1ItemsCounter, ".NET CLR Memory", "# Gen 1 Collections");
            SafeAssignPerfCounterForProcess(out gcGen2ItemsCounter, ".NET CLR Memory", "# Gen 2 Collections");
            SafeAssignPerfCounterForProcess(out gcGen0SizeCounter, ".NET CLR Memory", "Gen 0 heap size");
            SafeAssignPerfCounterForProcess(out gcGen1SizeCounter, ".NET CLR Memory", "Gen 1 heap size");
            SafeAssignPerfCounterForProcess(out gcGen2SizeCounter, ".NET CLR Memory", "Gen 2 heap size");
            SafeAssignPerfCounterForProcess(out gcLargeHeapSizeCounter, ".NET CLR Memory", "Large Object Heap size");
            SafeAssignPerfCounterForProcess(out gcAllocationSpeedCounter, ".NET CLR Memory", "Allocated Bytes/sec");
            SafeAssignPerfCounterForProcess(out gcTimeInGcCounter, ".NET CLR Memory", "% Time in GC");
            SafeAssignPerfCounterForProcess(out gcTotalBytesInHeapsCounter, ".NET CLR Memory", "# Bytes in all Heaps");

            if (!_allCountersEnabled)
            {
                _log.Error("Couldn't create some performance counters. Try running application with administrators rights.");
            }
        }

        private void SafeAssignPerfCounterForProcess(out PerformanceCounter field, string category, string counter)
        {
            SafeAssignPerfCounter(out field, category, counter, Process.GetCurrentProcess().ProcessName);
        }

        private void SafeAssignPerfCounter(out PerformanceCounter field, string category, string counter, string instance = null)
        {
            try
            {
                field = string.IsNullOrEmpty(instance)
                        ? new PerformanceCounter(category, counter)
                        : new PerformanceCounter(category, counter, instance);
            }
            catch (Exception ex)
            {
                field = null;
                _allCountersEnabled = false;
                _log.TraceException(ex, "Couldn't create performance counter: " +
                                        "category='{0}', counter='{1}', instance='{2}'. Error: {3}",
                                        category, counter, instance ?? string.Empty, ex.Message);
            }
        }

        public float GetTotalCpuUsage()
        {
            return totalCpuCounter != null ? totalCpuCounter.NextValue() : InvalidCounterResult;
        }

        public long GetFreeMemory()
        {
            if (totalMemCounter != null) return totalMemCounter.NextSample().RawValue;
            return InvalidCounterResult;
        }

        public float GetProcCpuUsage()
        {
            if (procCpuCounter != null) return procCpuCounter.NextValue();
            return InvalidCounterResult;
        }

        public int GetProcThreadsCount()
        {
            if (procThreadsCounter != null) return (int)procThreadsCounter.NextValue();
            return InvalidCounterResult;
        }

        public float GetThrownExceptionsCount()
        {
            if (exceptionsCounter != null) return exceptionsCounter.NextValue();
            return InvalidCounterResult;
        }

        public float GetExceptionsThrowingRate()
        {
            if (exceptionsRateCounter != null) return exceptionsRateCounter.NextValue();
            return InvalidCounterResult;
        }

        public float GetContentionsCount()
        {
            if (contentionsCounter != null) return contentionsCounter.NextValue();
            return InvalidCounterResult;
        }

        public float GetContentionsRateCount()
        {
            if (contentionsRateCounter != null) return contentionsRateCounter.NextValue();
            return InvalidCounterResult;
        }

        public GcStats GetGcStats()
        {
            return new GcStats(
                gcGen0Items: gcGen0ItemsCounter != null ? gcGen0ItemsCounter.NextSample().RawValue : InvalidCounterResult,
                gcGen1Items: gcGen1ItemsCounter != null ? gcGen1ItemsCounter.NextSample().RawValue : InvalidCounterResult,
                gcGen2Items: gcGen2ItemsCounter != null ? gcGen2ItemsCounter.NextSample().RawValue : InvalidCounterResult,
                gcGen0Size: gcGen0SizeCounter != null ? gcGen0SizeCounter.NextSample().RawValue : InvalidCounterResult,
                gcGen1Size: gcGen1SizeCounter != null ? gcGen1SizeCounter.NextSample().RawValue : InvalidCounterResult,
                gcGen2Size: gcGen2SizeCounter != null ? gcGen2SizeCounter.NextSample().RawValue : InvalidCounterResult,
                gcLargeHeapSize: gcLargeHeapSizeCounter != null ? gcLargeHeapSizeCounter.NextSample().RawValue : InvalidCounterResult,
                gcAllocationSpeed: gcAllocationSpeedCounter != null ? gcAllocationSpeedCounter.NextValue() : InvalidCounterResult,
                gcTimeInGc: gcTimeInGcCounter != null ? gcTimeInGcCounter.NextValue() : InvalidCounterResult,
                gcTotalBytesInHeaps: gcTotalBytesInHeapsCounter != null ? gcTotalBytesInHeapsCounter.NextSample().RawValue : InvalidCounterResult);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            DoDispose();
            _disposed = true;
        }

        private void DoDispose()
        {
            if (totalCpuCounter != null) totalCpuCounter.Dispose();
            if (totalMemCounter != null) totalMemCounter.Dispose();
            if (procCpuCounter != null) procCpuCounter.Dispose();
            if (procThreadsCounter != null) procThreadsCounter.Dispose();

            if (exceptionsCounter != null) exceptionsCounter.Dispose();
            if (exceptionsRateCounter != null) exceptionsRateCounter.Dispose();

            if (contentionsCounter != null) contentionsCounter.Dispose();
            if (contentionsRateCounter != null) contentionsRateCounter.Dispose();

            if (gcGen0ItemsCounter != null) gcGen0ItemsCounter.Dispose();
            if (gcGen1ItemsCounter != null) gcGen1ItemsCounter.Dispose();
            if (gcGen2ItemsCounter != null) gcGen2ItemsCounter.Dispose();
            if (gcGen0SizeCounter != null) gcGen0SizeCounter.Dispose();
            if (gcGen1SizeCounter != null) gcGen1SizeCounter.Dispose();
            if (gcGen2SizeCounter != null) gcGen2SizeCounter.Dispose();
            if (gcLargeHeapSizeCounter != null) gcLargeHeapSizeCounter.Dispose();
            if (gcAllocationSpeedCounter != null) gcAllocationSpeedCounter.Dispose();
            if (gcTimeInGcCounter != null) gcTimeInGcCounter.Dispose();
            if (gcTotalBytesInHeapsCounter != null) gcTotalBytesInHeapsCounter.Dispose();
        }

        ~PerfCounterHelper()
        {
            Dispose(disposing: false);
        }
    }
}