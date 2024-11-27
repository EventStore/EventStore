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

		// we may want to make the granularity here configurable, but as a starting point we only
		// separate scavenge, because its the only part the user has direct control over
		public static readonly string SystemChaserName = "system";
		public static readonly string SystemEpochManagerName = "system";
		public static readonly string SystemName = "system";
		public static readonly string SystemIndexCommitterName = "system";
		public static readonly string SystemPersistentSubscriptionsName = "system";
		public static readonly string SystemRedactionName = "system";
		public static readonly string SystemReplicationName = "system";
		public static readonly string SystemScavengeName = "system-scavenge";
		public static readonly string SystemSubscriptionsName = "system";
		public static readonly string SystemTelemetryName = "system";
		public static readonly string SystemWriterName = "system";
	}
}
