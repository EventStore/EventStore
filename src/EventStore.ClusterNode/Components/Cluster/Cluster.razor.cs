using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using MudBlazor;

namespace EventStore.ClusterNode.Components.Cluster;

public partial class Cluster {
	ClientClusterInfo _clusterInfo;
	Timer _timer;

	protected override async Task OnInitializedAsync() {
		await base.OnInitializedAsync();
		var now = DateTime.Now;
		var empty = Enumerable.Range(-360, 360).Select(x => new TimeSeriesChartSeries.TimeValue(now.AddSeconds(x * 10), 0)).ToList();
		_cpuChart = new() {
			Index = 0,
			Name = "CPU %",
			Data = empty,
			IsVisible = true,
			Type = TimeSeriesDisplayType.Area
		};
		_ramChart = new() {
			Index = 0,
			Name = "RAM %",
			Data = empty,
			IsVisible = true,
			Type = TimeSeriesDisplayType.Area
		};
		_series.Add(_cpuChart);
		_series.Add(_ramChart);

		_timer = new(Callback, null, 0, 1000);
		MonitoringService.DataUpdated += MonitoringServiceOnDataUpdated;
	}

	async Task RefreshStatus() {
		using var cts = new CancellationTokenSource(1000);
		var r = await RequestClient.RequestAsync<GossipMessage.ClientGossip, GossipMessage.SendClientGossip>(Publisher, env => new(env), cts.Token);
		_clusterInfo = r.ClusterInfo;
	}

	void Callback(object state) {
		Task.Run(RefreshStatus);
		_cpu = MonitoringService.CalculateCpu() * 100;
		var ram = MonitoringService.CalculateRam();
		_ram = ram.Used / ram.Total * 100;
		AddData(_cpuChart, _cpu);
		AddData(_ramChart, _ram);

		Refresh();
		return;

		void AddData(TimeSeriesChartSeries series, double value) {
			var newData = new List<TimeSeriesChartSeries.TimeValue>();
			newData.AddRange(series.Data.Skip(1));
			newData.Add(new(DateTime.Now, value));
			series.Data = newData;
		}
	}

	void MonitoringServiceOnDataUpdated(object sender, EventArgs e) {
		Refresh();
	}

	void Refresh() {
		Task.Run(() => InvokeAsync(StateHasChanged));
	}

	readonly ChartOptions _options = new() {
		YAxisLines = false,
		YAxisTicks = 100,
		MaxNumYAxisTicks = 10,
		YAxisRequireZeroPoint = true,
		XAxisLines = false,
		LineStrokeWidth = 1,
		// InterpolationOption = InterpolationOption.NaturalSpline
	};

	TimeSeriesChartSeries _cpuChart;
	TimeSeriesChartSeries _ramChart;
	readonly List<TimeSeriesChartSeries> _series = [];
	int _index = -1;

	bool IsClusterHealthy => _clusterInfo?.Members?.All(x => x.IsAlive) ?? true;
	string ClusterIcon => IsClusterHealthy ? Icons.Material.Filled.Check : Icons.Material.Filled.Warning;
	Color ClusterIconColor => IsClusterHealthy ? Color.Success : Color.Warning;
	int CardWidth => 12 / (_clusterInfo?.Members?.Length ?? 1);
	static string LocalOs => RuntimeInformation.OSDescription;
	double _cpu;
	double _ram;

	public void Dispose() {
		MonitoringService.DataUpdated -= MonitoringServiceOnDataUpdated;
		_timer.Dispose();
	}
}
