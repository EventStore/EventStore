using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Tests.Authorization {
	class TestAuthorizationProvider : IAuthorizationProvider {
		public ValueTask<bool> CheckAccessAsync(ClaimsPrincipal cp, Operation operation, CancellationToken ct) {
			return new ValueTask<bool>(true);
		}
	}
}
