// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EventStore.Core.Services.Transport.Http.Controllers;

public static class PingEndpoints {
	public static void MapGetPing(this IEndpointRouteBuilder app) {
		app.MapGet("/ping", () => Results.Ok("Ping request successfully handled"));
	}
}
