// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Data;

#pragma warning disable 1591

namespace KurrentDB.TestClient.Commands.DvuBasic;

public interface IBasicProducer {
	string Name { get; }

	Event Create(int version);
	bool Equal(int eventVersion, string eventType, byte[] actualData);
}
