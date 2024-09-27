// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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
