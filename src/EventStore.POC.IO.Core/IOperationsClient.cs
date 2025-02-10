// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.POC.IO.Core;

/// <summary>
/// A client abstraction used for administrative operations.
/// </summary>
public interface IOperationsClient {
	/// <summary>
	/// Demotes the current node to a follower if it is a leader. Don't expect the node to be demoted immediately. The
	/// call start demotion process.
	/// </summary>
	void Resign();
}
