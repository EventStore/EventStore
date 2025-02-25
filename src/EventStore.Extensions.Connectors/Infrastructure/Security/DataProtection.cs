// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Connect.Connectors;
using EventStore.Connect.Schema;
using EventStore.Connectors.Connect.Components.Connectors;
using EventStore.Connectors.Management;
using Kurrent.Surge.DataProtection;
using Kurrent.Surge.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DataProtectionProtocol = Kurrent.Surge.DataProtection.Protocol;

namespace EventStore.Connectors.Infrastructure.Security;

public static class DataProtection {
    public static IServiceCollection AddSurgeDataProtection(this IServiceCollection services) {
        var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();

        var token = configuration["KurrentDB:DataProtection:Token"] ?? configuration["EventStore:DataProtection:Token"];

        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("The DataProtection:Token configuration value is required");

        services
            .AddMessageSchemaRegistration()
            .AddDataProtection(configuration)
            .ProtectKeysWithToken(token)
            .PersistKeysToSurge();

        services.AddSingleton<ConnectorDomainServices.ProtectConnectorSettings>(ctx => {
            var connectorDataProtector = ctx.GetService<IConnectorDataProtector>() ?? ConnectorsMasterDataProtector.Instance;
            var surgeDataProtector     = ctx.GetRequiredService<IDataProtector>();

            return (connectorId, settings) => connectorDataProtector.Protect(
                connectorId,
                settings: new Dictionary<string, string?>(settings, StringComparer.OrdinalIgnoreCase),
                surgeDataProtector
            );
        });

        return services;
    }

    static IServiceCollection AddMessageSchemaRegistration(this IServiceCollection services) =>
        services.AddSchemaRegistryStartupTask("Data Protection Schema Registration",
            static async (registry, token) => {
                var schemaInfo = new SchemaInfo(typeof(DataProtectionProtocol.EncryptionKey).FullName!, SchemaDefinitionType.Json);
                await registry.RegisterSchema<DataProtectionProtocol.EncryptionKey>(schemaInfo, cancellationToken: token);
            });
}
