using System.Security.Principal;

namespace EventStore.Core.Services.UserManagement {
	public class SystemAccount : IPrincipal {
		public static readonly SystemAccount Principal = new SystemAccount();

		public IIdentity Identity {
			get { return _identity; }
		}

		private readonly IIdentity _identity = new SystemAccountIdentity();

		private SystemAccount() {
		}

		public bool IsInRole(string role) {
			return true;
		}

		private class SystemAccountIdentity : IIdentity {
			public string Name {
				get { return "system"; }
			}

			public string AuthenticationType {
				get { return "system"; }
			}

			public bool IsAuthenticated {
				get { return true; }
			}
		}
	}
}
