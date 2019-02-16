using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class ResetCommand {
		public string Name;
		public SerializedRunAs RunAs;
	}
}
