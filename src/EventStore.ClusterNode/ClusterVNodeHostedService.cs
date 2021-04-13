using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
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
using EventStore.Core.Authentication.PassthroughAuthentication;
using EventStore.Core.Authorization;
using EventStore.Core.PluginModel;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;
using EventStore.Plugins.Authentication;
using EventStore.Plugins.Authorization;
using EventStore.Projections.Core;
using Microsoft.Extensions.Hosting;
using Serilog;
using EventStore.Core.LogAbstraction;

namespace EventStore.ClusterNode {
	internal class ClusterVNodeHostedService : IHostedService, IDisposable {
		private static readonly ILogger Log = Serilog.Log.ForContext<ClusterVNodeHostedService>();

		private readonly ClusterVNodeOptions _options;
		private readonly ExclusiveDbLock _dbLock;
		private readonly ClusterNodeMutex _clusterNodeMutex;

		public ClusterVNode Node { get; }

		public ClusterVNodeHostedService(ClusterVNodeOptions options) {
			if (options == null) throw new ArgumentNullException(nameof(options));
			_options = options.Projections.RunProjections >= ProjectionType.System
				? options.WithSubsystem(new ProjectionsSubsystem(options.Projections.ProjectionThreads,
					options.Projections.RunProjections, false,
					TimeSpan.FromMinutes(options.Projections.ProjectionsQueryExpiry),
					options.Projections.FaultOutOfOrderProjections))
				: options;

			if (!_options.Database.MemDb) {
				var absolutePath = Path.GetFullPath(_options.Database.Db);
				if (Runtime.IsWindows)
					absolutePath = absolutePath.ToLower();

				_dbLock = new ExclusiveDbLock(absolutePath);
				if (!_dbLock.Acquire())
					throw new InvalidConfigurationException($"Couldn't acquire exclusive lock on DB at '{_options.Database.Db}'.");
			}

			_clusterNodeMutex = new ClusterNodeMutex();
			if (!_clusterNodeMutex.Acquire())
				throw new InvalidConfigurationException($"Couldn't acquire exclusive Cluster Node mutex '{_clusterNodeMutex.MutexName}'.");

			var authorizationConfig = string.IsNullOrEmpty(_options.Auth.AuthorizationConfig)
				? _options.Application.Config
				: _options.Auth.AuthorizationConfig;

			var authenticationConfig = string.IsNullOrEmpty(_options.Auth.AuthenticationConfig)
				? _options.Application.Config
				: _options.Auth.AuthenticationConfig;

			var pluginLoader = new PluginLoader(new DirectoryInfo(Locations.PluginsDirectory));

			var plugInContainer = FindPlugins();

			var logFormat = LogFormatAbstractor.V2;
			Node = new ClusterVNode<string>(_options, logFormat, GetAuthenticationProviderFactory(), GetAuthorizationProviderFactory(),
				GetPersistentSubscriptionConsumerStrategyFactories());

			var runProjections = _options.Projections.RunProjections;
			var enabledNodeSubsystems = runProjections >= ProjectionType.System
				? new[] {NodeSubsystems.Projections}
				: Array.Empty<NodeSubsystems>();

			RegisterWebControllers(enabledNodeSubsystems);

			AuthorizationProviderFactory GetAuthorizationProviderFactory() {
				if (_options.Application.Insecure) {
					return new AuthorizationProviderFactory(_ => new PassthroughAuthorizationProviderFactory());
				}
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

				if (!authorizationTypeToPlugin.TryGetValue(_options.Auth.AuthorizationType.ToLowerInvariant(),
					out var factory)) {
					throw new ApplicationInitializationException(
						$"The authorization type {_options.Auth.AuthorizationType} is not recognised. If this is supposed " +
						$"to be provided by an authorization plugin, confirm the plugin DLL is located in {Locations.PluginsDirectory}." +
						Environment.NewLine +
						$"Valid options for authorization are: {string.Join(", ", authorizationTypeToPlugin.Keys)}.");
				}

				return factory;
			}

			static CompositionContainer FindPlugins() {
				var catalog = new AggregateCatalog();

				catalog.Catalogs.Add(new AssemblyCatalog(typeof(ClusterVNodeHostedService).Assembly));

				if (Directory.Exists(Locations.PluginsDirectory)) {
					Log.Information("Plugins path: {pluginsDirectory}", Locations.PluginsDirectory);

					Log.Information("Adding: {pluginsDirectory} to the plugin catalog.", Locations.PluginsDirectory);
					catalog.Catalogs.Add(new DirectoryCatalog(Locations.PluginsDirectory));

					foreach (string dirPath in Directory.GetDirectories(Locations.PluginsDirectory, "*",
						SearchOption.TopDirectoryOnly)) {
						Log.Information("Adding: {pluginsDirectory} to the plugin catalog.", dirPath);
						catalog.Catalogs.Add(new DirectoryCatalog(dirPath));
					}
				} else {
					Log.Information("Cannot find plugins path: {pluginsDirectory}", Locations.PluginsDirectory);
				}

				return new CompositionContainer(catalog);
			}

			IPersistentSubscriptionConsumerStrategyFactory[] GetPersistentSubscriptionConsumerStrategyFactories() {
				var allPlugins = plugInContainer.GetExports<IPersistentSubscriptionConsumerStrategyPlugin>();

				var strategyFactories = new List<IPersistentSubscriptionConsumerStrategyFactory>();

				foreach (var potentialPlugin in allPlugins) {
					try {
						var plugin = potentialPlugin.Value;
						Log.Information("Loaded consumer strategy plugin: {plugin} version {version}.", plugin.Name,
							plugin.Version);
						strategyFactories.Add(plugin.GetConsumerStrategyFactory());
					} catch (CompositionException ex) {
						Log.Error(ex, "Error loading consumer strategy plugin.");
					}
				}

				return strategyFactories.ToArray();
			}

			AuthenticationProviderFactory GetAuthenticationProviderFactory() {
				if (_options.Application.Insecure) {
					return new AuthenticationProviderFactory(_ => new PassthroughAuthenticationProviderFactory());
				}

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

				return authenticationTypeToPlugin.TryGetValue(_options.Auth.AuthenticationType.ToLowerInvariant(),
					out var factory)
					? factory
					: throw new ApplicationInitializationException(
						$"The authentication type {_options.Auth.AuthenticationType} is not recognised. If this is supposed " +
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

		public Task StopAsync(CancellationToken cancellationToken) =>
			Node.StopAsync(cancellationToken: cancellationToken);

		public void Dispose() {
			if (_dbLock is not {IsAcquired: true}) {
				return;
			}
			using (_dbLock) {
				_dbLock.Release();
			}
		}
	}
}
