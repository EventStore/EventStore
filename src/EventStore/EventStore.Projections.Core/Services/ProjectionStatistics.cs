using EventStore.Projections.Core.Messages;
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
    }
}