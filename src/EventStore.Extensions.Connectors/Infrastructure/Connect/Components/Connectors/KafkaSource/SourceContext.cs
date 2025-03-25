// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using Kurrent.Surge.Schema;
using Kurrent.Surge.Schema.Serializers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Kurrent.Surge.Connectors.Sources;

public record ConnectorMetadata {
    /// <summary>
    /// The unique identifier of the connector. And it is the same for all worker instances of the processor.
    /// </summary>
    public string ConnectorId { get; init; }

    /// <summary>
    /// The unique identifier of the client. This identifier is different per worker instance of the processor.
    /// </summary>
    public string ClientId { get; init; }
}

public abstract record SourceBaseContext {
    public ConnectorMetadata Metadata          { get; internal init; }
    public IConfiguration    Configuration     { get; internal init; }
    public ISchemaRegistry   SchemaRegistry    { get; internal init; }
    public ISchemaSerializer Serializer        { get; internal init; }
    public ILogger           Logger            { get; internal init; }
    public CancellationToken CancellationToken { get; internal init; }

    public string ConnectorId => Metadata.ConnectorId;
}

public record SourceOpenContext : SourceBaseContext {
    public IServiceProvider ServiceProvider { get; internal init; }
}

public record SourceCloseContext : SourceBaseContext;

public class SourceStopException(Exception innerException) : Exception("Sink stopped by request", innerException);
