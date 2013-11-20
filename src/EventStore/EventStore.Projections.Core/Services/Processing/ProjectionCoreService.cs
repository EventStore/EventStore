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
using System.Security.Principal;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProjectionCoreService : IHandle<ProjectionCoreServiceMessage.StartCore>,
                                         IHandle<ProjectionCoreServiceMessage.StopCore>,
                                         IHandle<ProjectionCoreServiceMessage.CoreTick>,
                                         IHandle<CoreProjectionManagementMessage.CreateAndPrepare>,
                                         IHandle<CoreProjectionManagementMessage.CreatePrepared>,
                                         IHandle<CoreProjectionManagementMessage.CreateAndPrepareSlave>,
                                         IHandle<CoreProjectionManagementMessage.Dispose>,
                                         IHandle<CoreProjectionManagementMessage.Start>,
                                         IHandle<CoreProjectionManagementMessage.LoadStopped>,
                                         IHandle<CoreProjectionManagementMessage.Stop>,
                                         IHandle<CoreProjectionManagementMessage.Kill>,
                                         IHandle<CoreProjectionManagementMessage.GetState>,
                                        IHandle<CoreProjectionManagementMessage.GetResult>,
                                         IHandle<CoreProjectionManagementMessage.UpdateStatistics>,
                                        IHandle<CoreProjectionProcessingMessage.CheckpointCompleted>, 
                                        IHandle<CoreProjectionProcessingMessage.CheckpointLoaded>, 
                                        IHandle<CoreProjectionProcessingMessage.PrerecordedEventsLoaded>, 
                                        IHandle<CoreProjectionProcessingMessage.RestartRequested>,
                                        IHandle<CoreProjectionProcessingMessage.Failed>,
                                        IHandle<ProjectionManagementMessage.SlaveProjectionsStarted>
                                        

    {
        private readonly IPublisher _publisher;
        private readonly IPublisher _inputQueue;
        private readonly ILogger _logger = LogManager.GetLoggerFor<ProjectionCoreService>();

        private readonly Dictionary<Guid, CoreProjection> _projections = new Dictionary<Guid, CoreProjection>();

        private readonly IODispatcher _ioDispatcher;

        private readonly ReaderSubscriptionDispatcher _subscriptionDispatcher;

        private readonly ITimeProvider _timeProvider;
        private readonly ProcessingStrategySelector _processingStrategySelector;

        private readonly SpooledStreamReadingDispatcher _spoolProcessingResponseDispatcher;


        public ProjectionCoreService(
            IPublisher inputQueue, IPublisher publisher, ReaderSubscriptionDispatcher subscriptionDispatcher,
            ITimeProvider timeProvider, IODispatcher ioDispatcher,
            SpooledStreamReadingDispatcher spoolProcessingResponseDispatcher)
        {
            _inputQueue = inputQueue;
            _publisher = publisher;
            _ioDispatcher = ioDispatcher;
            _spoolProcessingResponseDispatcher = spoolProcessingResponseDispatcher;
            _subscriptionDispatcher = subscriptionDispatcher;
            _timeProvider = timeProvider;
            _processingStrategySelector = new ProcessingStrategySelector(
                _subscriptionDispatcher, _spoolProcessingResponseDispatcher);
        }

        public ILogger Logger
        {
            get { return _logger; }
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
                //TODO: factory method can throw
                IProjectionStateHandler stateHandler = message.HandlerFactory();
                string name = message.Name;
                var sourceDefinition = ProjectionSourceDefinition.From(
                    name, stateHandler.GetSourceDefinition(), message.HandlerType, message.Query);
                var projectionVersion = message.Version;
                var projectionConfig = message.Config;
                var namesBuilder = new ProjectionNamesBuilder(name, sourceDefinition);

                var projectionProcessingStrategy = _processingStrategySelector.CreateProjectionProcessingStrategy(
                    name, projectionVersion, namesBuilder,
                    sourceDefinition, projectionConfig, message.HandlerFactory, stateHandler);

                var slaveProjections = projectionProcessingStrategy.GetSlaveProjections();
                CreateCoreProjection(message.ProjectionId, projectionConfig.RunAs, projectionProcessingStrategy);
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.Prepared(
                        message.ProjectionId, sourceDefinition, slaveProjections));
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
                var name = message.Name;
                var sourceDefinition = ProjectionSourceDefinition.From(
                    name, message.SourceDefinition, message.HandlerType, message.Query);
                var projectionVersion = message.Version;
                var projectionConfig = message.Config;
                var namesBuilder = new ProjectionNamesBuilder(name, sourceDefinition);

                var projectionProcessingStrategy = _processingStrategySelector.CreateProjectionProcessingStrategy(
                    name, projectionVersion, namesBuilder, sourceDefinition, projectionConfig, null, null);

                var slaveProjections = projectionProcessingStrategy.GetSlaveProjections();
                CreateCoreProjection(message.ProjectionId, projectionConfig.RunAs, projectionProcessingStrategy);
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.Prepared(
                        message.ProjectionId, sourceDefinition, slaveProjections));
            }
            catch (Exception ex)
            {
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.Faulted(message.ProjectionId, ex.Message));
            }
        }

        public void Handle(CoreProjectionManagementMessage.CreateAndPrepareSlave message)
        {
            try
            {
                //TODO: factory method can throw!
                IProjectionStateHandler stateHandler = message.HandlerFactory();
                string name = message.Name;
                var sourceDefinition = ProjectionSourceDefinition.From(name, stateHandler.GetSourceDefinition(), null, null);
                var projectionVersion = message.Version;
                var projectionConfig = message.Config.SetIsSlave();
                var projectionProcessingStrategy =
                    _processingStrategySelector.CreateSlaveProjectionProcessingStrategy(
                        name, projectionVersion, sourceDefinition, projectionConfig, stateHandler,
                        message.ResultsPublisher, message.MasterCoreProjectionId, this);
                CreateCoreProjection(message.ProjectionId, projectionConfig.RunAs, projectionProcessingStrategy);
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.Prepared(
                        message.ProjectionId, sourceDefinition, slaveProjections: null));
            }
            catch (Exception ex)
            {
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.Faulted(message.ProjectionId, ex.Message));
            }
        }

        private void CreateCoreProjection(
            Guid projectionCorrelationId, IPrincipal runAs, ProjectionProcessingStrategy processingStrategy)
        {
            var projection = processingStrategy.Create(
                projectionCorrelationId, _inputQueue, runAs, _publisher, _ioDispatcher, _subscriptionDispatcher,
                _timeProvider);
            _projections.Add(projectionCorrelationId, projection);
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

        public void Handle(ProjectionManagementMessage.SlaveProjectionsStarted message)
        {
            CoreProjection projection;
            if (_projections.TryGetValue(message.CoreProjectionCorrelationId, out projection))
                projection.Handle(message);
        }
    }
}
