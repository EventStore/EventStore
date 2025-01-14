// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Plugins.Authorization;

namespace EventStore.Auth.LegacyAuthorizationWithStreamAuthorizationDisabled;

public class LegacyAuthorizationWithStreamAuthorizationDisabledPlugin : IAuthorizationPlugin {
	public IAuthorizationProviderFactory GetAuthorizationProviderFactory(string authorizationConfigPath) =>
		new LegacyAuthorizationWithStreamAuthorizationDisabledProviderFactory();

	public string Name { get; } = "LegacyAuthorizationWithStreamAuthorizationDisabled";

	public string Version { get; } =
		typeof(LegacyAuthorizationWithStreamAuthorizationDisabledPlugin).Assembly.GetName().Version!.ToString();

	public string CommandLineName { get; } = "legacy-authorization-with-stream-authorization-disabled";
}
