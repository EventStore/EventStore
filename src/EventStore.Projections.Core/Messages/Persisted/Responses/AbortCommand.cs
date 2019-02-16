using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class AbortCommand {
		public string Name;
		public SerializedRunAs RunAs;
	}
}
