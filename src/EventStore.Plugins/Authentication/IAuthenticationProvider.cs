using System.Threading.Tasks;

namespace EventStore.Plugins.Authentication {
	public interface IAuthenticationProvider {
		Task Initialize();
		void Authenticate(AuthenticationRequest authenticationRequest);
	}
}
