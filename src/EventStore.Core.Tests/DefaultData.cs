// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Net;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Services;

namespace EventStore.Core.Tests;

public class DefaultData {
	public static string AdminUsername = SystemUsers.Admin;
	public static string AdminPassword = SystemUsers.DefaultAdminPassword;
	public static UserCredentials AdminCredentials = new UserCredentials(AdminUsername, AdminPassword);
	public static NetworkCredential AdminNetworkCredentials = new NetworkCredential(AdminUsername, AdminPassword);
	public static ClusterVNodeOptions.DefaultUserOptions DefaultUserOptions = new ClusterVNodeOptions.DefaultUserOptions() {
		DefaultAdminPassword = SystemUsers.DefaultAdminPassword,
		DefaultOpsPassword = SystemUsers.DefaultOpsPassword
	};
}
