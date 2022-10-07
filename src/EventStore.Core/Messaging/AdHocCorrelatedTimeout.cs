using System;

namespace EventStore.Core.Messaging {
	public struct AdHocCorrelatedTimeout : ICorrelatedTimeout {
		private readonly Action<Guid> _timeout;

		public AdHocCorrelatedTimeout(Action<Guid> timeout) {
			_timeout = timeout;
		}

		public void Timeout(Guid correlationId) {
			_timeout(correlationId);
		}
	}
}
