using System.Threading.Tasks;
using EventStore.Core.Services.UserManagement;
using EventStore.Plugins.Authentication;

namespace EventStore.Core.Authentication.InternalAuthentication {
	public class PassthroughAuthenticationProvider : IAuthenticationProvider {
		public void Authenticate(AuthenticationRequest authenticationRequest) =>
			authenticationRequest.Authenticated(SystemAccounts.System);
		public Task Initialize() => Task.CompletedTask;
	}
}
