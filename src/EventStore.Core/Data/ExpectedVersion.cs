// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Data;

public static class ExpectedVersion {
	public const long Any = -2;

	public const long NoStream = -1;

	public const long Invalid = -3;
	public const long StreamExists = -4;
}
