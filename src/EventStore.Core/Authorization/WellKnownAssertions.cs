using System.Security.Claims;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.Services;

namespace EventStore.Core.Authorization {
	public static class WellKnownAssertions {
		public static readonly IAssertion System = new MultipleClaimMatchAssertion(Grant.Allow, MultipleMatchMode.All,
			new Claim(ClaimTypes.Name, "system"), new Claim(ClaimTypes.Authentication, "ES-Legacy"));

		public static readonly IAssertion Admin = new MultipleClaimMatchAssertion(Grant.Allow, MultipleMatchMode.Any,
			new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, SystemRoles.Admins));
	}
}
