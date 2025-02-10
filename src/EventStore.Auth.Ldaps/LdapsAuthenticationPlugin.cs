// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.ComponentModel.Composition;
using EventStore.Plugins.Authentication;

namespace EventStore.Auth.Ldaps;

[Export(typeof(IAuthenticationPlugin))]
public class LdapsAuthenticationPlugin : IAuthenticationPlugin {
	public string Name { get { return "LDAPS"; } }

	public string Version {
		get { return typeof(LdapsAuthenticationPlugin).Assembly.GetName().Version.ToString(); }
	}

	public string CommandLineName { get { return "ldaps"; } }

	public IAuthenticationProviderFactory GetAuthenticationProviderFactory(string authenticationConfigPath) {
		if (string.IsNullOrWhiteSpace(authenticationConfigPath))
			throw new LdapsConfigurationException(string.Format(
				"No LDAPS configuration file was specified. Use the --{0} option to specify " +
				"the path to the LDAPS configuration.", "authentication-config-file"));

		return new LdapsAuthenticationProviderFactory(authenticationConfigPath);
	}
}
