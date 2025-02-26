using EventStore.Connect.Connectors;
using Kurrent.Surge.Connectors;
using Kurrent.Surge.Leases;
using EventStore.Connect.Schema;
using EventStore.Connectors.System;
using EventStore.Core.Bus;
using Kurrent.Surge.DataProtection;
using Kurrent.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using static EventStore.Connectors.ConnectorsFeatureConventions;

using ConnectContracts = Kurrent.Surge.Protocol;
using ControlContracts = EventStore.Connectors.Control.Contracts;

namespace EventStore.Connectors.Control;

public static class ControlPlaneWireUp {
    public static IServiceCollection AddConnectorsControlPlane(this IServiceCollection services) {
        services
            .AddMessageSchemaRegistration()
            .AddConnectorsActivator()
            .AddConnectorsControlRegistry()
            .AddSingleton<GetNodeLifetimeService>(ctx =>
                component => new NodeLifetimeService(
                    component,
                    ctx.GetRequiredService<IPublisher>(),
                    ctx.GetRequiredService<ISubscriber>(),
                    ctx.GetService<ILogger<NodeLifetimeService>>()));

        services.AddSingleton<IHostedService, ConnectorsControlService>();

        return services;
    }

    static IServiceCollection AddMessageSchemaRegistration(this IServiceCollection services) =>
        services.AddSchemaRegistryStartupTask(
            "Connectors Control Schema Registration",
            static async (registry, token) => {
                Task[] tasks = [
                    RegisterControlMessages<ControlContracts.ActivatedConnectorsSnapshot>(registry, token),
                    RegisterControlMessages<ConnectContracts.Processors.ProcessorStateChanged>(registry, token),
                    RegisterControlMessages<ConnectContracts.Consumers.Checkpoint>(registry, token),
                    RegisterControlMessages<Lease>(registry, token), //TODO SS: transform Lease into a message contract in Connect
                ];

                await tasks.WhenAll();
            }
        );

    static IServiceCollection AddConnectorsActivator(this IServiceCollection services) =>
        services
            .AddSingleton<ISystemConnectorFactory>(ctx => {
                var validator              = ctx.GetRequiredService<IConnectorValidator>();
                var connectorDataProtector = ctx.GetService<IConnectorDataProtector>() ?? ConnectorsMasterDataProtector.Instance;
                var dataProtector          = ctx.GetRequiredService<IDataProtector>();

                var options = new SystemConnectorsFactoryOptions {
                    CheckpointsStreamTemplate = Streams.CheckpointsStreamTemplate,
                    LifecycleStreamTemplate   = Streams.LifecycleStreamTemplate,
                    AutoLock = new() {
                        LeaseDuration      = TimeSpan.FromSeconds(5),
                        AcquisitionTimeout = TimeSpan.FromSeconds(60),
                        AcquisitionDelay   = TimeSpan.FromSeconds(5),
                        StreamTemplate     = Streams.LeasesStreamTemplate
                    },
                    ProcessConfiguration = configuration => {
                        validator.EnsureValid(configuration);
                        return connectorDataProtector.Unprotect(configuration, dataProtector).AsTask().GetAwaiter().GetResult();
                    }
                };

                return new SystemConnectorsFactory(options, ctx);
            })
            .AddSingleton<ConnectorsActivator>();

    static IServiceCollection AddConnectorsControlRegistry(this IServiceCollection services) =>
        services
            .AddSingleton(new ConnectorsControlRegistryOptions {
                Filter           = Filters.ManagementFilter,
                SnapshotStreamId = Streams.ControlConnectorsRegistryStream
            })
            .AddSingleton<ConnectorsControlRegistry>()
            .AddSingleton<GetActiveConnectors>(static ctx => {
                var registry = ctx.GetRequiredService<ConnectorsControlRegistry>();
                return registry.GetConnectors;
            });
}
