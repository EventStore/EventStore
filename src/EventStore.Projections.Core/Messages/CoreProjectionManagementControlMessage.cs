using System;
using System.Threading;

namespace EventStore.Projections.Core.Messages {
	public class CoreProjectionManagementControlMessage : CoreProjectionManagementMessageBase {
		private readonly Guid _workerId;
		private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		public Guid WorkerId {
			get { return _workerId; }
		}

		public CoreProjectionManagementControlMessage(Guid projectionId, Guid workerId)
			: base(projectionId) {
			_workerId = workerId;
		}
	}
}
