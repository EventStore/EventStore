using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages {
	[StatsMessage]
	public abstract partial class CoreProjectionManagementMessageBase : Message {
		private readonly Guid _projectionIdId;

		protected CoreProjectionManagementMessageBase(Guid projectionId) {
			_projectionIdId = projectionId;
		}

		public Guid ProjectionId {
			get { return _projectionIdId; }
		}
	}
}
