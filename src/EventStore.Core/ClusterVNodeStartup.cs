using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Core.Services.Transport.Grpc.Cluster;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Plugins.Authentication;
using EventStore.Plugins.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using MidFunc = System.Func<
	Microsoft.AspNetCore.Http.HttpContext,
	System.Func<System.Threading.Tasks.Task>,
	System.Threading.Tasks.Task
>;
using ElectionsService = EventStore.Core.Services.Transport.Grpc.Cluster.Elections;
using Operations = EventStore.Core.Services.Transport.Grpc.Operations;
using ClusterGossip = EventStore.Core.Services.Transport.Grpc.Cluster.Gossip;
using ClientGossip = EventStore.Core.Services.Transport.Grpc.Gossip;

namespace EventStore.Core {
	public class ClusterVNodeStartup<TStreamId> : IStartup, IHandle<SystemMessage.SystemReady>,
		IHandle<SystemMessage.BecomeShuttingDown> {

		private readonly ISubsystem[] _subsystems;
		private readonly IPublisher _mainQueue;
		private readonly IPublisher _monitoringQueue;
		private readonly ISubscriber _mainBus;
		private readonly IAuthenticationProvider _authenticationProvider;
		private readonly IReadOnlyList<IHttpAuthenticationProvider> _httpAuthenticationProviders;
		private readonly IReadIndex<TStreamId> _readIndex;
		private readonly int _maxAppendSize;
		private readonly KestrelHttpService _httpService;
		private readonly StatusCheck _statusCheck;

		private bool _ready;
		private readonly IAuthorizationProvider _authorizationProvider;
		private readonly MultiQueuedHandler _httpMessageHandler;

		public ClusterVNodeStartup(
			ISubsystem[] subsystems,
			IPublisher mainQueue,
			IPublisher monitoringQueue,
			ISubscriber mainBus,
			MultiQueuedHandler httpMessageHandler,
			IAuthenticationProvider authenticationProvider,
			IReadOnlyList<IHttpAuthenticationProvider> httpAuthenticationProviders,
			IAuthorizationProvider authorizationProvider,
			IReadIndex<TStreamId> readIndex,
			int maxAppendSize,
			KestrelHttpService httpService) {
			if (subsystems == null) {
				throw new ArgumentNullException(nameof(subsystems));
			}

			if (mainQueue == null) {
				throw new ArgumentNullException(nameof(mainQueue));
			}

			if (httpAuthenticationProviders == null) {
				throw new ArgumentNullException(nameof(httpAuthenticationProviders));
			}

			if(authorizationProvider == null)
				throw new ArgumentNullException(nameof(authorizationProvider));

			if (readIndex == null) {
				throw new ArgumentNullException(nameof(readIndex));
			}

			Ensure.Positive(maxAppendSize, nameof(maxAppendSize));

			if (httpService == null) {
				throw new ArgumentNullException(nameof(httpService));
			}

			if (mainBus == null) {
				throw new ArgumentNullException(nameof(mainBus));
			}

			if (monitoringQueue == null) {
				throw new ArgumentNullException(nameof(monitoringQueue));
			}
			_subsystems = subsystems;
			_mainQueue = mainQueue;
			_monitoringQueue = monitoringQueue;
			_mainBus = mainBus;
			_httpMessageHandler = httpMessageHandler;
			_authenticationProvider = authenticationProvider;
			_httpAuthenticationProviders = httpAuthenticationProviders;
			_authorizationProvider = authorizationProvider;
			_readIndex = readIndex;
			_maxAppendSize = maxAppendSize;
			_httpService = httpService;

			_statusCheck = new StatusCheck(this);
		}

		public void Configure(IApplicationBuilder app) {
			var grpc = new MediaTypeHeaderValue("application/grpc");
			var internalDispatcher = new InternalDispatcherEndpoint(_mainQueue, _httpMessageHandler);
			_mainBus.Subscribe(internalDispatcher);
			app.Map("/health", _statusCheck.Configure)
				.UseMiddleware<AuthenticationMiddleware>()
				.UseRouting()
				.UseWhen(ctx => ctx.Request.Method == HttpMethods.Options 
				                && !(ctx.Request.GetTypedHeaders().ContentType?.IsSubsetOf(grpc)).GetValueOrDefault(false),
					b => b
						.UseMiddleware<KestrelToInternalBridgeMiddleware>()
				)
				.UseEndpoints(ep => _authenticationProvider.ConfigureEndpoints(ep))
				.UseWhen(ctx => !(ctx.Request.GetTypedHeaders().ContentType?.IsSubsetOf(grpc)).GetValueOrDefault(false),
					b => b
						.UseMiddleware<KestrelToInternalBridgeMiddleware>()
						.UseMiddleware<AuthorizationMiddleware>()
						.UseLegacyHttp(internalDispatcher.InvokeAsync, _httpService)
				)
				.UseEndpoints(ep => ep.MapGrpcService<PersistentSubscriptions>())
				.UseEndpoints(ep => ep.MapGrpcService<Users>())
				.UseEndpoints(ep => ep.MapGrpcService<Streams<TStreamId>>())
				.UseEndpoints(ep => ep.MapGrpcService<ClusterGossip>())
				.UseEndpoints(ep => ep.MapGrpcService<Elections>())
				.UseEndpoints(ep => ep.MapGrpcService<Operations>())
				.UseEndpoints(ep => ep.MapGrpcService<ClientGossip>())
				.UseEndpoints(ep => ep.MapGrpcService<Monitoring>());

			_subsystems.Aggregate(app, (b, subsystem) => subsystem.Configure(b));
		}

		IServiceProvider IStartup.ConfigureServices(IServiceCollection services) => ConfigureServices(services)
			.BuildServiceProvider();

		public IServiceCollection ConfigureServices(IServiceCollection services) =>
			_subsystems
				.Aggregate(services
						.AddRouting()
						.AddSingleton(_httpAuthenticationProviders)
						.AddSingleton(_authenticationProvider)
						.AddSingleton(_authorizationProvider)
						.AddSingleton<AuthenticationMiddleware>()
						.AddSingleton<AuthorizationMiddleware>()
						.AddSingleton(new KestrelToInternalBridgeMiddleware(_httpService.UriRouter, _httpService.LogHttpRequests, _httpService.AdvertiseAsHost, _httpService.AdvertiseAsPort))
						.AddSingleton(_readIndex)
						.AddSingleton(new Streams<TStreamId>(_mainQueue, _readIndex, _maxAppendSize, _authorizationProvider))
						.AddSingleton(new PersistentSubscriptions(_mainQueue, _authorizationProvider))
						.AddSingleton(new Users(_mainQueue, _authorizationProvider))
						.AddSingleton(new Operations(_mainQueue, _authorizationProvider))
						.AddSingleton(new ClusterGossip(_mainQueue, _authorizationProvider))
						.AddSingleton(new Elections(_mainQueue, _authorizationProvider))
						.AddSingleton(new ClientGossip(_mainQueue, _authorizationProvider))
						.AddSingleton(new Monitoring(_monitoringQueue))
						.AddGrpc()
						.AddServiceOptions<Streams<TStreamId>>(options =>
							options.MaxReceiveMessageSize = TFConsts.EffectiveMaxLogRecordSize)
						.Services,
					(s, subsystem) => subsystem.ConfigureServices(s));

		public void Handle(SystemMessage.SystemReady _) => _ready = true;

		public void Handle(SystemMessage.BecomeShuttingDown _) => _ready = false;

		private class StatusCheck {
			private readonly ClusterVNodeStartup<TStreamId> _startup;

			public StatusCheck(ClusterVNodeStartup<TStreamId> startup) {
				if (startup == null) {
					throw new ArgumentNullException(nameof(startup));
				}

				_startup = startup;
			}

			public void Configure(IApplicationBuilder builder) =>
				builder.Use(GetAndHeadOnly)
					.UseRouter(router => router
						.MapMiddlewareGet("live", inner => inner.Use(Live)));

			private MidFunc Live => (context, next) => {
				context.Response.StatusCode = _startup._ready ? 204 : 503;
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
}
