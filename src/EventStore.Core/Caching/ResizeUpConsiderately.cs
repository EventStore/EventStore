namespace EventStore.Core.Caching {
	// Limits the rate of resizing upwards.
	// Allows resizing Downward instantly
	// This allows us to share increased resources with others (in practice, other ES nodes)
	// Those with the least resources expand the most.
	// todo: we will probably also need a mechanism to exert pressure on other nodes to redistribute
	// their memory to other nodes
	public class ResizeUpConsiderately : CacheResizerWrapper {
		private long? _lastCapacity;

		public ResizeUpConsiderately(ICacheResizer wrapped) : base(wrapped) {
		}

		public override void CalcCapacity(long unreservedCapacity, int totalWeight) {
			var adjustedCapacity = AdjustCapacity(unreservedCapacity);
			base.CalcCapacity(adjustedCapacity, totalWeight);
			_lastCapacity = adjustedCapacity;
		}

		//qq we can come up with something more refined than this, but the idea is to progress towards
		// the specified capacity, but give other nodes a chance to do the same. those that have less capacity
		// already should expand less than those that have more.
		// this doesn't have to be _very_ good since running multiple nodes on one machine is pretty unusual,
		// but it should at least avoid one node yielding all its memory to another
		private long AdjustCapacity(long capacity) {
			if (_lastCapacity is null) {
				return capacity;
			}

			if (capacity <= _lastCapacity)
				return capacity; // going down instantly

			// going up considerately
			var capacityDiff = capacity - _lastCapacity;
			var adjustedCapacity = (long)(_lastCapacity + (0.5 * capacityDiff));
			return adjustedCapacity;
		}
	}


	public class DoNotResizeUp : CacheResizerWrapper {
		private long? _lastCapacity;

		public DoNotResizeUp(ICacheResizer wrapped) : base(wrapped) {
		}

		public override void CalcCapacity(long unreservedCapacity, int totalWeight) {
			if (_lastCapacity != null && _lastCapacity < unreservedCapacity)
				return;

			base.CalcCapacity(unreservedCapacity, totalWeight);
			_lastCapacity = unreservedCapacity;
		}
	}
}
