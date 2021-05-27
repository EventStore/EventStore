using FASTER.core;

namespace EventStore.Core.LogV3.FASTER {
	public class Context<TValue> {
		public Status Status { get; set; }
		public TValue Value { get; set; }
	}
}

