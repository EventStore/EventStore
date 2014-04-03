using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Services
{
    public class ProjectionStatistics
    {
        //TODO: resolve name collisions...

        public string Status { get; set; }

        public bool Enabled { get; set; }

        public ManagedProjectionState MasterStatus { get; set; }

        public string StateReason { get; set; }

        public string Name { get; set; }

        public int ProjectionId { get; set; }

        public int Epoch { get; set; }

        public int Version { get; set; }

        public ProjectionMode Mode { get; set; }

        public CheckpointTag Position { get; set; }

        public float Progress { get; set; }

        public string LastCheckpoint { get; set; }

        public int EventsProcessedAfterRestart { get; set; }

        public int BufferedEvents { get; set; }

        public string CheckpointStatus { get; set; }

        public int WritePendingEventsBeforeCheckpoint { get; set; }

        public int WritePendingEventsAfterCheckpoint { get; set; }

        public int PartitionsCached { get; set; }

        public int ReadsInProgress { get; set; }

        public int WritesInProgress { get; set; }

        public string EffectiveName { get; set; }

        public ProjectionSourceDefinition Definition { get; set; }

        public long CoreProcessingTime { get; set; }

        public ProjectionStatistics Clone()
        {
            return (ProjectionStatistics) MemberwiseClone();
        }

        protected bool Equals(ProjectionStatistics other)
        {
            return string.Equals(Status, other.Status) && Enabled.Equals(other.Enabled)
                   && MasterStatus == other.MasterStatus && string.Equals(StateReason, other.StateReason)
                   && string.Equals(Name, other.Name) && ProjectionId == other.ProjectionId && Epoch == other.Epoch
                   && Version == other.Version && Mode == other.Mode && Equals(Position, other.Position)
                   && Progress.Equals(other.Progress) && string.Equals(LastCheckpoint, other.LastCheckpoint)
                   && EventsProcessedAfterRestart == other.EventsProcessedAfterRestart
                   && BufferedEvents == other.BufferedEvents && string.Equals(CheckpointStatus, other.CheckpointStatus)
                   && WritePendingEventsBeforeCheckpoint == other.WritePendingEventsBeforeCheckpoint
                   && WritePendingEventsAfterCheckpoint == other.WritePendingEventsAfterCheckpoint
                   && PartitionsCached == other.PartitionsCached && ReadsInProgress == other.ReadsInProgress
                   && WritesInProgress == other.WritesInProgress && string.Equals(EffectiveName, other.EffectiveName)
                   && Equals(Definition, other.Definition) && CoreProcessingTime == other.CoreProcessingTime;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ProjectionStatistics) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (Status != null ? Status.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ Enabled.GetHashCode();
                hashCode = (hashCode*397) ^ (int) MasterStatus;
                hashCode = (hashCode*397) ^ (StateReason != null ? StateReason.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ ProjectionId;
                hashCode = (hashCode*397) ^ Epoch;
                hashCode = (hashCode*397) ^ Version;
                hashCode = (hashCode*397) ^ (int) Mode;
                hashCode = (hashCode*397) ^ (Position != null ? Position.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ Progress.GetHashCode();
                hashCode = (hashCode*397) ^ (LastCheckpoint != null ? LastCheckpoint.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ EventsProcessedAfterRestart;
                hashCode = (hashCode*397) ^ BufferedEvents;
                hashCode = (hashCode*397) ^ (CheckpointStatus != null ? CheckpointStatus.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ WritePendingEventsBeforeCheckpoint;
                hashCode = (hashCode*397) ^ WritePendingEventsAfterCheckpoint;
                hashCode = (hashCode*397) ^ PartitionsCached;
                hashCode = (hashCode*397) ^ ReadsInProgress;
                hashCode = (hashCode*397) ^ WritesInProgress;
                hashCode = (hashCode*397) ^ (EffectiveName != null ? EffectiveName.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Definition != null ? Definition.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ CoreProcessingTime.GetHashCode();
                return hashCode;
            }
        }
    }
}