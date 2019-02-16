using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class UpdateQueryCommand {
		public string Name;
		public SerializedRunAs RunAs;
		public bool? EmitEnabled;
		public string HandlerType;
		public string Query;
	}
}
