// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.AutoScavenge.Sources;

/// <summary>
/// Event provider abstraction use to source events to the auto scavenge state machine.
/// </summary>
public interface ISource {
	/// <summary>
	/// Reads the last auto scavenge configuration event.
	/// </summary>
	/// <param name="token"></param>
	/// <returns></returns>
	Task<Events.ConfigurationUpdated?> ReadConfigurationEvent(CancellationToken token);

	/// <summary>
	/// Reads the events necessary to initialize the auto scavenge state machine.
	/// </summary>
	/// <param name="token"></param>
	/// <returns>Ascending ordered collection of events related to the auto scavenge process</returns>
	IAsyncEnumerable<IEvent> ReadAutoScavengeEvents(CancellationToken token);
}
