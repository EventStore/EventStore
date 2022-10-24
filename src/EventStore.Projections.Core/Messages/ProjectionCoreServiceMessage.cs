using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages {
	public static partial class ProjectionCoreServiceMessage {
		[StatsGroup("projections-service")]
		public enum MessageType {
			None = 0,
			StartCore = 1,
			StopCore = 2,
			StopCoreTimeout = 3,
			CoreTick = 4,
			SubComponentStarted = 5,
			SubComponentStopped = 6,
		}

		[StatsMessage(MessageType.StartCore)]
		public partial class StartCore : Message {
			public readonly Guid InstanceCorrelationId;

			public StartCore(Guid instanceCorrelationId) {
				InstanceCorrelationId = instanceCorrelationId;
			}
		}

		[StatsMessage(MessageType.StopCore)]
		public partial class StopCore : Message {
			public Guid QueueId { get; }

			public StopCore(Guid queueId) {
				QueueId = queueId;
			}
		}

		[StatsMessage(MessageType.StopCoreTimeout)]
		public partial class StopCoreTimeout : Message {
			public Guid QueueId { get; }

			public StopCoreTimeout(Guid queueId) {
				QueueId = queueId;
			}
		}
		
		[StatsMessage(MessageType.CoreTick)]
		public partial class CoreTick : Message {
			private readonly Action _action;

			public CoreTick(Action action) {
				_action = action;
			}

			public Action Action {
				get { return _action; }
			}
		}

		[StatsMessage(MessageType.SubComponentStarted)]
		public partial class SubComponentStarted : Message {
			public string SubComponent { get; }
			public Guid InstanceCorrelationId { get; }
		
			public SubComponentStarted(string subComponent, Guid instanceCorrelationId) {
				InstanceCorrelationId = instanceCorrelationId;
				SubComponent = subComponent;
			}
		}

		[StatsMessage(MessageType.SubComponentStopped)]
		public partial class SubComponentStopped : Message {
			public readonly string SubComponent;

			public Guid QueueId { get; }

			public SubComponentStopped(string subComponent, Guid queueId) {
				SubComponent = subComponent;
				QueueId = queueId;
			}
		}
	}
}
