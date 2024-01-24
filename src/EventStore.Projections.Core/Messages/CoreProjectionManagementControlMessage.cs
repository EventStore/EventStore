using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages {
	
	public interface ICoreProjectionManagementControlMessage: Message {
		Guid WorkerId { get; }
	}
	
	[DerivedMessage]
	public abstract partial class CoreProjectionManagementControlMessage<T> : CoreProjectionManagementMessageBase<T>, ICoreProjectionManagementControlMessage where T : Message {
		public CoreProjectionManagementControlMessage(Guid projectionId, Guid workerId) : base(projectionId) => WorkerId = workerId;
		
		public Guid WorkerId { get; }
	}
}
