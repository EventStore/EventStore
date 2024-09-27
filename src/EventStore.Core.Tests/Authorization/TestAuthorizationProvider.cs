using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Tests.Authorization;

class TestAuthorizationProvider : Plugin, IAuthorizationProvider {
	public ValueTask<bool> CheckAccessAsync(ClaimsPrincipal principal, Operation operation, CancellationToken cancellationToken) => 
		ValueTask.FromResult(true);
}
