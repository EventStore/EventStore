namespace EventStore.Core.Caching {
	public class UnadjustedCapacityTracker : CacheResizerWrapper {
		public UnadjustedCapacityTracker(ICacheResizer wrapped) : base(wrapped) {
		}

		public long UnadjustedCapacity { get; private set; }

		//qqqq this is the parent capacity we are recording which isn't really comparable
		// to the eventual cache capacity
		public override void CalcCapacity(long unreservedCapacity, int totalWeight) {
			UnadjustedCapacity = unreservedCapacity;
			base.CalcCapacity(unreservedCapacity, totalWeight);
		}
	}
}
