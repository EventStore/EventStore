using System.Security.Principal;

namespace EventStore.Core.Authentication {
	public class OpenGenericPrincipal : IPrincipal {
		private readonly GenericPrincipal _base;
		private readonly string[] _roles;

		public OpenGenericPrincipal(IIdentity identity, string[] roles) {
			_roles = roles;
			_base = new GenericPrincipal(identity, roles);
		}

		public OpenGenericPrincipal(string identity, params string[] roles) {
			_roles = roles;
			_base = new GenericPrincipal(new GenericIdentity(identity), roles);
		}

		public bool IsInRole(string role) {
			return _base.IsInRole(role);
		}

		public IIdentity Identity {
			get { return _base.Identity; }
		}

		public string[] Roles {
			get { return _roles; }
		}
	}
}
