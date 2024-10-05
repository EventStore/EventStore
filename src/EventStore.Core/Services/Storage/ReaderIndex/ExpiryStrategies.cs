// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Services.Storage.ReaderIndex;

public interface IExpiryStrategy {
	public DateTime? GetExpiry();
}

// Generates null expiry. Default expiry of now + ESConsts.ReadRequestTimeout will be in effect.
public class DefaultExpiryStrategy : IExpiryStrategy {
	public DateTime? GetExpiry() => null;
}
