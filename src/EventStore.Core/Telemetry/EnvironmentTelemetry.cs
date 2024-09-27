// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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
}
