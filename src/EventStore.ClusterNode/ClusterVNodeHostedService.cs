using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using EventStore.Common.Exceptions;
using EventStore.Common.Options;
using EventStore.Common.Utils;
using EventStore.Core;
using EventStore.Core.Authentication;
using EventStore.Core.Services.Transport.Http.Controllers;
using System.Threading.Tasks;
using EventStore.Core.Authentication.InternalAuthentication;
using EventStore.Core.Authorization;
using EventStore.Plugins.Authentication;
using EventStore.Plugins.Authorization;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace EventStore.ClusterNode {
	internal class ClusterVNodeHostedService2 : IHostedService {
		private static readonly ILogger Log = Serilog.Log.ForContext<ClusterVNodeHostedService2>();

		private readonly ClusterVNodeOptions _options;
		private readonly ExclusiveDbLock _dbLock;
		private readonly ClusterNodeMutex _clusterNodeMutex;

		public ClusterVNode Node { get; }

		public ClusterVNodeHostedService2(ClusterVNodeOptions options) {
			if (options == null) throw new ArgumentNullException(nameof(options));
			_options = options;

			if (!options.Database.MemDb) {
				var absolutePath = Path.GetFullPath(options.Database.Db);
				if (Runtime.IsWindows)
					absolutePath = absolutePath.ToLower();

				_dbLock = new ExclusiveDbLock(absolutePath);
				if (!_dbLock.Acquire())
					throw new InvalidConfigurationException($"Couldn't acquire exclusive lock on DB at '{options.Database.Db}'.");
			}

			_clusterNodeMutex = new ClusterNodeMutex();
			if (!_clusterNodeMutex.Acquire())
				throw new InvalidConfigurationException($"Couldn't acquire exclusive Cluster Node mutex '{_clusterNodeMutex.MutexName}'.");

			var authorizationConfig = string.IsNullOrEmpty(options.Auth.AuthorizationConfig)
				? options.Application.Config
				: options.Auth.AuthorizationConfig;

			var authenticationConfig = string.IsNullOrEmpty(options.Auth.AuthenticationConfig)
				? options.Application.Config
				: options.Auth.AuthenticationConfig;

			var pluginLoader = new PluginLoader(new DirectoryInfo(Locations.PluginsDirectory));

			Node = new ClusterVNode(options, GetAuthenticationProviderFactory(), GetAuthorizationProviderFactory());

			var runProjections = options.Projections.RunProjections;
			var enabledNodeSubsystems = runProjections >= ProjectionType.System
				? new[] {NodeSubsystems.Projections}
				: Array.Empty<NodeSubsystems>();

			RegisterWebControllers(enabledNodeSubsystems);

			AuthorizationProviderFactory GetAuthorizationProviderFactory() {
				var authorizationTypeToPlugin = new Dictionary<string, AuthorizationProviderFactory> {
					{
						"internal", new AuthorizationProviderFactory(components =>
							new LegacyAuthorizationProviderFactory(components.MainQueue))
					}
				};

				foreach (var potentialPlugin in pluginLoader.Load<IAuthorizationPlugin>()) {
					try {
						var commandLine = potentialPlugin.CommandLineName.ToLowerInvariant();
						Log.Information(
							"Loaded authorization plugin: {plugin} version {version} (Command Line: {commandLine})",
							potentialPlugin.Name, potentialPlugin.Version, commandLine);
						authorizationTypeToPlugin.Add(commandLine,
							new AuthorizationProviderFactory(_ =>
								potentialPlugin.GetAuthorizationProviderFactory(authorizationConfig)));
					} catch (CompositionException ex) {
						Log.Error(ex, "Error loading authentication plugin.");
					}
				}

				if (!authorizationTypeToPlugin.TryGetValue(options.Auth.AuthorizationType.ToLowerInvariant(), out var factory)) {
					throw new ApplicationInitializationException(
						$"The authorization type {options.Auth.AuthorizationType} is not recognised. If this is supposed " +
						$"to be provided by an authorization plugin, confirm the plugin DLL is located in {Locations.PluginsDirectory}." +
						Environment.NewLine +
						$"Valid options for authorization are: {string.Join(", ", authorizationTypeToPlugin.Keys)}.");
				}

				return factory;
			}

			AuthenticationProviderFactory GetAuthenticationProviderFactory() {
				var authenticationTypeToPlugin = new Dictionary<string, AuthenticationProviderFactory> {
					{
						"internal", new AuthenticationProviderFactory(components =>
							new InternalAuthenticationProviderFactory(components))
					}
				};

				foreach (var potentialPlugin in pluginLoader.Load<IAuthenticationPlugin>()) {
					try {
						var commandLine = potentialPlugin.CommandLineName.ToLowerInvariant();
						Log.Information(
							"Loaded authentication plugin: {plugin} version {version} (Command Line: {commandLine})",
							potentialPlugin.Name, potentialPlugin.Version, commandLine);
						authenticationTypeToPlugin.Add(commandLine,
							new AuthenticationProviderFactory(_ =>
								potentialPlugin.GetAuthenticationProviderFactory(authenticationConfig)));
					} catch (CompositionException ex) {
						Log.Error(ex, "Error loading authentication plugin.");
					}
				}

				return authenticationTypeToPlugin.TryGetValue(options.Auth.AuthenticationType.ToLowerInvariant(), out var factory)
					? factory
					: throw new ApplicationInitializationException(
						$"The authentication type {options.Auth.AuthenticationType} is not recognised. If this is supposed " +
						$"to be provided by an authentication plugin, confirm the plugin DLL is located in {Locations.PluginsDirectory}." +
						Environment.NewLine +
						$"Valid options for authentication are: {string.Join(", ", authenticationTypeToPlugin.Keys)}.");
			}
		}

		private void RegisterWebControllers(NodeSubsystems[] enabledNodeSubsystems) {
			if (!_options.Interface.DisableAdminUi) {
				Node.HttpService.SetupController(new ClusterWebUiController(Node.MainQueue,
					enabledNodeSubsystems));
			}
		}

		public Task StartAsync(CancellationToken cancellationToken) =>
			_options.Application.WhatIf ? Task.CompletedTask : Node.StartAsync(false);

		public Task StopAsync(CancellationToken cancellationToken) {
			if (_dbLock is {IsAcquired: true}) {
				using (_dbLock) {
					_dbLock.Release();
				}
			}

			return Node.StopAsync(cancellationToken: cancellationToken);
		}
	}
}
