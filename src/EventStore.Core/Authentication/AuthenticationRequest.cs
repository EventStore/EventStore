using System.Security.Principal;

namespace EventStore.Core.Authentication {
	public abstract class AuthenticationRequest {
		public readonly string Name;
		public readonly string SuppliedPassword;

		protected AuthenticationRequest(string name, string suppliedPassword) {
			Name = name;
			SuppliedPassword = suppliedPassword;
		}

		public abstract void Unauthorized();
		public abstract void Authenticated(IPrincipal principal);
		public abstract void Error();
		public abstract void NotReady();
	}
}
