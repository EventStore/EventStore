using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class PostCommand {
		public string Name;
		public SerializedRunAs RunAs;

		public bool CheckpointsEnabled;
		public bool TrackEmittedStreams;
		public bool EmitEnabled;
		public bool EnableRunAs;
		public bool Enabled;
		public string HandlerType;
		public ProjectionMode Mode;
		public string Query;
	}
}
