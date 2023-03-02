using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using EventStore.Common.Configuration;
using EventStore.Core.Telemetry;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Telemetry;

public class ProcessMetricsTests : IDisposable {
	private readonly TestMeterListener<int> _intListener;
	private readonly TestMeterListener<double> _doubleListener;
	private readonly TestMeterListener<long> _longListener;
	private readonly ProcessMetrics _sut;

	public ProcessMetricsTests() {
		var meter = new Meter($"{typeof(ProcessMetricsTests)}");
		_intListener = new TestMeterListener<int>(meter);
		_doubleListener = new TestMeterListener<double>(meter);
		_longListener = new TestMeterListener<long>(meter);

		var config = new Dictionary<TelemetryConfiguration.ProcessTracker, bool>();

		foreach (var value in Enum.GetValues<TelemetryConfiguration.ProcessTracker>()) {
			config[value] = true;
		}

		_sut = new ProcessMetrics(meter, TimeSpan.FromSeconds(42), config);
		_sut.CreateObservableMetrics(new() {
			{ TelemetryConfiguration.ProcessTracker.UpTime, "eventstore-proc-up-time" },
			{ TelemetryConfiguration.ProcessTracker.Cpu, "eventstore-proc-cpu" },
			{ TelemetryConfiguration.ProcessTracker.ThreadCount, "eventstore-proc-thread-count" },
			{ TelemetryConfiguration.ProcessTracker.LockContentionCount, "eventstore-proc-contention-count" },
			{ TelemetryConfiguration.ProcessTracker.ExceptionCount, "eventstore-proc-exception-count" },
			{ TelemetryConfiguration.ProcessTracker.TimeInGc, "eventstore-gc-time-in-gc" },
			{ TelemetryConfiguration.ProcessTracker.HeapSize, "eventstore-gc-heap-size" },
			{ TelemetryConfiguration.ProcessTracker.HeapFragmentation, "eventstore-gc-heap-fragmentation" },
			{ TelemetryConfiguration.ProcessTracker.TotalAllocatedBytes, "eventstore-gc-total-allocated" },
		});

		_sut.CreateMemoryMetric("eventstore-proc-mem", new() {
			{ TelemetryConfiguration.ProcessTracker.MemWorkingSet, "working-set" },
		});

		_sut.CreateGcGenerationSizeMetric("eventstore-gc-generation-size", new() {
			{ TelemetryConfiguration.ProcessTracker.Gen0Size, "gen0" },
			{ TelemetryConfiguration.ProcessTracker.Gen1Size, "gen1" },
			{ TelemetryConfiguration.ProcessTracker.Gen2Size, "gen2" },
			{ TelemetryConfiguration.ProcessTracker.LohSize, "loh" },
		});

		_sut.CreateGcCollectionCountMetric("eventstore-gc-collection-count", new() {
			{ TelemetryConfiguration.ProcessTracker.Gen0CollectionCount, "gen0" },
			{ TelemetryConfiguration.ProcessTracker.Gen1CollectionCount, "gen1" },
			{ TelemetryConfiguration.ProcessTracker.Gen2CollectionCount, "gen2" },
		});

		_sut.CreateDiskBytesMetric("eventstore-disk-io", new() {
			{ TelemetryConfiguration.ProcessTracker.DiskReadBytes, "read" },
			{ TelemetryConfiguration.ProcessTracker.DiskWrittenBytes, "written" },
		});

		_sut.CreateDiskOpsMetric("eventstore-disk-io", new() {
			{ TelemetryConfiguration.ProcessTracker.DiskReadBytes, "read" },
			{ TelemetryConfiguration.ProcessTracker.DiskWrittenBytes, "written" },
		});

		_intListener.Observe();
		_doubleListener.Observe();
		_longListener.Observe();
	}

	public void Dispose() {
		_intListener.Dispose();
		_doubleListener.Dispose();
		_longListener.Dispose();
	}

	[Fact]
	public void can_collect_proc_up_time() {
		Assert.Collection(
			_doubleListener.RetrieveMeasurements("eventstore-proc-up-time"),
			m => {
				Assert.True(m.Value > 0);
				Assert.Collection(
					m.Tags,
					tag => {
						Assert.Equal("pid", tag.Key);
						Assert.NotNull(tag.Value);
					});
			});
	}

	[Fact]
	public void can_collect_proc_cpu() {
		Assert.Collection(
			_intListener.RetrieveMeasurements("eventstore-proc-cpu"),
			m => {
				Assert.True(m.Value >= 0);
				Assert.Empty(m.Tags);
			});
	}

	[Fact]
	public void can_collect_proc_contention_count() {
		Assert.Collection(
			_longListener.RetrieveMeasurements("eventstore-proc-contention-count"),
			m => {
				Assert.True(m.Value >= 0);
				Assert.Empty(m.Tags);
			});
	}

