// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Security.EncryptionAtRest.MasterKeySources;

namespace EventStore.Security.EncryptionAtRest.MasterKeySourceConfigurators;

public interface IMasterKeySourceConfigurator<TOptions> {
	public string Name { get; }
	public void Configure(TOptions options, out IMasterKeySource masterKeySource);
}
