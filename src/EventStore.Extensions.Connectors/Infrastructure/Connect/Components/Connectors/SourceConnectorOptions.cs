// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using Kurrent.Surge.Connectors;
using Kurrent.Surge.Processors.Configuration;
using Kurrent.Surge.Schema;
using Microsoft.Extensions.Configuration;

namespace Kurrent.Surge.SourceConnector;

[PublicAPI]
public record SourceConnectorOptions {
	public ConnectorId ConnectorId { get; init; }

	public SchemaRegistry SchemaRegistry { get; init; } = SchemaRegistry.Global;

	public IConfiguration Configuration { get; init; }

	public IServiceProvider ServiceProvider { get; init; }

	public AutoLockOptions AutoLock { get; init; }

	public PublishStateChangesOptions PublishStateChanges { get; init; } = new();
}
