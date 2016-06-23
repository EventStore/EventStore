using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core.Messages.Persisted.Responses
{
    public class DeleteCommand
    {
        public string Name;
        public SerializedRunAs RunAs;
        public bool DeleteCheckpointStream;
        public bool DeleteStateStream;
        public bool DeleteEmittedStreams;
    }
}