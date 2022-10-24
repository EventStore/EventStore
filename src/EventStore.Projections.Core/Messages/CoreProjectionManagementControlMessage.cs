using System;

namespace EventStore.Projections.Core.Messages {
	[StatsMessage]
	public abstract partial class CoreProjectionManagementControlMessage : CoreProjectionManagementMessageBase {
		private readonly Guid _workerId;

		public Guid WorkerId {
			get { return _workerId; }
		}

		public CoreProjectionManagementControlMessage(Guid projectionId, Guid workerId)
			: base(projectionId) {
			_workerId = workerId;
		}
	}
}
