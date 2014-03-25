namespace EventStore.Projections.Core.Services.Management
{
    public enum ManagedProjectionState
    {
        Creating,
        Loading,
        Loaded,
        Writing,
        Preparing,
        Prepared,
        Stopped,
        Completed,
        Faulted,
        Starting,
        LoadingState,
        Running,
        Stopping,
    }
}