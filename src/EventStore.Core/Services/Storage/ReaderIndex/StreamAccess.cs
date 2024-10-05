// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Services.Storage.ReaderIndex;

public struct StreamAccess {
	public readonly bool Granted;
	public readonly bool Public;

	public StreamAccess(bool granted) {
		Granted = granted;
		Public = false;
	}

	public StreamAccess(bool granted, bool @public) {
		Granted = granted;
		Public = @public;
	}
}
