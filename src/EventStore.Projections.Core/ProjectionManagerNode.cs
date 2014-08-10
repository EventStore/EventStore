using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.AwakeReaderService;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messaging;
using EventStore.Projections.Core.Services.Http;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core
{
    public class ProjectionManagerNode
    {
        public static void CreateManagerService(
            StandardComponents standardComponents,
            ProjectionsStandardComponents projectionsStandardComponents,
            IDictionary<Guid, IPublisher> queues)
        {
            QueuedHandler inputQueue = projectionsStandardComponents.MasterInputQueue;
            InMemoryBus outputBus = projectionsStandardComponents.MasterOutputBus;
            var ioDispatcher = new IODispatcher(outputBus, new PublishEnvelope(inputQueue));

            var projectionsController = new ProjectionsController(
                standardComponents.HttpForwarder,
                inputQueue,
                standardComponents.NetworkSendService);

            var forwarder = new RequestResponseQueueForwarder(
                inputQueue: projectionsStandardComponents.MasterInputQueue,
                externalRequestQueue: standardComponents.MainQueue);

            foreach (var httpService in standardComponents.HttpServices)
            {
                httpService.SetupController(projectionsController);
            }

            var commandWriter = new MultiStreamMessageWriter(ioDispatcher);
            var projectionManagerCommandWriter = new ProjectionManagerCommandWriter(commandWriter);
            var projectionManagerResponseReader = new ProjectionManagerResponseReader(outputBus, ioDispatcher);

            var projectionManager = new ProjectionManager(
                inputQueue,
                outputBus,
                queues,
                new RealTimeProvider(),
                projectionsStandardComponents.RunProjections);

            SubscribeMainBus(
                projectionsStandardComponents.MasterMainBus,
                projectionManager,
                projectionsStandardComponents.RunProjections,
                projectionManagerResponseReader,
                ioDispatcher,
                projectionManagerCommandWriter);


            SubscribeOutputBus(standardComponents, projectionsStandardComponents, forwarder);
        }

        private static void SubscribeMainBus(
            ISubscriber mainBus,
            ProjectionManager projectionManager,
            ProjectionType runProjections,
            ProjectionManagerResponseReader projectionManagerResponseReader,
            IODispatcher ioDispatcher,
            ProjectionManagerCommandWriter projectionManagerCommadnWriter)
        {
            mainBus.Subscribe<SystemMessage.StateChangeMessage>(projectionManager);
            if (runProjections >= ProjectionType.System)
            {
                mainBus.Subscribe<ProjectionManagementMessage.Command.Post>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Command.UpdateQuery>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Command.GetQuery>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Command.Delete>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Command.GetStatistics>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Command.GetState>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Command.GetResult>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Command.Disable>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Command.Enable>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Command.Abort>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Command.SetRunAs>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Command.Reset>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Command.StartSlaveProjections>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.RegisterSystemProjection>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Internal.CleanupExpired>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Internal.Deleted>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.RegisterSystemProjection>(projectionManager);
                mainBus.Subscribe<CoreProjectionStatusMessage.Started>(projectionManager);
                mainBus.Subscribe<CoreProjectionStatusMessage.Stopped>(projectionManager);
                mainBus.Subscribe<CoreProjectionStatusMessage.Faulted>(projectionManager);
                mainBus.Subscribe<CoreProjectionStatusMessage.Prepared>(projectionManager);
                mainBus.Subscribe<CoreProjectionStatusMessage.StateReport>(projectionManager);
                mainBus.Subscribe<CoreProjectionStatusMessage.ResultReport>(projectionManager);
                mainBus.Subscribe<CoreProjectionStatusMessage.StatisticsReport>(projectionManager);
                mainBus.Subscribe<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>(projectionManager);
                mainBus.Subscribe<CoreProjectionStatusMessage.ProjectionWorkerStarted>(projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.ReaderReady>(projectionManager);
//                mainBus.Subscribe<PartitionProcessingResultBase>(_projectionManagerMessageDispatcher);
//                mainBus.Subscribe<ReaderSubscriptionManagement.SpoolStreamReading>(_projectionManagerMessageDispatcher);
                mainBus.Subscribe<ProjectionManagementMessage.Starting>(projectionManagerResponseReader);
            }
            mainBus.Subscribe<ClientMessage.WriteEventsCompleted>(projectionManager);
            mainBus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(projectionManager);

            mainBus.Subscribe(ioDispatcher.Awaker);
            mainBus.Subscribe(ioDispatcher.BackwardReader);
            mainBus.Subscribe(ioDispatcher.ForwardReader);
            mainBus.Subscribe(ioDispatcher.StreamDeleter);
            mainBus.Subscribe(ioDispatcher.Writer);
            mainBus.Subscribe(ioDispatcher);

            mainBus.Subscribe<CoreProjectionManagementMessage.CreatePrepared>(projectionManagerCommadnWriter);
            mainBus.Subscribe<CoreProjectionManagementMessage.CreateAndPrepare>(projectionManagerCommadnWriter);
            mainBus.Subscribe<CoreProjectionManagementMessage.CreateAndPrepareSlave>(projectionManagerCommadnWriter);
            mainBus.Subscribe<CoreProjectionManagementMessage.LoadStopped>(projectionManagerCommadnWriter);
            mainBus.Subscribe<CoreProjectionManagementMessage.Start>(projectionManagerCommadnWriter);
            mainBus.Subscribe<CoreProjectionManagementMessage.Stop>(projectionManagerCommadnWriter);
            mainBus.Subscribe<CoreProjectionManagementMessage.Kill>(projectionManagerCommadnWriter);
            mainBus.Subscribe<CoreProjectionManagementMessage.Dispose>(projectionManagerCommadnWriter);
            mainBus.Subscribe<CoreProjectionManagementMessage.GetState>(projectionManagerCommadnWriter);
            mainBus.Subscribe<CoreProjectionManagementMessage.GetResult>(projectionManagerCommadnWriter);
            mainBus.Subscribe<ProjectionManagementMessage.SlaveProjectionsStarted>(projectionManagerCommadnWriter);
        }

        private static void SubscribeOutputBus(
            StandardComponents standardComponents,
            ProjectionsStandardComponents projectionsStandardComponents,
            RequestResponseQueueForwarder forwarder)
        {
            var managerOutput = projectionsStandardComponents.MasterOutputBus;
            managerOutput.Subscribe<ClientMessage.ReadEvent>(forwarder);
            managerOutput.Subscribe<ClientMessage.ReadStreamEventsBackward>(forwarder);
            managerOutput.Subscribe<ClientMessage.ReadStreamEventsForward>(forwarder);
            managerOutput.Subscribe<ClientMessage.WriteEvents>(forwarder);
            managerOutput.Subscribe(
                Forwarder.Create<ProjectionManagementMessage.RequestSystemProjections>(standardComponents.MainQueue));
            managerOutput.Subscribe(Forwarder.Create<Message>(projectionsStandardComponents.MasterInputQueue));

            managerOutput.Subscribe<TimerMessage.Schedule>(standardComponents.TimerService);
            managerOutput.Subscribe(Forwarder.Create<AwakeServiceMessage.SubscribeAwake>(standardComponents.MainQueue));
            managerOutput.Subscribe(
                Forwarder.Create<AwakeServiceMessage.UnsubscribeAwake>(standardComponents.MainQueue));

            // self forward all

            standardComponents.MainBus.Subscribe(
                Forwarder.Create<SystemMessage.StateChangeMessage>(projectionsStandardComponents.MasterInputQueue));
            projectionsStandardComponents.MasterMainBus.Subscribe(new UnwrapEnvelopeHandler());
        }
    }
}
