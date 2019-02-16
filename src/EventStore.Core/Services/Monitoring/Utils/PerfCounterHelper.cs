using System;
using System.Diagnostics;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Services.Monitoring.Stats;

namespace EventStore.Core.Services.Monitoring.Utils {
	internal class PerfCounterHelper : IDisposable {
		private const string DotNetProcessIdCounterName = "Process Id";
		private const string ProcessIdCounterName = "ID Process";
		private const string DotNetMemoryCategory = ".NET CLR Memory";
		private const string ProcessCategory = "Process";
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
		private readonly int _pid;

		public PerfCounterHelper(ILogger log) {
			_log = log;

			var currentProcess = Process.GetCurrentProcess();
			_pid = currentProcess.Id;

			_totalCpuCounter = CreatePerfCounter("Processor", "% Processor Time", "_Total");
			_totalMemCounter = CreatePerfCounter("Memory", "Available Bytes");

			var processInstanceName = GetProcessInstanceName(ProcessCategory, ProcessIdCounterName);

			if (processInstanceName != null) {
				_procCpuCounter = CreatePerfCounter(ProcessCategory, "% Processor Time", processInstanceName);
				_procThreadsCounter = CreatePerfCounter(ProcessCategory, "Thread Count", processInstanceName);
			}

			var netInstanceName = GetProcessInstanceName(DotNetMemoryCategory, DotNetProcessIdCounterName);

			if (netInstanceName != null) {
				_thrownExceptionsRateCounter =
					CreatePerfCounter(".NET CLR Exceptions", "# of Exceps Thrown / sec", netInstanceName);
				_contentionsRateCounter =
					CreatePerfCounter(".NET CLR LocksAndThreads", "Contention Rate / sec", netInstanceName);
				_gcGen0ItemsCounter = CreatePerfCounter(DotNetMemoryCategory, "# Gen 0 Collections", netInstanceName);
				_gcGen1ItemsCounter = CreatePerfCounter(DotNetMemoryCategory, "# Gen 1 Collections", netInstanceName);
				_gcGen2ItemsCounter = CreatePerfCounter(DotNetMemoryCategory, "# Gen 2 Collections", netInstanceName);
				_gcGen0SizeCounter = CreatePerfCounter(DotNetMemoryCategory, "Gen 0 heap size", netInstanceName);
				_gcGen1SizeCounter = CreatePerfCounter(DotNetMemoryCategory, "Gen 1 heap size", netInstanceName);
				_gcGen2SizeCounter = CreatePerfCounter(DotNetMemoryCategory, "Gen 2 heap size", netInstanceName);
				_gcLargeHeapSizeCounter = CreatePerfCounter(DotNetMemoryCategory, "Large Object Heap size",
					netInstanceName);
				_gcAllocationSpeedCounter =
					CreatePerfCounter(DotNetMemoryCategory, "Allocated Bytes/sec", netInstanceName);
				_gcTimeInGcCounter = CreatePerfCounter(DotNetMemoryCategory, "% Time in GC", netInstanceName);
				_gcTotalBytesInHeapsCounter =
					CreatePerfCounter(DotNetMemoryCategory, "# Bytes in all Heaps", netInstanceName);
			}
		}

		private PerformanceCounter CreatePerfCounter(string category, string counter, string instance = null) {
			try {
				return string.IsNullOrEmpty(instance)
					? new PerformanceCounter(category, counter)
					: new PerformanceCounter(category, counter, instance);
			} catch (Exception ex) {
				_log.Trace(
					"Could not create performance counter: category='{category}', counter='{counter}', instance='{instance}'. Error: {e}",
					category, counter, instance ?? string.Empty, ex.Message);
				return null;
			}
		}

		private string GetProcessInstanceName(string categoryName, string counterName) {
			// On Unix or MacOS, use the PID as the instance name
			if (Runtime.IsUnixOrMac) {
				return _pid.ToString();
			}

			// On Windows use the Performance Counter to get the name
			try {
				if (PerformanceCounterCategory.Exists(categoryName)) {
					var category = new PerformanceCounterCategory(categoryName).ReadCategory();

					if (category.Contains(counterName)) {
						var instanceDataCollection = category[counterName];

						if (instanceDataCollection.Values != null) {
							foreach (InstanceData item in instanceDataCollection.Values) {
								var instancePid = (int)item.RawValue;
								if (_pid.Equals(instancePid)) {
									return item.InstanceName;
								}
							}
						}
					}
				}
			} catch (InvalidOperationException) {
				_log.Trace("Unable to get performance counter category '{category}' instances.", categoryName);
			}

			return null;
		}


