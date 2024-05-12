using System.Runtime;

namespace EventStore.Core.Telemetry;

public readonly record struct ContainerInfo(bool IsContainer, bool IsKubernetes) {
    public static ContainerInfo Collect() => new(
        RuntimeInformation.IsRunningInContainer,
        RuntimeInformation.IsRunningInKubernetes
    );
}