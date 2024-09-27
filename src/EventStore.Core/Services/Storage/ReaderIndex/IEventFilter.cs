// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Data;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public interface IEventFilter {
		bool IsEventAllowed(EventRecord eventRecord);
	}
}
