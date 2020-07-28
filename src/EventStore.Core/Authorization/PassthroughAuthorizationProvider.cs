using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Authorization {
	public class PassthroughAuthorizationProvider : IAuthorizationProvider {
		public ValueTask<bool> CheckAccessAsync(ClaimsPrincipal cp, Operation operation, CancellationToken ct) =>
			new ValueTask<bool>(true);
	}
}
