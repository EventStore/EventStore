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
        Aborted,
        Faulted,
        Starting,
        LoadingState,
        Running,
        Stopping,
    }
}