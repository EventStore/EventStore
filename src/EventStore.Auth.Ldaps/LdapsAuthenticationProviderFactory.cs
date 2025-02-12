// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Plugins;
using EventStore.Plugins.Authentication;

namespace EventStore.Auth.Ldaps;

internal class LdapsAuthenticationProviderFactory : IAuthenticationProviderFactory {
	public const int CachedPrincipalCount = 1000;
	private readonly string _configPath;
	private LdapsAuthenticationProvider _authenticationProvider;

	public LdapsAuthenticationProviderFactory(string configPath) {
		_configPath = configPath;
	}

	public IAuthenticationProvider Build(bool logFailedAuthenticationAttempts) {
		var ldapsSettings = ConfigParser.ReadConfiguration<LdapsSettings>(_configPath, "LdapsAuth");
		_authenticationProvider = new LdapsAuthenticationProvider(ldapsSettings,
			CachedPrincipalCount, logFailedAuthenticationAttempts);
		return _authenticationProvider;
	}
}
