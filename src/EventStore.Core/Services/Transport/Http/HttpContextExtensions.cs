using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;

namespace EventStore.Core.Services.Transport.Http;

public static class HttpContextExtensions {
	public static bool IsUnixSocketConnection(this HttpContext ctx) {
		var connectionItemsFeature = ctx.Features.Get<IConnectionItemsFeature>();
		if (connectionItemsFeature is null)
			return false;

		return connectionItemsFeature.Items.ContainsKey(UnixSocketConnectionMiddleware.UnixSocketConnectionKey);
	}
}
