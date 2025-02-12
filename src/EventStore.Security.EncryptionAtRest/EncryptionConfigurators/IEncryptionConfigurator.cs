// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using EventStore.Plugins.Transforms;

namespace EventStore.Security.EncryptionAtRest.EncryptionConfigurators;

public interface IEncryptionConfigurator<TOptions> {
	public string Name { get; }
	public void Configure(
		TOptions options,
		IReadOnlyList<MasterKey> masterKeys,
		out IDbTransform encryptionTransform);
}
