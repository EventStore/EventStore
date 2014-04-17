namespace EventStore.Projections.Core.Services.Management
{
    public enum ManagedProjectionState
    {
        Creating,
        Loading,
        Loaded,
        Preparing,
        Prepared,
        Writing,
        Stopped,
        Completed,
        Aborted,
        Faulted,
        Starting,
        LoadingStopped,
        Running,
        Stopping,
        Aborting,
    }
}