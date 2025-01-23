// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace EventStore.Core.Services.Transport.Http;

internal static class WebHostBuilderExtensions {
	public static IWebHostBuilder UseStartup(this IWebHostBuilder builder, IStartup startup)
		=> builder.ConfigureServices(services => services.AddSingleton(startup));
}
