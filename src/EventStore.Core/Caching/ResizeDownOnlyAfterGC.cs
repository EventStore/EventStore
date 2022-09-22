using System;

namespace EventStore.Core.Caching {
	// Resizes Upwards instantly
	// Resizes Downward only if a full GC has occurred.
	// This prevents a feedback loop of resizing down causing further resizing down.
	// It would otherwise happen because resizing down reduces the cache size but does not free the
	// memory until the GC runs
	public class ResizeDownOnlyAfterGC : CacheResizerWrapper {
		// last time a resize was completed
		private int _lastGCCount;
		private long? _lastCapacity;

		public ResizeDownOnlyAfterGC(ICacheResizer wrapped) : base(wrapped) {
		}

		public override void CalcCapacity(long unreservedCapacity, int totalWeight) {
			//qq might be that we want get the gc count up here
			if (!RecalcConditionsMet(unreservedCapacity))
				//qq is 15s rather a long time to be waiting to check again though?
				return;

			// <--gc
			base.CalcCapacity(unreservedCapacity, totalWeight);
			_lastGCCount = GC.CollectionCount(GC.MaxGeneration);
			_lastCapacity = unreservedCapacity;
		}

		private bool RecalcConditionsMet(long capacity) {
			if (_lastCapacity is null) {
				return true;
			}

			if (capacity > _lastCapacity) {
				// available memory increased. resize up to make use of it.
				return true;
			}

			// available memory has decreased. wait for the GC though.
			if (_lastGCCount == GC.CollectionCount(GC.MaxGeneration)) {
				return false;
			}

			return true;
		}
	}
}
