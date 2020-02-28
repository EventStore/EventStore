using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

namespace EventStore.Core.Authentication {
	public abstract class AuthenticationRequest {
		public readonly string Id;
		public readonly string Name;
		public readonly string SuppliedPassword;
		public readonly X509Certificate SuppliedClientCertificate;

		protected AuthenticationRequest(string id, string name, string suppliedPassword, X509Certificate suppliedClientCertificate) {
			Id = id;
			Name = name;
			SuppliedPassword = suppliedPassword;
			SuppliedClientCertificate = suppliedClientCertificate;
		}

		public abstract void Unauthorized();
		public abstract void Authenticated(ClaimsPrincipal principal);
		public abstract void Error();
		public abstract void NotReady();
	}
}
