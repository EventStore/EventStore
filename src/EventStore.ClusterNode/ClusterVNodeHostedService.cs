#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Runtime;
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
using EventStore.Core.Certificates;
using EventStore.Core.Hashing;
using EventStore.Core.PluginModel;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;
using EventStore.PluginHosting;
using EventStore.Plugins;
using EventStore.Plugins.Authentication;
using EventStore.Plugins.Authorization;
using EventStore.Plugins.MD5;
using EventStore.Plugins.Subsystems;
using EventStore.Projections.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using EventStore.Core.LogAbstraction;

namespace EventStore.ClusterNode;

public class ClusterVNodeHostedService : IHostedService, IDisposable {
	static readonly ILogger Logger = Log.ForContext<ClusterVNodeHostedService>();

	readonly ExclusiveDbLock? _dbLock;

	public ClusterVNodeHostedService(ClusterVNodeOptions? options, CertificateProvider certificateProvider, IConfiguration configuration) {
		if (options is null) 
			throw new ArgumentNullException(nameof(options));

		// two plugin mechanisms; pluginLoader is the new one
		var pluginLoader = new PluginLoader(new(Locations.PluginsDirectory));
		
		options = options.WithPlugins(pluginLoader.GetSubsystemsPlugins());
		
		ConfigureMD5();

		ConfigureProjections(out var projectionType);

		_dbLock = AcquireDbLock();

		AcquireExclusiveNodeMutex();
		
		Options = options;
		
		Node = ClusterVNode.Create(
			Options, GetLogFormatFactory(), 
			pluginLoader.GetAuthenticationProviderFactory(Options),
			pluginLoader.GetAuthorizationProviderFactory(Options), 
			GetPersistentSubscriptionConsumerStrategyFactories(),
			certificateProvider,
			configuration
		);

		Node.RegisterWebUiControllers(projectionType, Options.Interface.DisableAdminUi);
		
		return;

		void ConfigureMD5() {
			var md5Provider = pluginLoader.GetMD5ProviderFactories()
				.FirstOrDefault()?.Build() ?? new NetMD5Provider();
			
			MD5.UseProvider(md5Provider);
		
			try {
				options = options.WithPlugin(md5Provider);
			} catch {
				throw new InvalidConfigurationException("Failed to configure MD5. If FIPS mode is enabled, please use the FIPS commercial plugin or disable FIPS mode.");
			}
		}

		void ConfigureProjections(out ProjectionType runProjections) {
			runProjections = options.DevMode.Dev && options.Projection.RunProjections == ProjectionType.None
				? ProjectionType.System
				: options.Projection.RunProjections;
			
			var startStandardProjections = options.Projection.StartStandardProjections || options.DevMode.Dev;

			if (runProjections >= ProjectionType.System) {
				options = options.WithPlugin(new ProjectionsSubsystem(new(
					options.Projection.ProjectionThreads,
					runProjections,
					startStandardProjections,
					TimeSpan.FromMinutes(options.Projection.ProjectionsQueryExpiry),
					options.Projection.FaultOutOfOrderProjections,
					options.Projection.ProjectionCompilationTimeout,
					options.Projection.ProjectionExecutionTimeout
				)));
			}
		}

		ExclusiveDbLock? AcquireDbLock() {
			if (options.Database.MemDb)
				return null;

			var absolutePath = RuntimeInformation.IsWindows
				? Path.GetFullPath(options.Database.Db).ToLower()
				: Path.GetFullPath(options.Database.Db);

			var dbLock = new ExclusiveDbLock(absolutePath);
			if (!dbLock.Acquire())
				throw new InvalidConfigurationException($"Couldn't acquire exclusive lock on DB at '{options.Database.Db}'.");

			return dbLock;
		}
		
		void AcquireExclusiveNodeMutex() {
			var clusterNodeMutex = new ClusterNodeMutex();
			if (!clusterNodeMutex.Acquire())
				throw new InvalidConfigurationException($"Couldn't acquire exclusive Cluster Node mutex '{clusterNodeMutex.MutexName}'.");
		}

		dynamic GetLogFormatFactory() {
			return options.Database.DbLogFormat switch {
				DbLogFormat.V2 => new LogV2FormatAbstractorFactory(),
				DbLogFormat.ExperimentalV3 => new LogV3FormatAbstractorFactory(),
				_ => throw new ArgumentOutOfRangeException(nameof(options.Database.DbLogFormat), "Unexpected log format specified.")
			};
		}

		static List<IPersistentSubscriptionConsumerStrategyFactory> GetPersistentSubscriptionConsumerStrategyFactories() {
			var result = new List<IPersistentSubscriptionConsumerStrategyFactory>();

			foreach (var potentialPlugin in AllPlugins()) {
				try {
					var plugin = potentialPlugin.Value;
				
					Logger.Information(
						"Loaded consumer strategy plugin: {Plugin} version {Version}.", 
						plugin.Name, plugin.Version
					);
				
					result.Add(plugin.GetConsumerStrategyFactory());
				}
				catch (CompositionException ex) {
					Logger.Error(ex, "Error loading consumer strategy plugin.");
				}
			}

			return result;
		
			static IEnumerable<Lazy<IPersistentSubscriptionConsumerStrategyPlugin>> AllPlugins() {
				var catalog = new AggregateCatalog();

				catalog.Catalogs.Add(new AssemblyCatalog(typeof(ClusterVNodeHostedService).Assembly));

				if (Directory.Exists(Locations.PluginsDirectory)) {
					Logger.Information("Plugins path: {PluginsDirectory}", Locations.PluginsDirectory);

					Logger.Information("Adding: {PluginsDirectory} to the plugin catalog.", Locations.PluginsDirectory);
					catalog.Catalogs.Add(new DirectoryCatalog(Locations.PluginsDirectory));

					foreach (var dirPath in Directory.GetDirectories(Locations.PluginsDirectory, "*", SearchOption.TopDirectoryOnly)) {
						Logger.Information("Adding: {PluginsDirectory} to the plugin catalog.", dirPath);
						catalog.Catalogs.Add(new DirectoryCatalog(dirPath));
					}
				}
				else {
					Logger.Information("Cannot find plugins path: {PluginsDirectory}", Locations.PluginsDirectory);
				}

				return new CompositionContainer(catalog).GetExports<IPersistentSubscriptionConsumerStrategyPlugin>();
			}
		}
	}
	
