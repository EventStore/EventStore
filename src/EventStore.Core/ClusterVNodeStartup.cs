// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Common.Configuration;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Core.Services.Transport.Grpc.Cluster;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Controllers;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Plugins;
using EventStore.Plugins.Authentication;
using EventStore.Plugins.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using MidFunc = System.Func<
	Microsoft.AspNetCore.Http.HttpContext,
	System.Func<System.Threading.Tasks.Task>,
	System.Threading.Tasks.Task
>;
using Operations = EventStore.Core.Services.Transport.Grpc.Operations;
using ClusterGossip = EventStore.Core.Services.Transport.Grpc.Cluster.Gossip;
using ClientGossip = EventStore.Core.Services.Transport.Grpc.Gossip;
using ServerFeatures = EventStore.Core.Services.Transport.Grpc.ServerFeatures;

#nullable enable
namespace EventStore.Core;

public interface IInternalStartup {
	void Configure(WebApplication app);
	void ConfigureServices(IServiceCollection services);
}

public class ClusterVNodeStartup<TStreamId> : IInternalStartup, IHandle<SystemMessage.SystemReady>,
	IHandle<SystemMessage.BecomeShuttingDown> {
	private readonly IReadOnlyList<IPlugableComponent> _plugableComponents;
	private readonly IPublisher _mainQueue;
	private readonly IPublisher _monitoringQueue;
	private readonly ISubscriber _mainBus;
	private readonly IAuthenticationProvider _authenticationProvider;
	private readonly int _maxAppendSize;
	private readonly TimeSpan _writeTimeout;
	private readonly IExpiryStrategy _expiryStrategy;
	private readonly IConfiguration _configuration;
	private readonly Trackers _trackers;
	private readonly StatusCheck _statusCheck;
	private readonly Func<IServiceCollection, IServiceCollection> _configureNodeServices;
	private readonly Action<IApplicationBuilder> _configureNode;
	private readonly Action<IApplicationBuilder> _startNode;

	private bool _ready;
	private readonly IAuthorizationProvider _authorizationProvider;
	private readonly MultiQueuedHandler _httpMessageHandler;
	private readonly string _clusterDns;

	public ClusterVNodeStartup(IReadOnlyList<IPlugableComponent> plugableComponents,
		IPublisher mainQueue,
		IPublisher monitoringQueue,
		ISubscriber mainBus,
		MultiQueuedHandler httpMessageHandler,
		IAuthenticationProvider authenticationProvider,
		IAuthorizationProvider authorizationProvider,
		int maxAppendSize,
		TimeSpan writeTimeout,
		IExpiryStrategy expiryStrategy,
		IConfiguration configuration,
		Trackers trackers,
		string clusterDns,
		Func<IServiceCollection, IServiceCollection> configureNodeServices,
		Action<IApplicationBuilder> configureNode,
		Action<IApplicationBuilder> startNode) {
		_plugableComponents = plugableComponents;
		_mainQueue = mainQueue;
		_monitoringQueue = Ensure.NotNull(monitoringQueue);
		_mainBus = Ensure.NotNull(mainBus);
		_httpMessageHandler = httpMessageHandler;
		_authenticationProvider = authenticationProvider;
		_authorizationProvider = Ensure.NotNull(authorizationProvider);
		_maxAppendSize = Ensure.Positive(maxAppendSize);
		_writeTimeout = writeTimeout;
		_expiryStrategy = expiryStrategy;
		_configuration = Ensure.NotNull(configuration);
		_trackers = trackers;
		_clusterDns = clusterDns;
		_configureNodeServices = Ensure.NotNull(configureNodeServices);
		_configureNode = Ensure.NotNull(configureNode);
		_startNode = Ensure.NotNull(startNode);
		_statusCheck = new StatusCheck(this);
	}

	public void Configure(WebApplication app) {
		_configureNode(app);

		var internalDispatcher = new InternalDispatcherEndpoint(_mainQueue, _httpMessageHandler);
		_mainBus.Subscribe(internalDispatcher);

		// UseAuthentication/UseAuthorization allow the rest of the pipeline to access auth
		// in a conventional way (e.g. with AuthorizeAttribute). The server doesn't make use
		// of this yet but plugins may. The registered authentication scheme (es auth)
		// is driven by the HttpContext.User established above
		app.UseAuthentication()
			.UseRouting()
			.UseAuthorization()
			.UseAntiforgery();

		app.Map("/health", _statusCheck.Configure);
		app.UseStaticFiles();
		// AuthenticationMiddleware uses _httpAuthenticationProviders and assigns
		// the resulting ClaimsPrinciple to HttpContext.User
		app.UseMiddleware<AuthenticationMiddleware>();

		// allow all subsystems to register their legacy controllers before calling MapLegacyHttp
		foreach (var component in _plugableComponents)
			component.ConfigureApplication(app, _configuration);

		_authenticationProvider.ConfigureEndpoints(app);
		app.MapGrpcService<PersistentSubscriptions>();
		app.MapGrpcService<Users>();
		app.MapGrpcService<Streams<TStreamId>>();
		app.MapGrpcService<ClusterGossip>();
		app.MapGrpcService<Elections>();
		app.MapGrpcService<Operations>();
		app.MapGrpcService<ClientGossip>();
		app.MapGrpcService<Monitoring>();
		app.MapGrpcService<ServerFeatures>();

		app.MapGetInfo();

		// enable redaction service on unix sockets only
		app.MapGrpcService<Redaction>().AddEndpointFilter(async (c, next) => {
			if (!c.HttpContext.IsUnixSocketConnection())
				return Results.BadRequest("Redaction is only available via Unix Sockets");
			return await next(c).ConfigureAwait(false);
		});
		// Select an appropriate controller action and codec.
		//    Success -> Add InternalContext (HttpEntityManager, urimatch, ...) to HttpContext
		//    Fail -> Pipeline terminated with response.
		app.UseMiddleware<KestrelToInternalBridgeMiddleware>();

		// Looks up the InternalContext to perform the check.
		// Terminal if auth check is not successful.
		app.UseMiddleware<AuthorizationMiddleware>();

		// Open telemetry currently guarded by our custom authz for consistency with stats
		app.UseOpenTelemetryPrometheusScrapingEndpoint();

		// Internal dispatcher looks up the InternalContext to call the appropriate controller
		app.Use((ctx, next) => internalDispatcher.InvokeAsync(ctx, next));

		_startNode(app);
	}

	public void ConfigureServices(IServiceCollection services) {
		var metricsConfiguration = MetricsConfiguration.Get(_configuration);

		services.AddRouting();
		services.AddAuthentication(o => o.AddScheme<EventStoreAuthenticationHandler>("es auth", displayName: null));
		services.AddAuthorization();
		services.AddSingleton(_authenticationProvider)
			.AddSingleton(_authorizationProvider)
			.AddSingleton(_mainBus)
			.AddSingleton(_mainQueue)
			// .AddSingleton(_httpService)
			.AddSingleton<AuthenticationMiddleware>()
			.AddSingleton<AuthorizationMiddleware>()
			.AddSingleton(new Streams<TStreamId>(_mainQueue, _maxAppendSize, _writeTimeout, _expiryStrategy, _trackers.GrpcTrackers, _authorizationProvider))
			.AddSingleton<PersistentSubscriptions>()
			.AddSingleton<Users>()
			.AddSingleton<Operations>()
			.AddSingleton(new ClusterGossip(_mainQueue, _authorizationProvider, _clusterDns,
				updateTracker: _trackers.GossipTrackers.ProcessingPushFromPeer,
				readTracker: _trackers.GossipTrackers.ProcessingRequestFromPeer))
			.AddSingleton(new Elections(_mainQueue, _authorizationProvider, _clusterDns))
			.AddSingleton(new ClientGossip(_mainQueue, _authorizationProvider, _trackers.GossipTrackers.ProcessingRequestFromGrpcClient))
			.AddSingleton(new Monitoring(_monitoringQueue))
			.AddSingleton<Redaction>()
			.AddSingleton<ServerFeatures>();

		// OpenTelemetry
		services.AddOpenTelemetry()
			.WithMetrics(meterOptions => meterOptions
				.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("eventstore"))
				.AddMeter(metricsConfiguration.Meters)
				.AddView(i => {
					if (i.Name == MetricsBootstrapper.LogicalChunkReadDistributionName)
						// 20 buckets, 0, 1, 2, 4, 8, ...
						return new ExplicitBucketHistogramConfiguration {
							Boundaries = [
								0,
								.. Enumerable.Range(0, count: 19).Select(x => 1 << x)
							]
						};
					if (i.Name.StartsWith("eventstore-") &&
					    i.Name.EndsWith("-latency") &&
					    i.Unit == "seconds")
						return new ExplicitBucketHistogramConfiguration {
							Boundaries = [
								0.001, //    1 ms
								0.005, //    5 ms
								0.01, //   10 ms
								0.05, //   50 ms
								0.1, //  100 ms
								0.5, //  500 ms
								1, // 1000 ms
								5, // 5000 ms
							]
						};
					if (i.Name.StartsWith("eventstore-") && i.Unit == "seconds")
						return new ExplicitBucketHistogramConfiguration {
							Boundaries = [
								0.000_001, // 1 microsecond
								0.000_01,
								0.000_1,
								0.001, // 1 millisecond
								0.01,
								0.1,
								1, // 1 second
								10,
							]
						};
					return null;
				})
				.AddPrometheusExporter(options => options.ScrapeResponseCacheDurationMilliseconds = 1000));

		// gRPC
		services.AddSingleton<RetryInterceptor>()
			.AddGrpc(options => options.Interceptors.Add<RetryInterceptor>())
			.AddServiceOptions<Streams<TStreamId>>(options => options.MaxReceiveMessageSize = TFConsts.EffectiveMaxLogRecordSize);

		_configureNodeServices(services);

		foreach (var component in _plugableComponents)
			component.ConfigureServices(services, _configuration);
	}

	public void Handle(SystemMessage.SystemReady _) => _ready = true;

	public void Handle(SystemMessage.BecomeShuttingDown _) => _ready = false;

	private class StatusCheck(ClusterVNodeStartup<TStreamId> startup) {
		private readonly ClusterVNodeStartup<TStreamId> _startup = startup ?? throw new ArgumentNullException(nameof(startup));
		private readonly int _livecode = 204;

		public void Configure(IApplicationBuilder builder) =>
			builder.Use(GetAndHeadOnly)
				.UseRouter(router => router.MapMiddlewareGet("live", inner => inner.Use(Live)));

		private MidFunc Live => (context, next) => {
			if (_startup._ready) {
				if (context.Request.Query.TryGetValue("liveCode", out var expected) &&
				    int.TryParse(expected, out var statusCode)) {
					context.Response.StatusCode = statusCode;
				} else {
					context.Response.StatusCode = _livecode;
				}
			} else {
				context.Response.StatusCode = 503;
			}

			return Task.CompletedTask;
		};

		private static MidFunc GetAndHeadOnly => (context, next) => {
			switch (context.Request.Method) {
				case "HEAD":
					context.Request.Method = "GET";
					return next();
				case "GET":
					return next();
				default:
					context.Response.StatusCode = 405;
					return Task.CompletedTask;
			}
		};
	}
}
