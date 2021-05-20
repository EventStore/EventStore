using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages {
	public static class ReaderCoreServiceMessage {
		public class StartReader : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Guid InstanceCorrelationId { get; }

			public StartReader(Guid instanceCorrelationId) {
				InstanceCorrelationId = instanceCorrelationId;
			}
		}

		public class StopReader : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public Guid QueueId { get; }

			public StopReader(Guid queueId) {
				QueueId = queueId;
			}
		}
	}
}
