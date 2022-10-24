using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages {
	public static partial class ProjectionSubsystemMessage {
		[StatsGroup("projections-subsystem")]
		public enum MessageType {
			None = 0,
			RestartSubsystem = 1,
			InvalidSubsystemRestart = 2,
			SubsystemRestarting = 3,
			StartComponents = 4,
			ComponentStarted = 5,
			StopComponents = 6,
			ComponentStopped = 7,
			IODispatcherDrained = 8,
		}
	
		[StatsMessage(MessageType.RestartSubsystem)]
		public partial class RestartSubsystem : Message  {
			public IEnvelope ReplyEnvelope { get; }
			
			public RestartSubsystem(IEnvelope replyEnvelope) {
				ReplyEnvelope = replyEnvelope;
			}
		}

		[StatsMessage(MessageType.InvalidSubsystemRestart)]
		public partial class InvalidSubsystemRestart : Message {
			public string SubsystemState { get; }

			public InvalidSubsystemRestart(string subsystemState) {
				SubsystemState = subsystemState;
			}
		}

		[StatsMessage(MessageType.SubsystemRestarting)]
		public partial class SubsystemRestarting : Message {
		}

		[StatsMessage(MessageType.StartComponents)]
		public partial class StartComponents : Message  {
			public Guid InstanceCorrelationId { get; }

			public StartComponents(Guid instanceCorrelationId) {
				InstanceCorrelationId = instanceCorrelationId;
			}
		}	
			
		[StatsMessage(MessageType.ComponentStarted)]
		public partial class ComponentStarted : Message  {
			public string ComponentName { get; }
			public Guid InstanceCorrelationId { get; }

			public ComponentStarted(string componentName, Guid instanceCorrelationId) {
				ComponentName = componentName;
				InstanceCorrelationId = instanceCorrelationId;
			}
		}	
	
		[StatsMessage(MessageType.StopComponents)]
		public partial class StopComponents : Message  {
			public Guid InstanceCorrelationId { get; }

			public StopComponents(Guid instanceCorrelationId) {
				InstanceCorrelationId = instanceCorrelationId;
			}
		}
		
		[StatsMessage(MessageType.ComponentStopped)]
		public partial class ComponentStopped : Message {
			public string ComponentName { get; }
			public Guid InstanceCorrelationId { get; }

			public ComponentStopped(string componentName, Guid instanceCorrelationId) {
				ComponentName = componentName;
				InstanceCorrelationId = instanceCorrelationId;
			}
		}

		[StatsMessage(MessageType.IODispatcherDrained)]
		public partial class IODispatcherDrained : Message {
			public string ComponentName { get; }

			public IODispatcherDrained(string componentName) {
				ComponentName = componentName;
			}
		}
	}
}
