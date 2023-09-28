#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Common.Utils;
using EventStore.Core.Services.Monitoring.Stats;
using EventStore.Core.Services.Monitoring.Utils;
using EventStore.Core.Util;
using EventStore.Native.Monitoring;
using static EventStore.Common.Configuration.MetricsConfiguration;

namespace EventStore.Core.Metrics;

public class SystemMetrics {
	private static readonly Serilog.ILogger _log = Serilog.Log.ForContext<SystemMetrics>();

	private readonly Meter _meter;
	private readonly TimeSpan _timeout;
	private readonly Dictionary<SystemTracker, bool> _config;
	private readonly HostStat.HostStat _stats;
	private readonly PerfCounterHelper _perfCounter;

	public SystemMetrics(
		Meter meter,
		TimeSpan timeout,
		Dictionary<SystemTracker, bool> config) {

		_meter = meter;
		_timeout = timeout;
		_config = config;
		_stats = new HostStat.HostStat();
		_perfCounter = new PerfCounterHelper(_log);
	}

	public void CreateLoadAverageMetric(string metricName, Dictionary<SystemTracker, string> dimNames) {
		if (!OS.IsUnix)
			return;

		var dims = new Dimensions<SystemTracker, double>(_config, dimNames, tag => new("period", tag));

		var getLoadAverages = Functions.Debounce(_stats.GetLoadAverages, _timeout);
		dims.Register(SystemTracker.LoadAverage1m, () => getLoadAverages().Average1m);
		dims.Register(SystemTracker.LoadAverage5m, () => getLoadAverages().Average5m);
		dims.Register(SystemTracker.LoadAverage15m, () => getLoadAverages().Average15m);

		if (dims.AnyRegistered())
			_meter.CreateObservableGauge(metricName, dims.GenObserve());
	}

	public void CreateCpuMetric(string name) {
		if (OS.IsUnix || !_config.TryGetValue(SystemTracker.Cpu, out var enabled) || !enabled)
			return;

		_meter.CreateObservableUpDownCounter(name, _perfCounter.GetTotalCpuUsage);
	}

	public void CreateMemoryMetric(string metricName, Dictionary<SystemTracker, string> dimNames) {
		var dims = new Dimensions<SystemTracker, long>(_config, dimNames, tag => new("kind", tag));

		if (OS.IsUnix) {
			dims.Register(SystemTracker.FreeMem, () => (long)_stats.GetFreeMemory());
			dims.Register(SystemTracker.TotalMem, () => (long)_stats.GetTotalMemory());
		} else {
			dims.Register(SystemTracker.FreeMem, _perfCounter.GetFreeMemory);
			dims.Register(SystemTracker.TotalMem, () => (long)WinNativeMemoryStatus.GetTotalMemory());
		}

		if (dims.AnyRegistered())
			_meter.CreateObservableGauge(metricName, dims.GenObserve(), "bytes");
	}

	public void CreateDiskMetric(string metricName, string dbPath, Dictionary<SystemTracker, string> dimNames) {
		var dims = new Dimensions<SystemTracker, long>(_config, dimNames, tag => new());

		var getDriveInfo = Functions.Debounce(() => EsDriveInfo.FromDirectory(dbPath, _log), _timeout);

		Func<string, Measurement<long>> GenMeasure(Func<EsDriveInfo, long> func) => tag => {
			var info = getDriveInfo();
			if (info is null)
				return new();

			return new(
				func(info),
				new KeyValuePair<string, object?>[] {
					new("kind", tag),
					new("disk", info.DiskName),
				});
		};

		dims.Register(SystemTracker.DriveUsedBytes, GenMeasure(info => info.UsedBytes));
		dims.Register(SystemTracker.DriveTotalBytes, GenMeasure(info => info.TotalBytes));

		if (dims.AnyRegistered())
			_meter.CreateObservableGauge(metricName, dims.GenObserve(), "bytes");
	}
}
