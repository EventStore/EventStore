using System;

namespace EventStore.Core.Messaging {
	public interface ICorrelatedTimeout {
		void Timeout(Guid correlationId);
	}
}
