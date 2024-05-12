using System.Runtime;

namespace EventStore.Core.Telemetry;

public readonly record struct EnvironmentTelemetry {
    public string        Arch      { get; init; }
    public ContainerInfo Container { get; init; }
    public MachineInfo   Machine   { get; init; }

    public static EnvironmentTelemetry Collect(ClusterVNodeOptions options) =>
        new() {
            Arch      = RuntimeInformation.Host.Architecture.ToString(),
            Container =  ContainerInfo.Collect(),
            Machine   =  MachineInfo.Collect(options)
        };

    // public static EnvironmentTelemetry Collect(ClusterVNodeOptions options) =>
    //     new()  {
    //         Arch      = RuntimeInformation.Host.Architecture.ToString(),
    //         Container =  new ContainerInfo (
    //             RuntimeInformation.IsRunningInContainer,
    //             RuntimeInformation.IsRunningInKubernetes
    //         ),
    //         Machine   =  new MachineInfo(
    //             Environment.ProcessorCount,
    //             RuntimeStats.GetTotalMemory(),
    //             DriveStats.GetDriveInfo(options.Database.Db).TotalBytes
    //         )
    //     };
}