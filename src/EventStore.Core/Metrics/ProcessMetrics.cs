#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Threading;
using EventStore.Core.Util;

using static EventStore.Common.Configuration.MetricsConfiguration;

namespace EventStore.Core.Metrics;

public class ProcessMetrics (Meter meter, TimeSpan timeout, int scrapingPeriodInSeconds, Dictionary<ProcessTracker, bool> config) {
    private readonly Func<DiskIoData> _getDiskIo      = Functions.Debounce(ProcessStats.GetDiskIo, timeout);
    private readonly Func<Process>    _getCurrentProc = Functions.Debounce(Process.GetCurrentProcess, timeout);

    public void CreateObservableMetrics(Dictionary<ProcessTracker, string> metricNames) {
		var enabledNames = metricNames
			.Where(kvp => config.TryGetValue(kvp.Key, out var enabled) && enabled)
			.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (enabledNames.TryGetValue(ProcessTracker.UpTime, out var upTimeName))
			meter.CreateObservableCounter(upTimeName, () => {
				var process = _getCurrentProc();
				var upTime = (DateTime.Now - process.StartTime).TotalSeconds;
				return new Measurement<double>(upTime, new KeyValuePair<string, object?>("pid", process.Id));
			});

		if (enabledNames.TryGetValue(ProcessTracker.GcPauseDuration, out var gcMaxPauseName)) {
			var maxGcPauseDurationMetric = new DurationMaxMetric(meter, gcMaxPauseName);
			var maxGcPauseDurationTracker = new DurationMaxTracker(maxGcPauseDurationMetric, null, scrapingPeriodInSeconds);
			_ = new GcSuspensionMetric(maxGcPauseDurationTracker);
		}

		CreateObservableCounter(ProcessTracker.LockContentionCount, () => Monitor.LockContentionCount);
		CreateObservableCounter(ProcessTracker.ExceptionCount, RuntimeStats.GetExceptionCount);
		CreateObservableCounter(ProcessTracker.TotalAllocatedBytes, () => GC.GetTotalAllocatedBytes(), "bytes");

		CreateObservableUpDownCounter(ProcessTracker.Cpu, RuntimeStats.GetCpuUsage);
		CreateObservableUpDownCounter(ProcessTracker.ThreadCount, () => ThreadPool.ThreadCount);
		CreateObservableUpDownCounter(ProcessTracker.ThreadPoolPendingWorkItemCount, () => ThreadPool.PendingWorkItemCount);
		CreateObservableUpDownCounter(ProcessTracker.TimeInGc, RuntimeStats.GetLastGCPercentTimeInGC);
		CreateObservableUpDownCounter(ProcessTracker.HeapSize, () => GC.GetGCMemoryInfo().HeapSizeBytes, "bytes");
		CreateObservableUpDownCounter(ProcessTracker.HeapFragmentation, () => {
			var info = GC.GetGCMemoryInfo();
			return info.HeapSizeBytes != 0 ? info.FragmentedBytes * 100d / info.HeapSizeBytes : 0;
		});
        
        return;

        void CreateObservableCounter<T>(ProcessTracker tracker, Func<T> observe, string? unit = null) where T : struct {
            if (enabledNames.TryGetValue(tracker, out var name))
                meter.CreateObservableCounter(name, observe);
        }

        void CreateObservableUpDownCounter<T>(ProcessTracker tracker, Func<T> observe, string? unit = null) where T : struct {
            if (enabledNames.TryGetValue(tracker, out var name))
                meter.CreateObservableUpDownCounter(unit is null ? name : $"{name}-{unit}", observe);
        }
    }

	public void CreateMemoryMetric(string metricName, Dictionary<ProcessTracker, string> dimNames) {
		var dims = new Dimensions<ProcessTracker, long>(config, dimNames, tag => new("kind", tag));

		dims.Register(ProcessTracker.MemWorkingSet, () => _getCurrentProc().WorkingSet64);
		dims.Register(ProcessTracker.MemPagedBytes, () => _getCurrentProc().PagedMemorySize64);
		dims.Register(ProcessTracker.MemVirtualBytes, () => _getCurrentProc().VirtualMemorySize64);

		if (dims.AnyRegistered())
			meter.CreateObservableGauge($"{metricName}-bytes", dims.GenObserve());
	}

	public void CreateGcGenerationSizeMetric(string metricName, Dictionary<ProcessTracker, string> dimNames) {
		var dims = new Dimensions<ProcessTracker, long>(config, dimNames, tag => new("generation", tag));

		var getGcGenerationSize = typeof(GC)
			.GetMethod("GetGenerationSize", BindingFlags.Static | BindingFlags.NonPublic)!
			.CreateDelegate<Func<int, ulong>>();

		dims.Register(ProcessTracker.Gen0Size, () => (long)getGcGenerationSize(0));
		dims.Register(ProcessTracker.Gen1Size, () => (long)getGcGenerationSize(1));
		dims.Register(ProcessTracker.Gen2Size, () => (long)getGcGenerationSize(2));
		dims.Register(ProcessTracker.LohSize, () => (long)getGcGenerationSize(3));

		if (dims.AnyRegistered())
			meter.CreateObservableUpDownCounter($"{metricName}-bytes", dims.GenObserve());
	}

	public void CreateGcCollectionCountMetric(string metricName, Dictionary<ProcessTracker, string> dimNames) {
		var dims = new Dimensions<ProcessTracker, int>(config, dimNames, tag => new("generation", tag));

		dims.Register(ProcessTracker.Gen0CollectionCount, () => GC.CollectionCount(0));
		dims.Register(ProcessTracker.Gen1CollectionCount, () => GC.CollectionCount(1));
		dims.Register(ProcessTracker.Gen2CollectionCount, () => GC.CollectionCount(2));

		if (dims.AnyRegistered())
			meter.CreateObservableCounter(metricName, dims.GenObserve());
	}

	public void CreateDiskBytesMetric(string metricName, Dictionary<ProcessTracker, string> dimNames) {
		var dims = new Dimensions<ProcessTracker, long>(config, dimNames, tag => new("activity", tag));

		dims.Register(ProcessTracker.DiskReadBytes, () => (long)_getDiskIo().ReadBytes);
		dims.Register(ProcessTracker.DiskWrittenBytes, () => (long)_getDiskIo().WrittenBytes);

		if (dims.AnyRegistered())
			meter.CreateObservableCounter($"{metricName}-bytes", dims.GenObserve());
	}

	public void CreateDiskOpsMetric(string name, Dictionary<ProcessTracker, string> dimNames) {
		var dims = new Dimensions<ProcessTracker, long>(config, dimNames, tag => new("activity", tag));

		dims.Register(ProcessTracker.DiskReadOps, () => (long)_getDiskIo().ReadOps);
		dims.Register(ProcessTracker.DiskWrittenOps, () => (long)_getDiskIo().WriteOps);

		if (dims.AnyRegistered())
			meter.CreateObservableCounter($"{name}-operations", dims.GenObserve());
	}
}
