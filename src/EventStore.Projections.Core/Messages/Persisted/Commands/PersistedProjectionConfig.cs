using System.Security.Principal;
using EventStore.Core.Authentication;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Services;

namespace EventStore.Projections.Core.Messages.Persisted.Commands {
	public sealed class PersistedProjectionConfig {
		public string RunAs;
		public string[] RunAsRoles;
		public int CheckpointHandledThreshold;
		public int CheckpointUnhandledBytesThreshold;
		public int PendingEventsThreshold;
		public int MaxWriteBatchLength;
		public bool EmitEventEnabled;
		public bool CheckpointsEnabled;
		public bool CreateTempStreams;
		public bool StopOnEof;
		public bool IsSlaveProjection;
		public bool TrackEmittedStreams;
		public int CheckpointAfterMs;
		public int MaximumAllowedWritesInFlight;

		public PersistedProjectionConfig() {
		}

		public PersistedProjectionConfig(ProjectionConfig config) {
			RunAs = config.RunAs.Identity.Name;
			RunAsRoles = config.RunAs == SystemAccount.Principal
				? new string[0]
				: ((OpenGenericPrincipal)config.RunAs).Roles;

			CheckpointHandledThreshold = config.CheckpointHandledThreshold;
			CheckpointUnhandledBytesThreshold = config.CheckpointUnhandledBytesThreshold;
			PendingEventsThreshold = config.PendingEventsThreshold;
			MaxWriteBatchLength = config.MaxWriteBatchLength;
			EmitEventEnabled = config.EmitEventEnabled;
			CheckpointsEnabled = config.CheckpointsEnabled;
			CreateTempStreams = config.CreateTempStreams;
			StopOnEof = config.StopOnEof;
			IsSlaveProjection = config.IsSlaveProjection;
			TrackEmittedStreams = config.TrackEmittedStreams;
			CheckpointAfterMs = config.CheckpointAfterMs;
			MaximumAllowedWritesInFlight = config.MaximumAllowedWritesInFlight;
		}

		public ProjectionConfig ToConfig() {
			return
				new ProjectionConfig(
					(RunAs == SystemAccount.Principal.Identity.Name)
						? (IPrincipal)SystemAccount.Principal
						: new OpenGenericPrincipal(RunAs, RunAsRoles),
					CheckpointHandledThreshold,
					CheckpointUnhandledBytesThreshold,
					PendingEventsThreshold,
					MaxWriteBatchLength,
					EmitEventEnabled,
					CheckpointsEnabled,
					CreateTempStreams,
					StopOnEof,
					IsSlaveProjection,
					TrackEmittedStreams,
					CheckpointAfterMs,
					MaximumAllowedWritesInFlight);
		}
	}
}
