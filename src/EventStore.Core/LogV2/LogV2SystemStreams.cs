// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Services;

namespace EventStore.Core.LogV2;

public class LogV2SystemStreams : ISystemStreamLookup<string> {
	public LogV2SystemStreams() {
	}

	public string AllStream { get; } = SystemStreams.AllStream;
	public string SettingsStream { get; } = SystemStreams.SettingsStream;

	public bool IsMetaStream(string streamId) => SystemStreams.IsMetastream(streamId);

	public ValueTask<bool> IsSystemStream(string streamId, CancellationToken token) =>
		new(SystemStreams.IsSystemStream(streamId));
	public string MetaStreamOf(string streamId) => SystemStreams.MetastreamOf(streamId);
	public string OriginalStreamOf(string streamId) => SystemStreams.OriginalStreamOf(streamId);
}
