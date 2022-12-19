using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages {
	[DerivedMessage]
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
