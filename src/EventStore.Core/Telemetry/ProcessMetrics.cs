#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Threading;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Util;
using static EventStore.Common.Configuration.TelemetryConfiguration;

namespace EventStore.Core.Telemetry;

public class ProcessMetrics {
	private static readonly Serilog.ILogger _log = Serilog.Log.ForContext<ProcessMetrics>();

	private readonly Meter _meter;
	private readonly TimeSpan _timeout;
	private readonly Dictionary<ProcessTracker, bool> _config;
	private readonly Func<DiskIo> _getDiskIo;
	private readonly Func<Process> _getCurrentProc;

	public ProcessMetrics(Meter meter, TimeSpan timeout, Dictionary<ProcessTracker, bool> config) {
		_meter = meter;
		_timeout = timeout;
		_config = config;

		_getDiskIo = Functions.Debounce(() => DiskIo.GetDiskIo(Environment.ProcessId, _log), timeout);
		_getCurrentProc = Functions.Debounce(Process.GetCurrentProcess, _timeout);
	}

	public void CreateObservableMetrics(Dictionary<ProcessTracker, string> metricNames) {
		var enabledNames = metricNames
			.Where(kvp => _config.TryGetValue(kvp.Key, out var enabled) && enabled)
			.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

		var getProcCpuUsage = typeof(GC)
			.Assembly
			.GetType("System.Diagnostics.Tracing.RuntimeEventSourceHelper")!
			.GetMethod("GetCpuUsage", BindingFlags.Static | BindingFlags.NonPublic)!
			.CreateDelegate<Func<int>>();

		var getExceptionCount = typeof(Exception)
			.GetMethod("GetExceptionCount", BindingFlags.Static | BindingFlags.NonPublic)!
			.CreateDelegate<Func<uint>>();

		var getPercentTimeInGc = typeof(GC)
			.GetMethod("GetLastGCPercentTimeInGC", BindingFlags.Static | BindingFlags.NonPublic)!
			.CreateDelegate<Func<int>>();

		void CreateObservableUpDownCounter<T>(ProcessTracker tracker, Func<T> observe, string? unit = null)
			where T : struct {

			if (enabledNames.TryGetValue(tracker, out var name))
				_meter.CreateObservableUpDownCounter(name, observe, unit);
		}

		void CreateObservableCounter<T>(ProcessTracker tracker, Func<T> observe, string? unit = null)
			where T : struct {

			if (enabledNames.TryGetValue(tracker, out var name))
				_meter.CreateObservableCounter(name, observe);
		}

		if (enabledNames.TryGetValue(ProcessTracker.UpTime, out var upTimeName))
			_meter.CreateObservableCounter(upTimeName, () => {
				var process = _getCurrentProc();
				var upTime = (DateTime.Now - process.StartTime).TotalSeconds;
				return new Measurement<double>(upTime, new KeyValuePair<string, object?>("pid", process.Id));
			});

		CreateObservableCounter(ProcessTracker.Cpu, getProcCpuUsage);
		CreateObservableCounter(ProcessTracker.LockContentionCount, () => Monitor.LockContentionCount);
		CreateObservableCounter(ProcessTracker.ExceptionCount, () => (int)getExceptionCount());
		CreateObservableCounter(ProcessTracker.TotalAllocatedBytes, () => GC.GetTotalAllocatedBytes(), "bytes");

		CreateObservableUpDownCounter(ProcessTracker.ThreadCount, () => ThreadPool.ThreadCount);
		CreateObservableUpDownCounter(ProcessTracker.TimeInGc, getPercentTimeInGc);
		CreateObservableUpDownCounter(ProcessTracker.HeapSize, () => GC.GetGCMemoryInfo().HeapSizeBytes, "bytes");
		CreateObservableUpDownCounter(ProcessTracker.HeapFragmentation, () => {
			var info = GC.GetGCMemoryInfo();
			return info.HeapSizeBytes != 0 ? info.FragmentedBytes * 100d / info.HeapSizeBytes : 0;
		});
	}

	public void CreateMemoryMetric(string metricName, Dictionary<ProcessTracker, string> dimNames) {
		var dims = new Dimensions<ProcessTracker, long>(_config, dimNames, tag => new("kind", tag));

		dims.Register(ProcessTracker.MemWorkingSet, () => _getCurrentProc().WorkingSet64);

		if (dims.AnyRegistered())
			_meter.CreateObservableGauge(metricName, dims.GenObserve(), "bytes");
	}

	public void CreateGcGenerationSizeMetric(string metricName, Dictionary<ProcessTracker, string> dimNames) {
		var dims = new Dimensions<ProcessTracker, long>(_config, dimNames, tag => new("generation", tag));

		var getGcGenerationSize = typeof(GC)
			.GetMethod("GetGenerationSize", BindingFlags.Static | BindingFlags.NonPublic)!
			.CreateDelegate<Func<int, ulong>>();

		dims.Register(ProcessTracker.Gen0Size, () => (long)getGcGenerationSize(0));
		dims.Register(ProcessTracker.Gen1Size, () => (long)getGcGenerationSize(1));
		dims.Register(ProcessTracker.Gen2Size, () => (long)getGcGenerationSize(2));
		dims.Register(ProcessTracker.LohSize, () => (long)getGcGenerationSize(3));

		if (dims.AnyRegistered())
			_meter.CreateObservableUpDownCounter(metricName, dims.GenObserve(), "bytes");
	}

	public void CreateGcCollectionCountMetric(string metricName, Dictionary<ProcessTracker, string> dimNames) {
		var dims = new Dimensions<ProcessTracker, int>(_config, dimNames, tag => new("generation", tag));

		dims.Register(ProcessTracker.Gen0CollectionCount, () => GC.CollectionCount(0));
		dims.Register(ProcessTracker.Gen1CollectionCount, () => GC.CollectionCount(1));
		dims.Register(ProcessTracker.Gen2CollectionCount, () => GC.CollectionCount(2));

		if (dims.AnyRegistered())
			_meter.CreateObservableCounter(metricName, dims.GenObserve());
	}

	public void CreateDiskBytesMetric(string metricName, Dictionary<ProcessTracker, string> dimNames) {
		var dims = new Dimensions<ProcessTracker, long>(_config, dimNames, tag => new("activity", tag));

		dims.Register(ProcessTracker.DiskReadBytes, () => (long)(_getDiskIo()?.ReadBytes ?? 0));
		dims.Register(ProcessTracker.DiskWrittenBytes, () => (long)(_getDiskIo()?.WrittenBytes ?? 0));

		if (dims.AnyRegistered())
			_meter.CreateObservableCounter(metricName, dims.GenObserve(), "bytes");
	}

	public void CreateDiskOpsMetric(string name, Dictionary<ProcessTracker, string> dimNames) {
		var dims = new Dimensions<ProcessTracker, long>(_config, dimNames, tag => new("activity", tag));

		dims.Register(ProcessTracker.DiskReadOps, () => (long)(_getDiskIo()?.ReadOps ?? 0));
		dims.Register(ProcessTracker.DiskWrittenOps, () => (long)(_getDiskIo()?.WriteOps ?? 0));

		if (dims.AnyRegistered())
			_meter.CreateObservableCounter(name, dims.GenObserve(), "operations");
	}
}
