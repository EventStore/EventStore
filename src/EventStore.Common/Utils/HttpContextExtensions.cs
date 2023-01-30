using Microsoft.AspNetCore.Http;

namespace EventStore.Common.Utils;

public static class HttpContextExtensions {
	public static bool IsUnixSocket(this HttpContext ctx) =>
		ctx.Connection.RemoteIpAddress is null; // non-ip transport
}
