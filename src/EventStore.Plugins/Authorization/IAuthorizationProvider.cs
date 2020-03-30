using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Plugins.Authorization {
	public interface IAuthorizationProvider {
		ValueTask<bool> CheckAccessAsync(ClaimsPrincipal cp, Operation operation, CancellationToken ct);
	}
}
