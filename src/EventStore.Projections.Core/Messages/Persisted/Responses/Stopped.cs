namespace EventStore.Projections.Core.Messages.Persisted.Responses
{
    public class Stopped
    {
        public string Id { get; set; }
        public bool Completed { get; set; }
    }
}