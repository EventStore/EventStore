// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using EventStore.Plugins.Authorization;
using EventStore.Transport.Http;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace EventStore.Core.Services.Transport.Http;

public class AuthorizationMiddleware(IAuthorizationProvider authorization) : IMiddleware {
	private static readonly ILogger Log = Serilog.Log.ForContext<AuthorizationMiddleware>();

	public async Task InvokeAsync(HttpContext context, RequestDelegate next) {
		if (InternalHttpHelper.TryGetInternalContext(context, out var manager, out var match, out _)) {
			if (await authorization.CheckAccessAsync(manager.User, match.ControllerAction.Operation(match.TemplateMatch), context.RequestAborted)) {
				await next(context);
				return;
			}

			context.Response.StatusCode = HttpStatusCode.Unauthorized;
			return;
		}

		//LOG
		await next(context);
		// TODO: Why was it like this? It fails for all HTTP calls for which there's no component.
		// Log.Error("Failed to get internal http components for request {requestId}", context.TraceIdentifier);
		// context.Response.StatusCode = HttpStatusCode.InternalServerError;
	}
}
