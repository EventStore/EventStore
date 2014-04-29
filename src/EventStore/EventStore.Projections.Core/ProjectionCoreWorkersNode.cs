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

namespace EventStore.Projections.Core
{
    public static class ProjectionCoreWorkersNode
    {
        public static Dictionary<Guid, QueuedHandler> CreateCoreWorkers(
            StandardComponents standardComponents,
            ProjectionsStandardComponents projectionsStandardComponents)
        {
            var coreTimeoutSchedulers =
                CreateTimeoutSchedulers(projectionsStandardComponents.ProjectionWorkerThreadCount);

            var coreQueues = new Dictionary<Guid, QueuedHandler>();
            while (coreQueues.Count < projectionsStandardComponents.ProjectionWorkerThreadCount)
            {
                var coreInputBus = new InMemoryBus("bus");
                var coreQueue = new QueuedHandler(
                    coreInputBus,
                    "Projection Core #" + coreQueues.Count,
                    groupName: "Projection Core");
                var workerId = Guid.NewGuid();
                var projectionNode = new ProjectionWorkerNode(
                    workerId,
                    standardComponents.Db,
                    coreQueue,
                    standardComponents.TimeProvider,
                    coreTimeoutSchedulers[coreQueues.Count],
                    projectionsStandardComponents.RunProjections);
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


                if (projectionsStandardComponents.RunProjections >= RunProjections.System)
                {
                    var slaveProjectionResponseWriter = projectionNode.SlaveProjectionResponseWriter;
                    coreOutput.Subscribe<PartitionMeasuredOutput>(slaveProjectionResponseWriter);
                    coreOutput.Subscribe<PartitionProcessingProgressOutput>(slaveProjectionResponseWriter);
                    coreOutput.Subscribe<PartitionProcessingResultOutput>(slaveProjectionResponseWriter);
                    coreOutput.Subscribe<ReaderSubscriptionManagement.SpoolStreamReading>(slaveProjectionResponseWriter);


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
            new ProjectionCoreCoordinator(
                projectionsStandardComponents.RunProjections,
                coreTimeoutSchedulers,
                projectionsStandardComponents.MasterOutputBus,
                new PublishEnvelope(projectionsStandardComponents.MasterInputQueue, crossThread: true)).SetupMessaging(
                    projectionsStandardComponents.MasterMainBus);
            projectionsStandardComponents.MasterMainBus.Subscribe(
                Forwarder.CreateBalancing<FeedReaderMessage.ReadPage>(coreQueues.Values.Cast<IPublisher>().ToArray()));
            return coreQueues;
        }

        public static TimeoutScheduler[] CreateTimeoutSchedulers(int count)
        {
            var timeoutSchedulers = new TimeoutScheduler[count];
            for (var i = 0; i < timeoutSchedulers.Length; i++)
                timeoutSchedulers[i] = new TimeoutScheduler();
            return timeoutSchedulers;
        }
    }
}
