// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.POC.IO.Core;

namespace EventStore.POC.ConnectorsEngine.Infrastructure.Serialization;

public interface ISerializer {
	EventToWrite Serialize(Message evt);
	bool TryDeserialize(Event evt, out Message? message, out Exception? exception);
}
