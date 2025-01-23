// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http;

public static class HttpContextExtensions {
	public static bool IsUnixSocketConnection(this HttpContext ctx) {
		var connectionItemsFeature = ctx.Features.Get<IConnectionItemsFeature>();
		return connectionItemsFeature is not null && connectionItemsFeature.Items.ContainsKey(UnixSocketConnectionMiddleware.UnixSocketConnectionKey);
	}
}
