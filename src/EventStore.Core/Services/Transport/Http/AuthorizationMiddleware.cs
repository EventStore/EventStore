// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using System.Threading.Tasks;
using EventStore.Plugins.Authorization;
using EventStore.Transport.Http;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace EventStore.Core.Services.Transport.Http;

public class AuthorizationMiddleware : IMiddleware {
	private static readonly ILogger Log = Serilog.Log.ForContext<AuthorizationMiddleware>();
	private readonly IAuthorizationProvider _authorization;

	public AuthorizationMiddleware(IAuthorizationProvider authorization) {
		_authorization = authorization;
	}
	public async Task InvokeAsync(HttpContext context, RequestDelegate next) {
		if (InternalHttpHelper.TryGetInternalContext(context, out var manager, out var match, out _)) {
			if (await _authorization.CheckAccessAsync(manager.User, match.ControllerAction.Operation(match.TemplateMatch), context.RequestAborted)) {
				await next(context);
				return;
			}

			context.Response.StatusCode = HttpStatusCode.Unauthorized;
			return;
		}
		//LOG
		Log.Error("Failed to get internal http components for request {requestId}", context.TraceIdentifier);
		context.Response.StatusCode = HttpStatusCode.InternalServerError;
	}
}
