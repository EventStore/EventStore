using System;
using System.Diagnostics;

namespace EventStore.Core.Telemetry;

public readonly record struct MachineInfo(int ProcessorCount, long TotalMemory, long TotalDiskSpace) {
    public static MachineInfo Collect(ClusterVNodeOptions options) => new(
        Environment.ProcessorCount,
        RuntimeStats.GetTotalMemory(),
        DriveStats.GetDriveInfo(options.Database.Db).TotalBytes
    );
}