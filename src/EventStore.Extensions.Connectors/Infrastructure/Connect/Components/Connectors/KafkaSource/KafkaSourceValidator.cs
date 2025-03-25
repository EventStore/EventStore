// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using Kurrent.Connectors.Kafka;
using Kurrent.Surge.Connectors;
using Kurrent.Surge.Connectors.Sinks;

namespace EventStore.Connectors.Connect.Components.Connectors.KafkaSource;

public class KafkaSourceValidator : SourceConnectorValidator<KafkaSourceOptions> {
    public static readonly KafkaSourceValidator Instance = new();
}
