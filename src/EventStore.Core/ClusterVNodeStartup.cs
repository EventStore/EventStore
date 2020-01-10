using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Cluster.Settings;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.Transport.Grpc;
using EventStore.Core.Services.Transport.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using MidFunc = System.Func<
	Microsoft.AspNetCore.Http.HttpContext,
	System.Func<System.Threading.Tasks.Task>,
	System.Threading.Tasks.Task
>;

namespace EventStore.Core {
	public class ClusterVNodeStartup : IStartup, IHandle<SystemMessage.SystemReady>,
		IHandle<SystemMessage.BecomeShuttingDown> {
		private static readonly PathString PersistentSegment =
			"/event_store.grpc.persistent_subscriptions.PersistentSubscriptions";

		private static readonly PathString StreamsSegment = "/event_store.grpc.streams.Streams";
		private static readonly PathString UsersSegment = "/event_store.grpc.users.Users";

		private readonly ISubsystem[] _subsystems;
		private readonly IQueuedHandler _mainQueue;
		private readonly IAuthenticationProvider _internalAuthenticationProvider;
		private readonly IReadIndex _readIndex;
		private readonly ClusterVNodeSettings _vNodeSettings;
		private readonly KestrelHttpService _externalHttpService;
		private readonly KestrelHttpService _internalHttpService;
		private readonly StatusCheck _statusCheck;

		private bool _ready;

		public ClusterVNodeStartup(
			ISubsystem[] subsystems,
			IQueuedHandler mainQueue,
			IAuthenticationProvider internalAuthenticationProvider,
			IReadIndex readIndex,
			ClusterVNodeSettings vNodeSettings,
			KestrelHttpService externalHttpService,
			KestrelHttpService internalHttpService = null) {
			if (subsystems == null) {
				throw new ArgumentNullException(nameof(subsystems));
			}

			if (mainQueue == null) {
				throw new ArgumentNullException(nameof(mainQueue));
			}

			if (internalAuthenticationProvider == null) {
				throw new ArgumentNullException(nameof(internalAuthenticationProvider));
			}

			if (readIndex == null) {
				throw new ArgumentNullException(nameof(readIndex));
			}

			if (vNodeSettings == null) {
				throw new ArgumentNullException(nameof(vNodeSettings));
			}

			if (externalHttpService == null) {
				throw new ArgumentNullException(nameof(externalHttpService));
			}

			_subsystems = subsystems;
			_mainQueue = mainQueue;
			_internalAuthenticationProvider = internalAuthenticationProvider;
			_readIndex = readIndex;
			_vNodeSettings = vNodeSettings;
			_externalHttpService = externalHttpService;
			_internalHttpService = internalHttpService;

			_statusCheck = new StatusCheck(this);
		}

		public void Configure(IApplicationBuilder app) {
			app.Map("/health", _statusCheck.Configure);
			_subsystems
				.Aggregate(app
						.UseWhen(context => context.Request.Path.StartsWithSegments(PersistentSegment),
							inner => inner.UseRouting().UseEndpoints(endpoint =>
								endpoint.MapGrpcService<PersistentSubscriptions>()))
						.UseWhen(context => context.Request.Path.StartsWithSegments(UsersSegment),
							inner => inner.UseRouting().UseEndpoints(endpoint =>
								endpoint.MapGrpcService<Users>()))
						.UseWhen(context => context.Request.Path.StartsWithSegments(StreamsSegment),
							inner => inner.UseRouting().UseEndpoints(endpoint =>
								endpoint.MapGrpcService<Streams>())),
					(b, subsystem) => subsystem.Configure(b))
				.Use(_externalHttpService.MidFunc);

			if (_internalHttpService != null) {
				app.Use(_internalHttpService.MidFunc);
			}
		}

		public IServiceProvider ConfigureServices(IServiceCollection services) =>
			_subsystems
				.Aggregate(services
						.AddRouting()
						.AddSingleton(_internalAuthenticationProvider)
						.AddSingleton(_readIndex)
						.AddSingleton(new Streams(_mainQueue, _internalAuthenticationProvider, _readIndex,
							_vNodeSettings.MaxAppendSize))
						.AddSingleton(new PersistentSubscriptions(_mainQueue, _internalAuthenticationProvider))
						.AddSingleton(new Users(_mainQueue, _internalAuthenticationProvider))
						.AddGrpc().Services,
					(s, subsystem) => subsystem.ConfigureServices(s))
				.BuildServiceProvider();

		public void Handle(SystemMessage.SystemReady message) => _ready = true;

		public void Handle(SystemMessage.BecomeShuttingDown message) => _ready = false;

		private class StatusCheck {
			private readonly ClusterVNodeStartup _startup;

			public StatusCheck(ClusterVNodeStartup startup) {
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
