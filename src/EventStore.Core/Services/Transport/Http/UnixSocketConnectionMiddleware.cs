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
		await _next(context).ConfigureAwait(false);
	}
}
