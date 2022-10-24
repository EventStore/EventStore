using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages {
	public static partial class ReaderCoreServiceMessage {
		[StatsGroup("projections-reader-core-service")]
		public enum MessageType {
			None = 0,
			StartReader = 1,
			StopReader = 2,
		}

		[StatsMessage(MessageType.StartReader)]
		public partial class StartReader : Message {
			public Guid InstanceCorrelationId { get; }

			public StartReader(Guid instanceCorrelationId) {
				InstanceCorrelationId = instanceCorrelationId;
			}
		}

		[StatsMessage(MessageType.StopReader)]
		public partial class StopReader : Message {
			public Guid QueueId { get; }
			
			public StopReader(Guid queueId) {
				QueueId = queueId;
			}
		}
	}
}
