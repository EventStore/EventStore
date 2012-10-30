// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Collections.Generic;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProjectionCoreService : IHandle<ProjectionMessage.CoreService.Start>,
                                         IHandle<ProjectionMessage.CoreService.Stop>,
                                         IHandle<ProjectionMessage.CoreService.Tick>,
                                         IHandle<ProjectionMessage.Projections.SubscribeProjection>,
                                         IHandle<ProjectionMessage.Projections.UnsubscribeProjection>,
                                         IHandle<ProjectionMessage.Projections.PauseProjectionSubscription>,
                                         IHandle<ProjectionMessage.Projections.ResumeProjectionSubscription>,
                                         IHandle<ProjectionMessage.Projections.CommittedEventDistributed>,
                                         IHandle<ProjectionMessage.CoreService.Management.Create>,
                                         IHandle<ProjectionMessage.CoreService.Management.Dispose>,
                                         IHandle<ProjectionMessage.Projections.Management.Start>,
                                         IHandle<ProjectionMessage.Projections.Management.Stop>,
                                         IHandle<ProjectionMessage.Projections.Management.GetState>,
                                         IHandle<ProjectionMessage.Projections.Management.UpdateStatistics>,
                                         IHandle<ClientMessage.ReadStreamEventsForwardCompleted>,
                                         IHandle<ClientMessage.ReadAllEventsForwardCompleted>,
                                         IHandle<ClientMessage.ReadStreamEventsBackwardCompleted>,
                                         IHandle<ClientMessage.WriteEventsCompleted>


    {
        private readonly IPublisher _publisher;
        private readonly IPublisher _inputQueue;
        private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionCoreService>();

        private bool _stopped = true;

        private readonly ICheckpoint _writerCheckpoint;

        private readonly Dictionary<Guid, ProjectionSubscription> _subscriptions =
            new Dictionary<Guid, ProjectionSubscription>();

        private readonly Dictionary<Guid, CoreProjection> _projections = new Dictionary<Guid, CoreProjection>();

        private readonly Dictionary<Guid, EventDistributionPoint> _distributionPoints =
            new Dictionary<Guid, EventDistributionPoint>();

        private readonly Dictionary<Guid, Guid> _projectionDistributionPoints = new Dictionary<Guid, Guid>();
        private readonly Dictionary<Guid, Guid> _distributionPointSubscriptions = new Dictionary<Guid, Guid>();
        private readonly HashSet<Guid> _pausedProjections = new HashSet<Guid>();
        private readonly HeadingEventDistributionPoint _headingEventDistributionPoint;
        private TransactionFileReaderEventDistributionPoint _headDistributionPoint;

        private readonly
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
            _readDispatcher;

        private readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>
            _writeDispatcher;


        public ProjectionCoreService(
            IPublisher publisher, IPublisher inputQueue, int eventCacheSize, ICheckpoint writerCheckpoint)
        {
            _publisher = publisher;
            _inputQueue = inputQueue;
            _headingEventDistributionPoint = new HeadingEventDistributionPoint(eventCacheSize);
            _writerCheckpoint = writerCheckpoint;
            _readDispatcher =
                new RequestResponseDispatcher
                    <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>(
                    _publisher, v => v.CorrelationId, v => v.CorrelationId, new PublishEnvelope(_inputQueue));
            _writeDispatcher =
                new RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>(
                    _publisher, v => v.CorrelationId, v => v.CorrelationId, new PublishEnvelope(_inputQueue));
        }

        public void Handle(ProjectionMessage.CoreService.Start message)
        {
            //TODO: do we need to clear subscribed projections here?
            //TODO: do we need to clear subscribed distribution points here?
            _stopped = false;
            var distibutionPointCorrelationId = Guid.NewGuid();
            _headDistributionPoint = new TransactionFileReaderEventDistributionPoint(
                _publisher, distibutionPointCorrelationId, new EventPosition(_writerCheckpoint.Read(), -1),
                deliverEndOfTFPosition: false);
            _distributionPoints.Add(distibutionPointCorrelationId, _headDistributionPoint);
            _headingEventDistributionPoint.Start(distibutionPointCorrelationId, _headDistributionPoint);
            //NOTE: writing any event to avoid empty database which we don not handle properly
            // and write it after startAtCurrent to fill buffer
            _publisher.Publish(
                new ClientMessage.WriteEvents(
                    Guid.NewGuid(), new NoopEnvelope(), RoutingStrategy.AllowForwarding, "$temp", ExpectedVersion.Any,
                    new Event(Guid.NewGuid(), "Starting", false, new byte[0], new byte[0])));
        }

        public void Handle(ProjectionMessage.CoreService.Stop message)
        {
            _headingEventDistributionPoint.Stop();
            _stopped = true;
        }

        public void Handle(ProjectionMessage.Projections.PauseProjectionSubscription message)
        {
            if (!_pausedProjections.Add(message.CorrelationId))
                throw new InvalidOperationException("Already paused projection");
            var projectionSubscription = _subscriptions[message.CorrelationId];
            var distributionPointId = _projectionDistributionPoints[message.CorrelationId];
            if (distributionPointId == Guid.Empty) // head
            {
                _projectionDistributionPoints.Remove(message.CorrelationId);
                _headingEventDistributionPoint.Unsubscribe(message.CorrelationId);
                var forkedDistributionPointId = Guid.NewGuid();
                var forkedDistributionPoint = projectionSubscription.CreatePausedEventDistributionPoint(
                    _publisher, forkedDistributionPointId);
                _projectionDistributionPoints.Add(message.CorrelationId, forkedDistributionPointId);
                _distributionPointSubscriptions.Add(forkedDistributionPointId, message.CorrelationId);
                _distributionPoints.Add(forkedDistributionPointId, forkedDistributionPoint);
            }
            else
            {
                _distributionPoints[distributionPointId].Pause();
            }
        }

        public void Handle(ProjectionMessage.Projections.ResumeProjectionSubscription message)
        {
            if (!_pausedProjections.Remove(message.CorrelationId))
                throw new InvalidOperationException("Not a paused projection");
            var distributionPoint = _projectionDistributionPoints[message.CorrelationId];
            _distributionPoints[distributionPoint].Resume();
        }

        public void Handle(ProjectionMessage.Projections.SubscribeProjection message)
        {
            if (_stopped)
                return;

            var fromCheckpointTag = message.FromPosition;
            var projectionSubscription = new ProjectionSubscription(
                message.CorrelationId, fromCheckpointTag, message.Subscriber, message.Subscriber, message.Subscriber,
                message.CheckpointStrategy, message.CheckpointUnhandledBytesThreshold);
            _subscriptions.Add(message.CorrelationId, projectionSubscription);

            var distibutionPointCorrelationId = Guid.NewGuid();
            var eventDistributionPoint = projectionSubscription.CreatePausedEventDistributionPoint(
                _publisher, distibutionPointCorrelationId);
            _logger.Trace(
                "The '{0}' projection subscribed to the '{1}' distribution point", message.CorrelationId,
                distibutionPointCorrelationId);
            _distributionPoints.Add(distibutionPointCorrelationId, eventDistributionPoint);
            _projectionDistributionPoints.Add(message.CorrelationId, distibutionPointCorrelationId);
            _distributionPointSubscriptions.Add(distibutionPointCorrelationId, message.CorrelationId);
            eventDistributionPoint.Resume();
        }

        public void Handle(ProjectionMessage.Projections.UnsubscribeProjection message)
        {
            if (!_pausedProjections.Contains(message.CorrelationId))
                Handle(new ProjectionMessage.Projections.PauseProjectionSubscription(message.CorrelationId));
            var distributionPointId = _projectionDistributionPoints[message.CorrelationId];
            if (distributionPointId != Guid.Empty)
            {
                //TODO: test it
                _distributionPoints.Remove(distributionPointId);
                _distributionPointSubscriptions.Remove(distributionPointId);
                _logger.Trace(
                    "The '{0}' projection has unsubscribed from the '{1}' distribution point", message.CorrelationId,
                    distributionPointId);
            }

            _pausedProjections.Remove(message.CorrelationId);
            _projectionDistributionPoints.Remove(message.CorrelationId);
            _subscriptions.Remove(message.CorrelationId);
        }

        public void Handle(ProjectionMessage.CoreService.Tick message)
        {
            message.Action();
        }

        public void Handle(ClientMessage.ReadStreamEventsForwardCompleted message)
        {
            EventDistributionPoint distributionPoint;
            if (_distributionPoints.TryGetValue(message.CorrelationId, out distributionPoint))
                distributionPoint.Handle(message);
        }

        public void Handle(ClientMessage.ReadAllEventsForwardCompleted message)
        {
            EventDistributionPoint distributionPoint;
            if (_distributionPoints.TryGetValue(message.CorrelationId, out distributionPoint))
                distributionPoint.Handle(message);
        }

        public void Handle(ProjectionMessage.Projections.CommittedEventDistributed message)
        {
            Guid projectionId;
            if (_stopped)
                return;
            if (_headingEventDistributionPoint.Handle(message))
                return;
            if (!_distributionPointSubscriptions.TryGetValue(message.CorrelationId, out projectionId))
                return; // unsubscribed
            if (TrySubscribeHeadingDistributionPoint(message, projectionId))
                return;
            if (message.Data != null) // means notification about the end of the stream/source
                _subscriptions[projectionId].Handle(message);
        }

        private bool TrySubscribeHeadingDistributionPoint(
            ProjectionMessage.Projections.CommittedEventDistributed message, Guid projectionId)
        {
            if (_pausedProjections.Contains(projectionId))
                return false;

            var projectionSubscription = _subscriptions[projectionId];

            if (message.SafeTransactionFileReaderJoinPosition == null
                || !_headingEventDistributionPoint.TrySubscribe(
                    projectionId, projectionSubscription, message.SafeTransactionFileReaderJoinPosition.Value))
                return false;

            if (message.Data == null)
            {
                _logger.Trace(
                    "The '{0}' is subscribing to the heading distribution point with TF-EOF marker event at '{1}'",
                    projectionId, message.Position);
            }

            Guid distributionPointId = message.CorrelationId;
            _distributionPoints[distributionPointId].Dispose();
            _distributionPoints.Remove(distributionPointId);
            _distributionPointSubscriptions.Remove(distributionPointId);
            _projectionDistributionPoints[projectionId] = Guid.Empty;
            return true;
        }

        public void Handle(ProjectionMessage.CoreService.Management.Create message)
        {
            try
            {
                //TODO: factory method can throw!
                IProjectionStateHandler stateHandler = message.HandlerFactory();
                // constructor can fail if wrong source defintion
                //TODO: revise it
                var projection = new CoreProjection(
                    message.Name, message.CorrelationId, _publisher, stateHandler, message.Config, _readDispatcher,
                    _writeDispatcher, _logger);
                _projections.Add(message.CorrelationId, projection);
            }
            catch (Exception ex)
            {
                message.Envelope.ReplyWith(
                    new ProjectionMessage.Projections.StatusReport.Faulted(message.CorrelationId, ex.Message));
            }
        }

        public void Handle(ProjectionMessage.CoreService.Management.Dispose message)
        {
            CoreProjection projection;
            if (_projections.TryGetValue(message.CorrelationId, out projection))
            {
                _projections.Remove(message.CorrelationId);
                projection.Dispose();
            }
        }

        public void Handle(ProjectionMessage.Projections.Management.Start message)
        {
            var projection = _projections[message.CorrelationId];
            projection.Start();
        }

        public void Handle(ProjectionMessage.Projections.Management.Stop message)
        {
            var projection = _projections[message.CorrelationId];
            projection.Stop();
        }

        public void Handle(ProjectionMessage.Projections.Management.GetState message)
        {
            var projection = _projections[message.CorrelationId];
            projection.Handle(message);
        }

        public void Handle(ProjectionMessage.Projections.Management.UpdateStatistics message)
        {
            CoreProjection projection;
            if (_projections.TryGetValue(message.CorrelationId, out projection))
            {
                projection.UpdateStatistics();
            }
        }

        public void Handle(ClientMessage.ReadStreamEventsBackwardCompleted message)
        {
            _readDispatcher.Handle(message);
        }

        public void Handle(ClientMessage.WriteEventsCompleted message)
        {
            _writeDispatcher.Handle(message);
        }
    }
}
