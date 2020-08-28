﻿using System.Collections.Generic;
using System.Security.Claims;
using EventStore.Core.TransactionLog.Services;

namespace EventStore.Core.Services.UserManagement {
	public class SystemAccounts {
		private static readonly IReadOnlyList<Claim> Claims = new[] {
			new Claim(ClaimTypes.Name, "system"), 
			new Claim(ClaimTypes.Role, "system"), 
			new Claim(ClaimTypes.Role, SystemRoles.Admins), 
		};
		public static readonly ClaimsPrincipal System = new ClaimsPrincipal(new ClaimsIdentity(Claims, "system"));
		public static readonly ClaimsPrincipal Anonymous = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]{new Claim(ClaimTypes.Anonymous, ""), }));
	}
}
