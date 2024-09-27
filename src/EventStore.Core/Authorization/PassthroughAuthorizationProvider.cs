#nullable enable

using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Authorization;

public class PassthroughAuthorizationProvider : AuthorizationProviderBase {
	public override ValueTask<bool> CheckAccessAsync(ClaimsPrincipal principal, Operation operation, CancellationToken cancellationToken) =>
		ValueTask.FromResult(true);
}
