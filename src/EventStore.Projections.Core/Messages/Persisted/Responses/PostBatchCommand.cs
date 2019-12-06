using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core.Messages.Persisted.Responses {
	public class PostBatchCommand {
		public SerializedRunAs RunAs;
		public ProjectionPost[] Projections;

		public class ProjectionPost {
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
}
