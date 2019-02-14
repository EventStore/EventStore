using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class EnableCommand {
		public string Name;
		public SerializedRunAs RunAs;
	}
}
