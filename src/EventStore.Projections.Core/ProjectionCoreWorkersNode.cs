using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.AwakeReaderService;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.EventReaders.Feeds;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Messaging;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core {
	public static class ProjectionCoreWorkersNode {
		public static Dictionary<Guid, IQueuedHandler> CreateCoreWorkers(
			StandardComponents standardComponents,
			ProjectionsStandardComponents projectionsStandardComponents) {
			var coreTimeoutSchedulers =
				CreateTimeoutSchedulers(projectionsStandardComponents.ProjectionWorkerThreadCount);

			var coreQueues = new Dictionary<Guid, IQueuedHandler>();
			while (coreQueues.Count < projectionsStandardComponents.ProjectionWorkerThreadCount) {
				var coreInputBus = new InMemoryBus("bus");
				var coreQueue = QueuedHandler.CreateQueuedHandler(coreInputBus,
					"Projection Core #" + coreQueues.Count,
					groupName: "Projection Core");
				var workerId = Guid.NewGuid();
				var projectionNode = new ProjectionWorkerNode(
					workerId,
					standardComponents.Db,
					coreQueue,
					standardComponents.TimeProvider,
					coreTimeoutSchedulers[coreQueues.Count],
					projectionsStandardComponents.RunProjections,
					projectionsStandardComponents.FaultOutOfOrderProjections);
				projectionNode.SetupMessaging(coreInputBus);

				var forwarder = new RequestResponseQueueForwarder(
					inputQueue: coreQueue,
					externalRequestQueue: standardComponents.MainQueue);
				// forwarded messages
				var coreOutput = projectionNode.CoreOutput;
				coreOutput.Subscribe<ClientMessage.ReadEvent>(forwarder);
				coreOutput.Subscribe<ClientMessage.ReadStreamEventsBackward>(forwarder);
				coreOutput.Subscribe<ClientMessage.ReadStreamEventsForward>(forwarder);
				coreOutput.Subscribe<ClientMessage.ReadAllEventsForward>(forwarder);
				coreOutput.Subscribe<ClientMessage.WriteEvents>(forwarder);
				coreOutput.Subscribe<ClientMessage.DeleteStream>(forwarder);
				coreOutput.Subscribe<ProjectionCoreServiceMessage.SubComponentStarted>(forwarder);
				coreOutput.Subscribe<ProjectionCoreServiceMessage.SubComponentStopped>(forwarder);

				if (projectionsStandardComponents.RunProjections >= ProjectionType.System) {
					var slaveProjectionResponseWriter = projectionNode.SlaveProjectionResponseWriter;
					coreOutput.Subscribe<PartitionMeasuredOutput>(slaveProjectionResponseWriter);
					coreOutput.Subscribe<PartitionProcessingProgressOutput>(slaveProjectionResponseWriter);
					coreOutput.Subscribe<PartitionProcessingResultOutput>(slaveProjectionResponseWriter);
					coreOutput.Subscribe<ReaderSubscriptionManagement.SpoolStreamReading>(
						slaveProjectionResponseWriter);


					coreOutput.Subscribe(
						Forwarder.Create<AwakeServiceMessage.SubscribeAwake>(standardComponents.MainQueue));
					coreOutput.Subscribe(
						Forwarder.Create<AwakeServiceMessage.UnsubscribeAwake>(standardComponents.MainQueue));
				}

				coreOutput.Subscribe<TimerMessage.Schedule>(standardComponents.TimerService);


				coreOutput.Subscribe(Forwarder.Create<Message>(coreQueue)); // forward all

				coreInputBus.Subscribe(new UnwrapEnvelopeHandler());

				coreQueues.Add(workerId, coreQueue);
			}

			var queues = coreQueues.Select(v => v.Value).Cast<IPublisher>().ToArray();
			var coordinator = new ProjectionCoreCoordinator(
				projectionsStandardComponents.RunProjections,
				coreTimeoutSchedulers,
				queues,
				projectionsStandardComponents.MasterOutputBus,
				new PublishEnvelope(projectionsStandardComponents.MasterInputQueue, crossThread: true));

			coordinator.SetupMessaging(projectionsStandardComponents.MasterMainBus);
			projectionsStandardComponents.MasterMainBus.Subscribe(
				Forwarder.CreateBalancing<FeedReaderMessage.ReadPage>(coreQueues.Values.Cast<IPublisher>().ToArray()));
			return coreQueues;
		}

		public static TimeoutScheduler[] CreateTimeoutSchedulers(int count) {
			var timeoutSchedulers = new TimeoutScheduler[count];
			for (var i = 0; i < timeoutSchedulers.Length; i++)
				timeoutSchedulers[i] = new TimeoutScheduler();
			return timeoutSchedulers;
		}
	}
}
