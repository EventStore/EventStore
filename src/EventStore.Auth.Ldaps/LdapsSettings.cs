// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;

namespace EventStore.Auth.Ldaps;

public class LdapsSettings {
	public string Host { get; set; }
	public int Port { get; set; }
	public bool ValidateServerCertificate { get; set; }
	public bool UseSSL { get; set; }

	public bool AnonymousBind { get; set; }
	public string BindUser { get; set; }
	public string BindPassword { get; set; }

	public string BaseDn { get; set; }
	public string ObjectClass { get; set; }
	public string Filter { get; set; }
	public string GroupMembershipAttribute { get; set; }

	public bool RequireGroupMembership { get; set; }
	public string RequiredGroupDn { get; set; }

	public int PrincipalCacheDurationSec { get; set; }

	public Dictionary<string, string> LdapGroupRoles { get; set; }
	public int LdapOperationTimeout { get; set; }

	public LdapsSettings() {
		Port = 636;
		ObjectClass = "organizationalPerson";
		Filter = "sAMAccountName";
		GroupMembershipAttribute = "memberOf";
		PrincipalCacheDurationSec = 60;
		UseSSL = true;
		LdapOperationTimeout = 5000;
	}
}
