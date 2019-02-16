using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages {
	public abstract class CoreProjectionManagementMessageBase : Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		private readonly Guid _projectionIdId;

		protected CoreProjectionManagementMessageBase(Guid projectionId) {
			_projectionIdId = projectionId;
		}

		public Guid ProjectionId {
			get { return _projectionIdId; }
		}
	}
}
