using System.Collections.Generic;
using System.Security.Claims;

namespace EventStore.Core.Services.UserManagement {
	public class SystemAccounts {
		private static readonly IReadOnlyList<Claim> Claims = new[] {
			new Claim(ClaimTypes.Name, "system"), 
			new Claim(ClaimTypes.Role, "system"), 
			new Claim(ClaimTypes.Role, SystemRoles.Admins), 
		};
		public static readonly ClaimsPrincipal System = new ClaimsPrincipal(new ClaimsIdentity(Claims, "system"));
		public static readonly ClaimsPrincipal Anonymous = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]{new Claim(ClaimTypes.Anonymous, ""), }));

		//qq risk if we create a claims principle for projections?
		//qq consider granularity
		public static readonly string SystemName = "system";
		public static readonly string SystemIndexMergeName = "system-index-merge";
		public static readonly string SystemIndexScavengeName = "system-index-scavenge";
		public static readonly string SystemIndexCommitterName = "system-index-committer";
		public static readonly string SystemPersistentSubscriptionsName = "system-persistent-subscriptions";
		public static readonly string SystemRedactionName = "system-redaction";
		public static readonly string SystemScavengeName = "system-scavenge";
		public static readonly string SystemSubscriptionsName = "system-subscriptions";
		public static readonly string SystemWriterName = "system-writer";
	}
}
