using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EventStore.Core.Telemetry;

public readonly record struct MachineInfo(
	string OS,
	int ProcessorCount,
	long TotalMemory,
	long TotalDiskSpace) {

	public static MachineInfo Collect(ClusterVNodeOptions options) => new(
		RuntimeInformation.OSDescription,
		Environment.ProcessorCount,
		RuntimeStats.GetTotalMemory(),
		DriveStats.GetDriveInfo(options.Database.Db).TotalBytes
	);
}
