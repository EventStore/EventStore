using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages {
	public static partial class ReaderCoreServiceMessage {
		[DerivedMessage(ProjectionMessage.ReaderCoreService)]
		public partial class InitReaderService : Message {
			public Guid InstanceCorrelationId { get; }

			public InitReaderService(Guid instanceCorrelationId) {
				InstanceCorrelationId = instanceCorrelationId;
			}
		}

		[DerivedMessage(ProjectionMessage.ReaderCoreService)]
		public partial class DisposeReader : Message {
			public Guid QueueId { get; }
			
			public DisposeReader(Guid queueId) {
				QueueId = queueId;
			}
		}
	}
}
