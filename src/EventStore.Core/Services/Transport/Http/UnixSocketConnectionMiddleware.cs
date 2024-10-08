// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace EventStore.Core.Services.Transport.Http;
public class UnixSocketConnectionMiddleware {
	private readonly ConnectionDelegate _next;
	public const string UnixSocketConnectionKey = "UnixSocketConnection";

	public UnixSocketConnectionMiddleware(ConnectionDelegate next) {
		_next = next;
	}

	public async Task OnConnectAsync(ConnectionContext context) {
		context.Items.Add(UnixSocketConnectionKey, true);
		await _next(context);
	}
}
