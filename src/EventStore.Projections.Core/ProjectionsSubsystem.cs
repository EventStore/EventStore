using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.AwakeReaderService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Grpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace EventStore.Projections.Core {
	public sealed class ProjectionsSubsystem : ISubsystem, IHandle<CoreProjectionStatusMessage.Stopped> {
		private static readonly MediaTypeHeaderValue Grpc = new MediaTypeHeaderValue("application/grpc");
		private static readonly PathString ProjectionsSegment = "/event_store.grpc.projections.Projections";

		public InMemoryBus MasterMainBus {
			get { return _masterMainBus; }
		}
		private readonly int _projectionWorkerThreadCount;
		private readonly ProjectionType _runProjections;
		private readonly bool _startStandardProjections;
		private readonly TimeSpan _projectionsQueryExpiry;
		public const int VERSION = 3;

		private IQueuedHandler _masterInputQueue;
		private readonly InMemoryBus _masterMainBus;
		private InMemoryBus _masterOutputBus;
		private IDictionary<Guid, IQueuedHandler> _coreQueues;
		private Dictionary<Guid, IPublisher> _queueMap;
		private bool _subsystemStarted;

		private readonly bool _faultOutOfOrderProjections;

		public Func<IApplicationBuilder, IApplicationBuilder> Configure => builder => builder
			.UseWhen(
				context => context.Request.Path.StartsWithSegments(ProjectionsSegment),
				inner => inner
					.UseRouting()
					.UseEndpoints(endpoints => endpoints.MapGrpcService<ProjectionManagement>()));

		public Func<IServiceCollection, IServiceCollection> ConfigureServices => services =>
			services.AddSingleton(provider =>
				new ProjectionManagement(_masterInputQueue, provider.GetService<IAuthenticationProvider>()));

		public ProjectionsSubsystem(int projectionWorkerThreadCount, ProjectionType runProjections,
			bool startStandardProjections, TimeSpan projectionQueryExpiry, bool faultOutOfOrderProjections) {
			if (runProjections <= ProjectionType.System)
				_projectionWorkerThreadCount = 1;
			else
				_projectionWorkerThreadCount = projectionWorkerThreadCount;

			_runProjections = runProjections;
			_startStandardProjections = startStandardProjections;
			_projectionsQueryExpiry = projectionQueryExpiry;
			_faultOutOfOrderProjections = faultOutOfOrderProjections;
			_masterMainBus = new InMemoryBus("manager input bus");
		}

		public void Register(StandardComponents standardComponents) {
			_masterInputQueue = QueuedHandler.CreateQueuedHandler(_masterMainBus, "Projections Master",
				standardComponents.QueueStatsManager);
			_masterOutputBus = new InMemoryBus("ProjectionManagerAndCoreCoordinatorOutput");

			var projectionsStandardComponents = new ProjectionsStandardComponents(
				_projectionWorkerThreadCount,
				_runProjections,
				_masterOutputBus,
				_masterInputQueue,
				_masterMainBus, _faultOutOfOrderProjections);

			CreateAwakerService(standardComponents);
			_coreQueues =
				ProjectionCoreWorkersNode.CreateCoreWorkers(standardComponents, projectionsStandardComponents);
			_queueMap = _coreQueues.ToDictionary(v => v.Key, v => (IPublisher)v.Value);

			ProjectionManagerNode.CreateManagerService(standardComponents, projectionsStandardComponents, _queueMap,
				_projectionsQueryExpiry);
			projectionsStandardComponents.MasterMainBus.Subscribe<CoreProjectionStatusMessage.Stopped>(this);
		}

		private static void CreateAwakerService(StandardComponents standardComponents) {
			var awakeReaderService = new AwakeService();
			standardComponents.MainBus.Subscribe<StorageMessage.EventCommitted>(awakeReaderService);
			standardComponents.MainBus.Subscribe<StorageMessage.TfEofAtNonCommitRecord>(awakeReaderService);
			standardComponents.MainBus.Subscribe<AwakeServiceMessage.SubscribeAwake>(awakeReaderService);
			standardComponents.MainBus.Subscribe<AwakeServiceMessage.UnsubscribeAwake>(awakeReaderService);
		}


		public IEnumerable<Task> Start() {
			var tasks = new List<Task>();
			if (_subsystemStarted == false) {
				if (_masterInputQueue != null)
					tasks.Add(_masterInputQueue.Start());
				foreach (var queue in _coreQueues)
					tasks.Add(queue.Value.Start());
			}

			_subsystemStarted = true;
			return tasks;
		}

		public void Stop() {
			if (_subsystemStarted) {
				if (_masterInputQueue != null)
					_masterInputQueue.Stop();
				foreach (var queue in _coreQueues)
					queue.Value.Stop();
			}

			_subsystemStarted = false;
		}

		private List<string> _standardProjections = new List<string> {
			"$by_category",
			"$stream_by_category",
			"$streams",
			"$by_event_type",
			"$by_correlation_id"
		};

		public void Handle(CoreProjectionStatusMessage.Stopped message) {
			if (_startStandardProjections) {
				if (_standardProjections.Contains(message.Name)) {
					_standardProjections.Remove(message.Name);
					var envelope = new NoopEnvelope();
					_masterMainBus.Publish(new ProjectionManagementMessage.Command.Enable(envelope, message.Name,
						ProjectionManagementMessage.RunAs.System));
				}
			}
		}

		private static bool IsGrpc(HttpContext context) =>
			context.Request.Headers.TryGetValue("content-type", out var contentType) &&
			MediaTypeHeaderValue.TryParse(new StringSegment(contentType), out var contentTypeHeader) &&
			contentTypeHeader.Type == Grpc.Type &&
			contentTypeHeader.SubTypeWithoutSuffix == Grpc.SubTypeWithoutSuffix;

	}
}
