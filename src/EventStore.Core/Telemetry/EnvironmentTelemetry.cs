using System.Runtime.InteropServices;

namespace EventStore.Core.Telemetry;

public record EnvironmentTelemetry {
    public string        Arch      { get; init; }
    public ContainerInfo Container { get; init; }
    public MachineInfo   Machine   { get; init; }

    public static EnvironmentTelemetry Collect(ClusterVNodeOptions options) =>
        new()  {
            Arch      = RuntimeInformation.ProcessArchitecture.ToString(),
            Container = ContainerInfo.Collect(),
            Machine   = MachineInfo.Collect(options)
        };
}