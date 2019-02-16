using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class StartSlaveProjectionsCommand {
		public string Name;
		public SerializedRunAs RunAs;
		public string MasterCorrelationId;
		public string MasterWorkerId;
		public SlaveProjectionDefinitions SlaveProjections;
	}
}
