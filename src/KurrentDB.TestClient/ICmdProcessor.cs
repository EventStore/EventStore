// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#pragma warning disable 1591
namespace KurrentDB.TestClient;

/// <summary>
/// Standalone command executable within the test client
/// </summary>
public interface ICmdProcessor {
	/// <summary>
	/// Keyword associated with this command (to start it)
	/// </summary>
	string Keyword { get; }

	/// <summary>
	/// Short usage string with optional and required parameters
	/// </summary>
	string Usage { get; }

	bool Execute(CommandProcessorContext context, string[] args);
}
