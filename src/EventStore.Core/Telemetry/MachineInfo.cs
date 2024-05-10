using System;
using System.Diagnostics;

namespace EventStore.Core.Telemetry;

public record MachineInfo(int ProcessorCount, long TotalMemory, long TotalDiskSpace) {
    public static MachineInfo Collect(ClusterVNodeOptions options) =>
        new(
            Environment.ProcessorCount,
            RuntimeStats.GetTotalMemorySync(),
            DriveStats.GetDriveInfo(options.Database.Db).TotalBytes
        );
}