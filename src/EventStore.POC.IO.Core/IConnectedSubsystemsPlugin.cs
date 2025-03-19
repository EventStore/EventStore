// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using EventStore.Plugins.Subsystems;

namespace EventStore.POC.IO.Core;

// Necessary for now because of how PluginLoader works.
// Later it may be possible to remove this interface entirely.
public interface IConnectedSubsystemsPlugin {
	string Name { get; }
	string Version { get; }
	string CommandLineName { get; }
	IReadOnlyList<ISubsystem> GetSubsystems();
}
