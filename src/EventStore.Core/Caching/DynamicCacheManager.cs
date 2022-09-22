using System;
using System.Collections.Generic;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using Serilog;

//qq
// - make sure allotments are smooth, not oscilating
// - consider/test multiple nodes on one machine
// - consider the names of the stats hierarchy, does it make sense esp with the keepFree
namespace EventStore.Core.Caching {
	public class DynamicCacheManager:
		IHandle<MonitoringMessage.DynamicCacheManagerTick>,
		IHandle<MonitoringMessage.InternalStatsRequest> {

		private readonly IPublisher _bus;
		private readonly Func<long> _getFreeMem;
		private readonly UnadjustedCapacityTracker _cacheResizer;
		private readonly ICacheResizer _rootCacheResizer;
		private readonly Message _scheduleTick;

		public DynamicCacheManager(
			IPublisher bus,
			Func<long> getFreeMem,
			long totalMem,
			int keepFreeMemPercent,
			long keepFreeMemBytes,
			TimeSpan monitoringInterval,
			ICacheResizer cacheResizer) {

			_bus = bus;
			_getFreeMem = getFreeMem;
			_scheduleTick = TimerMessage.Schedule.Create(
				monitoringInterval,
				new PublishEnvelope(_bus),
				new MonitoringMessage.DynamicCacheManagerTick());

			CreatePipeline(
				totalMem, keepFreeMemPercent, keepFreeMemBytes, cacheResizer,
				out _cacheResizer, out _rootCacheResizer);
		}

		//qq add tests
		public static void CreatePipeline(
			long totalMem,
			int keepFreeMemPercent,
			long keepFreeMemBytes,
			ICacheResizer cacheResizer,
			out UnadjustedCapacityTracker outCacheResizer,
			out ICacheResizer rootCacheResizer) {

			outCacheResizer = AddResizingLogic(cacheResizer);
			rootCacheResizer = CreateRootResizer(totalMem, keepFreeMemPercent, keepFreeMemBytes, outCacheResizer);
		}

		private static UnadjustedCapacityTracker AddResizingLogic(ICacheResizer cacheResizer) {
			cacheResizer = new ResizeDownOnlyAfterGC(cacheResizer);
			cacheResizer = new DoNotResizeUp(cacheResizer);
			return new UnadjustedCapacityTracker(cacheResizer);
		}

		// cacheResizer distributes among the caches what we want to use for the caches
		// but we dont want to use _all_ the free memory for the caches. here we add logic to the
		// resizing pipeline that
		//   1. keeps a portion of memory free so there is always room to allocate
		//   2. keeps a portion of memory aside for the OS disk cache so the OS can help us with chunk/ptable access
		private static ICacheResizer CreateRootResizer(
			long totalMem,
			int keepFreeMemPercent,
			long keepFreeMemBytes,
			ICacheResizer cacheResizer) {

			var rootName = "availableMemory";
			if (cacheResizer.Unit == ResizerUnit.Entries) {
				return new CompositeCacheResizer(name: rootName, weight: 100, cacheResizer);
			}

			if (keepFreeMemPercent is < 0 or > 100)
				throw new ArgumentException($"{nameof(keepFreeMemPercent)} must be between 0 to 100 inclusive.");

			if (keepFreeMemBytes < 0)
				throw new ArgumentException($"{nameof(keepFreeMemBytes)} must be non-negative.");

			var keepFreeMem = Math.Max(keepFreeMemBytes, totalMem.ScaleByPercent(keepFreeMemPercent));

			return new CompositeCacheResizer(
				name: rootName,
				weight: 100,
				cacheResizer,
				// later we might want to leave some amount of room for the disk cache specifically, but for now
				// the 20%/4gb in the keepFree is sufficient
				new DynamicCacheResizer(ResizerUnit.Bytes, minCapacity: 0, weight: 100, new EmptyDynamicCache("keepForOS")),
				new StaticCacheResizer(ResizerUnit.Bytes, keepFreeMem, new EmptyDynamicCache("keepFree")));
		}

		public void Start() {
			if (_rootCacheResizer.Unit == ResizerUnit.Entries) {
				_rootCacheResizer.CalcCapacityTopLevel(_rootCacheResizer.ReservedCapacity);
			} else {
				ResizeCaches();
				Tick();
			}

			foreach (var stat in _rootCacheResizer.GetStats(string.Empty)) {
				Log.Information("Cache {key} capacity initialized to {capacity:N0} {unit}",
					stat.Key, stat.Capacity, _rootCacheResizer.Unit);
			}
		}

		public void Handle(MonitoringMessage.DynamicCacheManagerTick message) {
			ThreadPool.QueueUserWorkItem(_ => {
				try {
					ResizeCaches();
				} finally {
					Thread.MemoryBarrier();
					Tick();
				}
			});
		}

		public void Handle(MonitoringMessage.InternalStatsRequest message) {
			Thread.MemoryBarrier(); // just to ensure we're seeing latest values

			var stats = new Dictionary<string, object>();
			var cachesStats = _cacheResizer.GetStats(string.Empty);

			foreach (var cacheStat in cachesStats) {
				var statNamePrefix = $"es-{cacheStat.Key}-";
				stats[statNamePrefix + "name"] = cacheStat.Name;
				stats[statNamePrefix + "size" + _rootCacheResizer.Unit] = cacheStat.Size;
				stats[statNamePrefix + "capacity" + _rootCacheResizer.Unit] = cacheStat.Capacity;
				stats[statNamePrefix + "utilizationPercent"] = cacheStat.UtilizationPercent;
			}

			//qq maybe we dont want this
			// record the capacity that was handed to the pipeline so that we can see how the pipeline
			// has adjusted it (i.e. the pipeline is gradually letting the capacity grow up to here,
			// or the pipeline is waiting for a GC to resize down to here)
			if (_rootCacheResizer.Unit == ResizerUnit.Bytes)
				stats[$"es-{_cacheResizer.Name}-unadjustedCapacityBytes"] = _cacheResizer.UnadjustedCapacity;

			message.Envelope.ReplyWith(new MonitoringMessage.InternalStatsRequestResponse(stats));
		}

		private void ResizeCaches() {
			try {
				var availableMem = CalcAvailableMemory();
				_rootCacheResizer.CalcCapacityTopLevel(availableMem);
			} catch (Exception ex) {
				Log.Error(ex, "Error while resizing caches");
				throw;
			}
		}

		// Memory available for static and dynamic caches
		private long CalcAvailableMemory() {
			var gcInfo = GC.GetGCMemoryInfo();

			var fragmentedBytes = gcInfo.FragmentedBytes;
			var freeBytes = _getFreeMem();
			var cachedBytes = _rootCacheResizer.Size;

			// fragmented bytes are essentially free bytes within the heap
			// (at least, until they get too small to use)
			var availableMem = fragmentedBytes + freeBytes + cachedBytes;

			Log.Verbose(
				"Calculating memory available for caching based on:\n" +
				"Free memory: {freeMem:N0} bytes\n" +
				"Fragmented memory: {fragmentedMem:N0} bytes\n" +
				"Cached memory: ~{cachedMem:N0} bytes\n" +
				"Memory available: ~{availableMem:N0} bytes\n",
				freeBytes, fragmentedBytes, cachedBytes, availableMem);

			return availableMem;
		}

		private void Tick() => _bus.Publish(_scheduleTick);
	}
}
