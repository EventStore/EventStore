// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Plugins.MD5;

namespace EventStore.MD5;

internal class MD5ProviderFactory : IMD5ProviderFactory {
	public IMD5Provider Build() => new MD5Provider();
}
