using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using Serilog;

namespace EventStore.Core.Caching {
	public class DynamicCacheManager:
		IHandle<MonitoringMessage.DynamicCacheManagerTick>,
		IHandle<MonitoringMessage.InternalStatsRequest> {

		private readonly IPublisher _bus;
		private readonly Func<long> _getFreeMem;
		private readonly long _totalMem;
		private readonly int _keepFreeMemPercent;
		private readonly long _keepFreeMemBytes;
		private readonly long _keepFreeMem;
		private readonly TimeSpan _minResizeInterval;
		private readonly long _minResizeThreshold;
		private readonly ICacheResizer _rootCacheResizer;
		private readonly Message _scheduleTick;
		private readonly object _lock = new();

		private DateTime _lastResize = DateTime.UtcNow;
		private long _lastAvailableMem = -1;

		public DynamicCacheManager(
			IPublisher bus,
			Func<long> getFreeMem,
			long totalMem,
			int keepFreeMemPercent,
			long keepFreeMemBytes,
			TimeSpan monitoringInterval,
			TimeSpan minResizeInterval,
			long minResizeThreshold,
			ICacheResizer rootCacheResizer) {

			if (keepFreeMemPercent is < 0 or > 100)
				throw new ArgumentException($"{nameof(keepFreeMemPercent)} must be between 0 to 100 inclusive.");

			if (keepFreeMemBytes < 0)
				throw new ArgumentException($"{nameof(keepFreeMemBytes)} must be non-negative.");

			_bus = bus;
			_getFreeMem = getFreeMem;
			_totalMem = totalMem;
			_keepFreeMemPercent = keepFreeMemPercent;
			_keepFreeMemBytes = keepFreeMemBytes;
			_keepFreeMem = Math.Max(_keepFreeMemBytes, _totalMem.ScaleByPercent(_keepFreeMemPercent));
			_minResizeInterval = minResizeInterval;
			_minResizeThreshold = minResizeThreshold;
			_rootCacheResizer = rootCacheResizer;
			_scheduleTick = TimerMessage.Schedule.Create(
				monitoringInterval,
				new PublishEnvelope(_bus),
				new MonitoringMessage.DynamicCacheManagerTick());
		}

		public void Start() {
			var availableMem = CalcAvailableMemory(_getFreeMem(), 0);
			ResizeCaches(availableMem);
			Tick();
		}

		public void Handle(MonitoringMessage.DynamicCacheManagerTick message) {
			ThreadPool.QueueUserWorkItem(_ => {
				try {
					lock (_lock) { // only to add read/write barriers
						ResizeCachesIfNeeded();
					}
				} finally {
					Tick();
				}
			});
		}

		public void Handle(MonitoringMessage.InternalStatsRequest message) {
			Thread.MemoryBarrier(); // just to ensure we're seeing latest values

			var stats = new Dictionary<string, object>();
			var cachesStats = _rootCacheResizer.GetStats(string.Empty);

			foreach(var cacheStat in cachesStats) {
				var statNamePrefix = $"es-cache-{cacheStat.Key}-";
				stats[statNamePrefix + "name"] = cacheStat.Name;
				stats[statNamePrefix + "weight"] = cacheStat.Weight;
				stats[statNamePrefix + "mem-used"] = cacheStat.MemUsed;
				stats[statNamePrefix + "mem-allotted"] = cacheStat.MemAllotted;
			}

			message.Envelope.ReplyWith(new MonitoringMessage.InternalStatsRequestResponse(stats));
		}

		private static void TriggerGc() {
			var sw = new Stopwatch();
			Log.Debug("Triggering gen {generation} garbage collection", GC.MaxGeneration);

			sw.Restart();
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
			sw.Stop();

			Log.Debug("GC took {elapsed}", sw.Elapsed);

			sw.Restart();
			GC.WaitForPendingFinalizers();
			sw.Stop();

			Log.Debug("GC finalization took {elapsed}", sw.Elapsed);
		}

		private bool ResizeConditionsMet(out long freeMem, out long availableMem) {
			availableMem = 0L;
			freeMem = _getFreeMem();

			if (freeMem >= _keepFreeMem && DateTime.UtcNow - _lastResize < _minResizeInterval)
				return false;

			var cachedMem = _rootCacheResizer.GetMemUsage();
			availableMem = CalcAvailableMemory(freeMem, cachedMem);

			if (_lastAvailableMem != -1 && Math.Abs(availableMem - _lastAvailableMem) < _minResizeThreshold)
				return false;

			return true;
		}

		private void ResizeCachesIfNeeded() {
			// We need to trigger the GC before resizing the caches, since things that have
			// been deleted from the caches may not yet have been garbage collected and
			// this will result in a lower calculation of "availableMem", which in turn
			// will result in a smaller memory allotment to the caches.
			// Triggering the GC is not cheap, so we want to do it as rarely as we can.
			// We first check if the conditions for resizing the caches are met, then trigger the GC.
			// But now, we may have enough free memory and resizing the caches may no longer be necessary,
			// so we verify the conditions again before finally resizing the caches.

			if (!ResizeConditionsMet(out _, out _))
				return;

			TriggerGc();

			if (!ResizeConditionsMet(out var freeMem, out var availableMem))
				return;

			if (freeMem < _keepFreeMem)
				Log.Debug("Available system memory is lower than "
				          + "{thresholdPercent}% or {thresholdBytes:N0} bytes: {freeMem:N0} bytes. Resizing caches.",
					_keepFreeMemPercent, _keepFreeMemBytes, freeMem);

			try {
				ResizeCaches(availableMem);
			} catch (Exception ex) {
				Log.Error(ex, "Error while resizing caches");
			} finally {
				_lastResize = DateTime.UtcNow;
				_lastAvailableMem = availableMem;
			}
		}

		private void ResizeCaches(long availableMem) {
			_rootCacheResizer.CalcAllotment(availableMem, _rootCacheResizer.Weight);
		}

		// Memory available for caching
		private long CalcAvailableMemory(long freeMem, long cachedMem) {
			var availableMem = Math.Max(0L, freeMem + cachedMem - _keepFreeMem);

			Log.Verbose("Calculating memory available for caching based on:\n" +
			          "Free memory: {freeMem:N0} bytes\n" +
			          "Total memory: {totalMem:N0} bytes\n" +
			          "Cached memory: ~{cachedMem:N0} bytes\n" +
			          "Keep free memory: {keepFreeMem:N0} bytes (higher of {keepFreeMemPercent}% total mem & {keepFreeMemBytes:N0} bytes)\n\n" +
			          "Memory available for caching: ~{availableMem:N0} bytes\n",
				freeMem, _totalMem, cachedMem,
				_keepFreeMem, _keepFreeMemPercent, _keepFreeMemBytes,
				availableMem);

			return availableMem;
		}

		private void Tick() => _bus.Publish(_scheduleTick);
	}
}
