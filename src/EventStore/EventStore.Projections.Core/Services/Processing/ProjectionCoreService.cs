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
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProjectionCoreService : IHandle<ProjectionCoreServiceMessage.StartCore>,
                                         IHandle<ProjectionCoreServiceMessage.StopCore>,
                                         IHandle<ProjectionCoreServiceMessage.CoreTick>,
                                         IHandle<CoreProjectionManagementMessage.CreateAndPrepare>,
                                         IHandle<CoreProjectionManagementMessage.CreatePrepared>,
                                         IHandle<CoreProjectionManagementMessage.Dispose>,
                                         IHandle<CoreProjectionManagementMessage.Start>,
                                         IHandle<CoreProjectionManagementMessage.LoadStopped>,
                                         IHandle<CoreProjectionManagementMessage.Stop>,
                                         IHandle<CoreProjectionManagementMessage.Kill>,
                                         IHandle<CoreProjectionManagementMessage.GetState>,
                                        IHandle<CoreProjectionManagementMessage.GetResult>,
                                         IHandle<CoreProjectionManagementMessage.UpdateStatistics>,
                                         IHandle<ClientMessage.ReadStreamEventsBackwardCompleted>,
                                         IHandle<ClientMessage.WriteEventsCompleted>, 
                                        IHandle<CoreProjectionProcessingMessage.CheckpointCompleted>, 
                                        IHandle<CoreProjectionProcessingMessage.CheckpointLoaded>, 
                                        IHandle<CoreProjectionProcessingMessage.PrerecordedEventsLoaded>, 
                                        IHandle<CoreProjectionProcessingMessage.RestartRequested>,
                                        IHandle<CoreProjectionProcessingMessage.Failed>


    {
        private readonly IPublisher _publisher;
        private readonly IPublisher _inputQueue;
        private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionCoreService>();

        private readonly Dictionary<Guid, CoreProjection> _projections = new Dictionary<Guid, CoreProjection>();

        private readonly IODispatcher _ioDispatcher;

        private readonly PublishSubscribeDispatcher<ReaderSubscriptionManagement.Subscribe, ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage>
            _subscriptionDispatcher;

        private readonly ITimeProvider _timeProvider;


        public ProjectionCoreService(
            IPublisher inputQueue, IPublisher publisher,
            PublishSubscribeDispatcher
                    <ReaderSubscriptionManagement.Subscribe,
                        ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage
                        >
                subscriptionDispatcher, ITimeProvider timeProvider)
        {
            _inputQueue = inputQueue;
            _publisher = publisher;
            _ioDispatcher = new IODispatcher(publisher, new PublishEnvelope(inputQueue));
            _subscriptionDispatcher = subscriptionDispatcher;
            _timeProvider = timeProvider;
        }

        public void Handle(ProjectionCoreServiceMessage.StartCore message)
        {
        }

        public void Handle(ProjectionCoreServiceMessage.StopCore message)
        {
            StopProjections();
        }

        private void StopProjections()
        {
            _ioDispatcher.BackwardReader.CancelAll();
            _ioDispatcher.ForwardReader.CancelAll();
            _ioDispatcher.Writer.CancelAll();

            var allProjections = _projections.Values;
            foreach (var projection in allProjections)
                projection.Kill();
            if (_projections.Count > 0)
            {
                _logger.Info("_projections is not empty after all the projections have been killed");
                _projections.Clear();
            }
        }

        public void Handle(ProjectionCoreServiceMessage.CoreTick message)
        {
            message.Action();
        }

        public void Handle(CoreProjectionManagementMessage.CreateAndPrepare message)
        {
            try
            {
                //TODO: factory method can throw!
                IProjectionStateHandler stateHandler = message.HandlerFactory();
                // constructor can fail if wrong source definition
                ProjectionSourceDefinition sourceDefinition;
                var projection = CoreProjection.CreateAndPrepare(
                    message.Name, message.Version, message.ProjectionId, _publisher, stateHandler, message.Config,
                    _ioDispatcher, _subscriptionDispatcher, _logger, _timeProvider,
                    out sourceDefinition);
                _projections.Add(message.ProjectionId, projection);
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.Prepared(message.ProjectionId, sourceDefinition));
            }
            catch (Exception ex)
            {
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.Faulted(message.ProjectionId, ex.Message));
            }
        }

        public void Handle(CoreProjectionManagementMessage.CreatePrepared message)
        {
            try
            {
                //TODO: factory method can throw!
                // constructor can fail if wrong source defintion
                ProjectionSourceDefinition sourceDefinition;
                var projection = CoreProjection.CreatePrepared(
                    message.Name, message.Version, message.ProjectionId, _publisher, message.SourceDefinition, message.Config,
                    _ioDispatcher, _subscriptionDispatcher, _logger, _timeProvider, out sourceDefinition);
                _projections.Add(message.ProjectionId, projection);
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.Prepared(message.ProjectionId, sourceDefinition));
            }
            catch (Exception ex)
            {
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.Faulted(message.ProjectionId, ex.Message));
            }
        }

        public void Handle(CoreProjectionManagementMessage.Dispose message)
        {
            CoreProjection projection;
            if (_projections.TryGetValue(message.ProjectionId, out projection))
            {
                _projections.Remove(message.ProjectionId);
                projection.Dispose();
            }
        }

        public void Handle(CoreProjectionManagementMessage.Start message)
        {
            var projection = _projections[message.ProjectionId];
            projection.Start();
        }

        public void Handle(CoreProjectionManagementMessage.LoadStopped message)
        {
            var projection = _projections[message.ProjectionId];
            projection.LoadStopped();
        }

        public void Handle(CoreProjectionManagementMessage.Stop message)
        {
            var projection = _projections[message.ProjectionId];
            projection.Stop();
        }

        public void Handle(CoreProjectionManagementMessage.Kill message)
        {
            var projection = _projections[message.ProjectionId];
            projection.Kill();
        }

        public void Handle(CoreProjectionManagementMessage.GetState message)
        {
            CoreProjection projection;
            if (_projections.TryGetValue(message.ProjectionId, out projection))
                projection.Handle(message);
        }

        public void Handle(CoreProjectionManagementMessage.GetResult message)
        {
            CoreProjection projection;
            if (_projections.TryGetValue(message.ProjectionId, out projection))
                projection.Handle(message);
        }

        public void Handle(CoreProjectionManagementMessage.UpdateStatistics message)
        {
            CoreProjection projection;
            if (_projections.TryGetValue(message.ProjectionId, out projection))
                projection.UpdateStatistics();
        }

        public void Handle(ClientMessage.ReadStreamEventsBackwardCompleted message)
        {
            _ioDispatcher.BackwardReader.Handle(message);
        }

        public void Handle(ClientMessage.WriteEventsCompleted message)
        {
            _ioDispatcher.Writer.Handle(message);
        }

        public void Handle(CoreProjectionProcessingMessage.CheckpointCompleted message)
        {
            CoreProjection projection;
            if (_projections.TryGetValue(message.ProjectionId, out projection))
                projection.Handle(message);
        }

        public void Handle(CoreProjectionProcessingMessage.CheckpointLoaded message)
        {
            CoreProjection projection;
            if (_projections.TryGetValue(message.ProjectionId, out projection))
                projection.Handle(message);
        }

        public void Handle(CoreProjectionProcessingMessage.PrerecordedEventsLoaded message)
        {
            CoreProjection projection;
            if (_projections.TryGetValue(message.ProjectionId, out projection))
                projection.Handle(message);
        }

        public void Handle(CoreProjectionProcessingMessage.RestartRequested message)
        {
            CoreProjection projection;
            if (_projections.TryGetValue(message.ProjectionId, out projection))
                projection.Handle(message);
        }

        public void Handle(CoreProjectionProcessingMessage.Failed message)
        {
            CoreProjection projection;
            if (_projections.TryGetValue(message.ProjectionId, out projection))
                projection.Handle(message);
        }

        public void Handle(EventReaderSubscriptionMessage.CommittedEventReceived message)
        {
            if (_subscriptionDispatcher.Handle(message))
                return;
        }

        public void Handle(EventReaderSubscriptionMessage.CheckpointSuggested message)
        {
            if (_subscriptionDispatcher.Handle(message))
                return;
        }

        public void Handle(EventReaderSubscriptionMessage.EofReached message)
        {
            if (_subscriptionDispatcher.Handle(message))
                return;
        }

        public void Handle(EventReaderSubscriptionMessage.ProgressChanged message)
        {
            if (_subscriptionDispatcher.Handle(message))
                return;
        }

        public void Handle(EventReaderSubscriptionMessage.NotAuthorized message)
        {
            if (_subscriptionDispatcher.Handle(message))
                return;
        }

    }
}