	public ClusterVNode Node { get; }
	public ClusterVNodeOptions Options { get; }
	
	public Task StartAsync(CancellationToken cancellationToken) =>
		Options.Application.WhatIf ? Task.CompletedTask : Node.StartAsync(false);

	public Task StopAsync(CancellationToken cancellationToken) =>
		Node.StopAsync(cancellationToken: cancellationToken);

	public void Dispose() {
		if (_dbLock is not {IsAcquired: true})
			return;
		
		using (_dbLock) _dbLock.Release();
	}
}

static class PluginLoaderExtensions {
	static readonly ILogger Logger = Log.ForContext<ClusterVNodeHostedService>();
	
	public static IEnumerable<IPlugableComponent> GetSubsystemsPlugins(this PluginLoader pluginLoader) {
		foreach (var plugin in pluginLoader.Load<ISubsystemsPlugin>()) {
			Logger.Information(
				"Loaded SubsystemsPlugin plugin: {Plugin} {Version}.",
				plugin.CommandLineName, plugin.Version
			);
		
			foreach (var subsystem in plugin.GetSubsystems())
				yield return subsystem;
		}
	}

	public static AuthorizationProviderFactory GetAuthorizationProviderFactory(this PluginLoader pluginLoader, ClusterVNodeOptions options) {
		if (options.Application.Insecure)
			return new(_ => new PassthroughAuthorizationProviderFactory());

		var authorizationTypeToPlugin = new Dictionary<string, AuthorizationProviderFactory> {
			{
				"internal", new AuthorizationProviderFactory(components =>
					new LegacyAuthorizationProviderFactory(components.MainQueue,
						options.Application.AllowAnonymousEndpointAccess,
						options.Application.AllowAnonymousStreamAccess,
						options.Application.OverrideAnonymousEndpointAccessForGossip))
			}
		};
		
		var authorizationConfig = string.IsNullOrEmpty(options.Auth.AuthorizationConfig)
			? options.Application.Config
			: options.Auth.AuthorizationConfig;

		foreach (var plugin in pluginLoader.Load<IAuthorizationPlugin>()) {
			try {
				Logger.Information(
					"Loaded authorization plugin: {Plugin} version {Version} (Command Line: {CommandLine})",
					plugin.Name, plugin.Version, plugin.CommandLineName
				);
				
				authorizationTypeToPlugin.Add(
					plugin.CommandLineName,
					new(_ => plugin.GetAuthorizationProviderFactory(authorizationConfig))
				);
			}
			catch (CompositionException ex) {
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

	public static AuthenticationProviderFactory GetAuthenticationProviderFactory(this PluginLoader pluginLoader, ClusterVNodeOptions options) {
		if (options.Application.Insecure)
			return new(_ => new PassthroughAuthenticationProviderFactory());

		var authenticationTypeToPlugin = new Dictionary<string, AuthenticationProviderFactory> {
			{
				"internal", new AuthenticationProviderFactory(components =>
					new InternalAuthenticationProviderFactory(components, options.DefaultUser))
			}
		};

		var authenticationConfig = string.IsNullOrEmpty(options.Auth.AuthenticationConfig)
			? options.Application.Config
			: options.Auth.AuthenticationConfig;

		foreach (var plugin in pluginLoader.Load<IAuthenticationPlugin>()) {
			try {
				Logger.Information(
					"Loaded authentication plugin: {Plugin} version {Version} (Command Line: {CommandLine})",
					plugin.Name, plugin.Version, plugin.CommandLineName
				);
				
				authenticationTypeToPlugin.Add(
					plugin.CommandLineName, new(_ => plugin.GetAuthenticationProviderFactory(authenticationConfig))
				);
			}
			catch (CompositionException ex) {
				Logger.Error(ex, "Error loading authentication plugin.");
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

	public static List<IMD5ProviderFactory> GetMD5ProviderFactories(this PluginLoader pluginLoader) {
		var result = new List<IMD5ProviderFactory>();

		foreach (var plugin in pluginLoader.Load<IMD5Plugin>()) {
			try {
				Logger.Information(
					"Loaded MD5 plugin: {Plugin} version {Version} (Command Line: {CommandLine})",
					plugin.Name, plugin.Version, plugin.CommandLineName
				);
				
				result.Add(plugin.GetMD5ProviderFactory());
			}
			catch (CompositionException ex) {
				Log.Error(ex, "Error loading MD5 plugin: {Plugin}.", plugin.Name);
			}
		}

		return result;
	}
}

static class ClusterVNodeExtensions {
	public static void RegisterWebUiControllers(this ClusterVNode node, ProjectionType projectionType, bool disableAdminUi) {
		if (disableAdminUi) return;
				
		NodeSubsystems[] enabledNodeSubsystems = projectionType >= ProjectionType.System
			? [NodeSubsystems.Projections]
			: [];
			
		node.HttpService.SetupController(
			new ClusterWebUiController(node.MainQueue, enabledNodeSubsystems)
		);
	}
}