		/// <summary>
		/// Re-examines the performance counter instances for the correct instance name for this process.
		/// </summary>
		/// <remarks>
		/// The performance counter instance on .NET Framework can change at any time
		/// due to creation or destruction of processes with the same image name. This method should be called before using the Get methods.
		///
		/// The correct instance name must be found by dereferencing via a look up counter, e.g. .Net CLR Memory/Process Id
		/// </remarks>
		public void RefreshInstanceName() {
			if (!Runtime.IsWindows) {
				return;
			}

			if (_procCpuCounter != null) {
				var processInstanceName = GetProcessInstanceName(ProcessCategory, ProcessIdCounterName);

				if (processInstanceName != null) {
					if (_procCpuCounter != null) _procCpuCounter.InstanceName = processInstanceName;
					if (_procThreadsCounter != null) _procThreadsCounter.InstanceName = processInstanceName;
				}
			}

			if (_gcLargeHeapSizeCounter != null) {
				var netInstanceName = GetProcessInstanceName(DotNetMemoryCategory, DotNetProcessIdCounterName);

				if (netInstanceName != null) {
					if (_thrownExceptionsRateCounter != null)
						_thrownExceptionsRateCounter.InstanceName = netInstanceName;
					if (_contentionsRateCounter != null) _contentionsRateCounter.InstanceName = netInstanceName;
					if (_gcGen0ItemsCounter != null) _gcGen0ItemsCounter.InstanceName = netInstanceName;
					if (_gcGen1ItemsCounter != null) _gcGen1ItemsCounter.InstanceName = netInstanceName;
					if (_gcGen2ItemsCounter != null) _gcGen2ItemsCounter.InstanceName = netInstanceName;
					if (_gcGen0SizeCounter != null) _gcGen0SizeCounter.InstanceName = netInstanceName;
					if (_gcGen1SizeCounter != null) _gcGen1SizeCounter.InstanceName = netInstanceName;
					if (_gcGen2SizeCounter != null) _gcGen2SizeCounter.InstanceName = netInstanceName;
					if (_gcLargeHeapSizeCounter != null) _gcLargeHeapSizeCounter.InstanceName = netInstanceName;
					if (_gcAllocationSpeedCounter != null) _gcAllocationSpeedCounter.InstanceName = netInstanceName;
					if (_gcTimeInGcCounter != null) _gcTimeInGcCounter.InstanceName = netInstanceName;
					if (_gcTotalBytesInHeapsCounter != null) _gcTotalBytesInHeapsCounter.InstanceName = netInstanceName;
				}
			}
		}

		///<summary>
		///Total CPU usage in percentage
		///</summary>
		public float GetTotalCpuUsage() {
			return _totalCpuCounter?.NextValue() ?? InvalidCounterResult;
		}

		///<summary>
		///Free memory in bytes
		///</summary>
		public long GetFreeMemory() {
			return _totalMemCounter?.NextSample().RawValue ?? InvalidCounterResult;
		}

		///<summary>
		///Total process CPU usage
		///</summary>
		public float GetProcCpuUsage() {
			return _procCpuCounter?.NextValue() ?? InvalidCounterResult;
		}

		///<summary>
		///Current thread count
		///</summary>
		public int GetProcThreadsCount() {
			return (int)(_procThreadsCounter?.NextValue() ?? InvalidCounterResult);
		}

		///<summary>
		///Number of exceptions thrown per second
		///</summary>
		public float GetThrownExceptionsRate() {
			return _thrownExceptionsRateCounter?.NextValue() ?? InvalidCounterResult;
		}

		///<summary>
		///The rate at which threads in the runtime attempt to acquire a managed lock unsuccessfully
		///</summary>
		public float GetContentionsRateCount() {
			return _contentionsRateCounter?.NextValue() ?? InvalidCounterResult;
		}

		public GcStats GetGcStats() {
			return new GcStats(
				gcGen0Items: _gcGen0ItemsCounter?.NextSample().RawValue ?? InvalidCounterResult,
				gcGen1Items: _gcGen1ItemsCounter?.NextSample().RawValue ?? InvalidCounterResult,
				gcGen2Items: _gcGen2ItemsCounter?.NextSample().RawValue ?? InvalidCounterResult,
				gcGen0Size: _gcGen0SizeCounter?.NextSample().RawValue ?? InvalidCounterResult,
				gcGen1Size: _gcGen1SizeCounter?.NextSample().RawValue ?? InvalidCounterResult,
				gcGen2Size: _gcGen2SizeCounter?.NextSample().RawValue ?? InvalidCounterResult,
				gcLargeHeapSize: _gcLargeHeapSizeCounter?.NextSample().RawValue ?? InvalidCounterResult,
				gcAllocationSpeed: _gcAllocationSpeedCounter?.NextValue() ?? InvalidCounterResult,
				gcTimeInGc: _gcTimeInGcCounter?.NextValue() ?? InvalidCounterResult,
				gcTotalBytesInHeaps: _gcTotalBytesInHeapsCounter?.NextSample().RawValue ?? InvalidCounterResult);
		}

		public void Dispose() {
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
