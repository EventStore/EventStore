// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Common.Exceptions;
using EventStore.Common.Utils;
using EventStore.Core;
using EventStore.Core.Authorization.AuthorizationPolicies;
using EventStore.Core.Bus;
using EventStore.PluginHosting;
using EventStore.Plugins.Subsystems;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace EventStore.ClusterNode;

public class AuthorizationPolicyRegistryFactory: ISubsystemsPlugin, ISubsystem {
	public string Name => "AuthorizationPolicyRegistryFactory";
	public string DiagnosticsName => Name;
	public KeyValuePair<string, object?>[] DiagnosticsTags => [];
	public string Version => "1.0.0";
	public bool Enabled => true;
	public string? LicensePublicKey => null;
	public string CommandLineName => Name.ToLowerInvariant();
	public IAuthorizationPolicyRegistry AuthorizationPolicyRegistry => _authorizationPolicyRegistry!;
	private readonly ILogger _logger = Log.ForContext<AuthorizationPolicyRegistryFactory>();
	private IAuthorizationPolicyRegistry? _authorizationPolicyRegistry;
	private readonly ClusterVNodeOptions _options;
	private IPolicySelectorFactory[] _pluginSelectorFactories = [];

	public AuthorizationPolicyRegistryFactory(ClusterVNodeOptions options) {
		_options = options;
	}

	public IReadOnlyList<ISubsystem> GetSubsystems() {
		// Load up all policy selectors in the plugins directory
		var pluginsDir = new DirectoryInfo(Locations.PluginsDirectory);
		var pluginLoader = new PluginLoader(pluginsDir);

		var factories = pluginLoader.Load<IPolicySelectorFactory>();

		var subsystems = new List<ISubsystem> { this };
		_pluginSelectorFactories = factories?
			.Select(x => {
				_logger.Information("Loaded Authorization Policy plugin: {plugin}.", x.CommandLineName);
				return x;
			}).ToArray() ?? [];
		// ReSharper disable once SuspiciousTypeConversion.Global
		subsystems.AddRange(_pluginSelectorFactories.OfType<ISubsystem>());
		return subsystems.ToArray();
	}

	public void ConfigureServices(IServiceCollection services, IConfiguration configuration) {
	}

	public void ConfigureApplication(IApplicationBuilder builder, IConfiguration configuration) {
		if (_options.Application.Insecure) {
			_authorizationPolicyRegistry = new StaticAuthorizationPolicyRegistry([]);
			return;
		}

		var publisher = builder.ApplicationServices.GetRequiredService<IPublisher>();
		// Set up the legacy policy selector factory
		var allowAnonymousEndpointAccess = _options.Application.AllowAnonymousEndpointAccess;
		var allowAnonymousStreamAccess = _options.Application.AllowAnonymousStreamAccess;
		var overrideAnonymousGossipEndpointAccess = _options.Application.OverrideAnonymousEndpointAccessForGossip;
		var legacyPolicyFactory = new LegacyPolicySelectorFactory(
			allowAnonymousEndpointAccess,
			allowAnonymousStreamAccess,
			overrideAnonymousGossipEndpointAccess);
		var legacyPolicySelector = legacyPolicyFactory.Create(publisher);

		// Check if there is a default policy type. Use this if the settings stream is empty
		var defaultPolicyType =
			configuration.GetValue<string>("EventStore::Authorization:PolicyType") ?? string.Empty;
		AuthorizationPolicySettings defaultSettings;
		if (!string.IsNullOrEmpty(defaultPolicyType)) {
			if (_pluginSelectorFactories.Any(x => x.CommandLineName == defaultPolicyType)) {
				defaultSettings = new AuthorizationPolicySettings(defaultPolicyType);
			} else {
				throw new InvalidConfigurationException(
					$"No authorization policy with the name '{defaultPolicyType}' has been registered. " +
					$"Available authorization policies are: {string.Join(", ", _pluginSelectorFactories.Select(x => x.CommandLineName))}");
			}
		} else {
			// No default policy type configured. Use ACLs.
			defaultSettings = new AuthorizationPolicySettings(LegacyPolicySelectorFactory.LegacyPolicySelectorName);
		}

		// There are no plugins and a default policy type wasn't configured.
		// Don't use the stream based registry
		if (_pluginSelectorFactories.Length == 0) {
			_logger.Information("No authorization policy plugins found. Only ACLs will be used.");
			_authorizationPolicyRegistry = new StaticAuthorizationPolicyRegistry([legacyPolicySelector]);
			return;
		}

		_logger.Information("The default authorization policy settings are: {settings}", defaultSettings);
		_authorizationPolicyRegistry = new StreamBasedAuthorizationPolicyRegistry(publisher, legacyPolicySelector, _pluginSelectorFactories, defaultSettings);
	}

	public Task Start() {
		return _authorizationPolicyRegistry!.Start();
	}

	public Task Stop() {
		return _authorizationPolicyRegistry!.Stop();
	}
}
