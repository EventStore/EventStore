using System.Threading.Tasks;

namespace EventStore.Core.Authentication {
	public interface IAuthenticationProvider {
		Task Initialize();
		void Authenticate(AuthenticationRequest authenticationRequest);
	}
}
