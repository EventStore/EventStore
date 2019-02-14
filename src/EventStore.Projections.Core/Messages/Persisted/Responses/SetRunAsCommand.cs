using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class SetRunAsCommand {
		public string Name;
		public SerializedRunAs RunAs;
		public ProjectionManagementMessage.Command.SetRunAs.SetRemove SetRemove;
	}
}
