// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.DependencyInjection;
// using static Kurrent.Surge.DataProtection.DataProtectionBuilderExtensions;
//
// namespace EventStore.Connectors.Infrastructure.Security;
//
// public static class DataProtection {
//     public static IServiceCollection AddSurgeDataProtection(this IServiceCollection services) {
//         var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
//
//         var token = configuration["DataProtection:Token"];
//
//         if (string.IsNullOrEmpty(token))
//             throw new InvalidOperationException("The DataProtection:Token configuration value is required.");
//
//         services
//             .AddDataProtection(configuration)
//             .ProtectKeysWithToken(token)
//             .PersistKeysToSurge();
//
//         return services;
//     }
// }
