using System;

namespace EventStore.Projections.Core.Services {
	public interface ISingletonTimeoutScheduler {
		void Schedule(int timeout, Action action);
	}
}
