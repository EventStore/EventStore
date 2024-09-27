using System.Runtime;

namespace EventStore.Common.Utils;

public static class ContainerizedEnvironment {
    public const int ReaderThreadCount       = 4;
    public const int WorkerThreadCount       = 5;
    public const int StreamInfoCacheCapacity = 100000;

    public static bool IsRunningInContainer() => RuntimeInformation.IsRunningInContainer;
}
