// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable

using EventStore.Core.Messages;
using EventStore.Core.Services.Archive;
using Microsoft.Extensions.Configuration;

namespace EventStore.Core.TransactionLog.Scavenging;

// Calculates the way to configure the scavenge based on various configuration sources
public class ScavengeOptionsCalculator {
	readonly ClusterVNodeOptions _vNodeOptions;
	readonly ClientMessage.ScavengeDatabase _message;
	readonly bool _archiveEnabled;

	public ScavengeOptionsCalculator(
		ClusterVNodeOptions vNodeOptions,
		ClientMessage.ScavengeDatabase message) {

		_vNodeOptions = vNodeOptions;
		_message = message;

		var archiveOptions = vNodeOptions.ConfigurationRoot?
			.GetSection("EventStore:Archive")
			.Get<ArchiveOptions>() ?? new();

		_archiveEnabled = archiveOptions.Enabled;
	}

	// Archiving disables chunk merging because the two in combination make things
	// more complex (locating chunks in the archive is more complicated if they
	// are merged, and there may be subtleties if we try to merge a local chunk
	// into a chunk that is located in the archive.
	// Merging is also less important with archiving - we shouldn't have too many
	// chunks locally because of the archive, and it is ok to have lots of physical
	// files in the archive because we do not open file handles to all of them
	// continuously
	public bool MergeChunks =>
		!_archiveEnabled &&
		!_vNodeOptions.Database.DisableScavengeMerging;

	public int ChunkExecutionThreshold => _message.Threshold ?? 0;
}