	[Fact]
	public void can_collect_proc_exception_count() {
		Assert.Collection(
			_intListener.RetrieveMeasurements("eventstore-proc-exception-count"),
			m => {
				Assert.True(m.Value >= 0);
				Assert.Empty(m.Tags);
			});
	}

	[Fact]
	public void can_collect_gc_total_allocated() {
		Assert.Collection(
			_longListener.RetrieveMeasurements("eventstore-gc-total-allocated"),
			m => {
				Assert.True(m.Value > 0);
				Assert.Empty(m.Tags);
			});
	}

	[Fact]
	public void can_collect_thread_count() {
		Assert.Collection(
			_intListener.RetrieveMeasurements("eventstore-proc-thread-count"),
			m => {
				Assert.True(m.Value > 0);
				Assert.Empty(m.Tags);
			});
	}

	[Fact]
	public void can_collect_gc_time_in_gc() {
		Assert.Collection(
			_intListener.RetrieveMeasurements("eventstore-gc-time-in-gc"),
			m => {
				Assert.True(m.Value >= 0);
				Assert.Empty(m.Tags);
			});
	}

	[Fact]
	public void can_collect_gc_heap_size() {
		Assert.Collection(
			_longListener.RetrieveMeasurements("eventstore-gc-heap-size-bytes"),
			m => {
				Assert.True(m.Value > 0);
				Assert.Empty(m.Tags);
			});
	}

	[Fact]
	public void can_collect_gc_heap_fragmentation() {
		Assert.Collection(
			_doubleListener.RetrieveMeasurements("eventstore-gc-heap-fragmentation"),
			m => {
				Assert.True(m.Value > 0);
				Assert.Empty(m.Tags);
			});
	}

	[Fact]
	public void can_collect_proc_mem() {
		Assert.Collection(
			_longListener.RetrieveMeasurements("eventstore-proc-mem-bytes"),
			m => {
				Assert.True(m.Value > 0);
				Assert.Collection(
					m.Tags,
					tag => {
						Assert.Equal("kind", tag.Key);
						Assert.Equal("working-set", tag.Value);
					});
			});
	}

	[Fact]
	public void can_collect_gc_generation_size() {
		Assert.Collection(
			_longListener.RetrieveMeasurements("eventstore-gc-generation-size-bytes"),
			m => {
				Assert.True(m.Value > 0);
				Assert.Collection(
					m.Tags,
					tag => {
						Assert.Equal("generation", tag.Key);
						Assert.Equal("gen0", tag.Value);
					});
			},
			m => {
				Assert.True(m.Value > 0);
				Assert.Collection(
					m.Tags,
					tag => {
						Assert.Equal("generation", tag.Key);
						Assert.Equal("gen1", tag.Value);
					});
			},
			m => {
				Assert.True(m.Value > 0);
				Assert.Collection(
					m.Tags,
					tag => {
						Assert.Equal("generation", tag.Key);
						Assert.Equal("gen2", tag.Value);
					});
			},
			m => {
				Assert.True(m.Value > 0);
				Assert.Collection(
					m.Tags,
					tag => {
						Assert.Equal("generation", tag.Key);
						Assert.Equal("loh", tag.Value);
					});
			});
	}

	[Fact]
	public void can_collect_gc_collections_count() {
		Assert.Collection(
			_intListener.RetrieveMeasurements("eventstore-gc-collection-count"),
			m => {
				Assert.True(m.Value >= 0);
				Assert.Collection(
					m.Tags,
					tag => {
						Assert.Equal("generation", tag.Key);
						Assert.Equal("gen0", tag.Value);
					});
			},
			m => {
				Assert.True(m.Value >= 0);
				Assert.Collection(
					m.Tags,
					tag => {
						Assert.Equal("generation", tag.Key);
						Assert.Equal("gen1", tag.Value);
					});
			},
			m => {
				Assert.True(m.Value >= 0);
				Assert.Collection(
					m.Tags,
					tag => {
						Assert.Equal("generation", tag.Key);
						Assert.Equal("gen2", tag.Value);
					});
			});
	}

	[Fact]
	public void can_collect_disk_io_bytes() {
		Assert.Collection(
			_longListener.RetrieveMeasurements("eventstore-disk-io-bytes"),
			m => {
				Assert.True(m.Value >= 0);
				Assert.Collection(
					m.Tags,
					tag => {
						Assert.Equal("activity", tag.Key);
						Assert.Equal("read", tag.Value);
					});
			},
			m => {
				Assert.True(m.Value >= 0);
				Assert.Collection(
					m.Tags,
					tag => {
						Assert.Equal("activity", tag.Key);
						Assert.Equal("written", tag.Value);
					});
			});
	}
}
