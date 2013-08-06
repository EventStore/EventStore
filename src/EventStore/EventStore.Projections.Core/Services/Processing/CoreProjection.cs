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
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Utils;

namespace EventStore.Projections.Core.Services.Processing
{
    //TODO: replace Console.WriteLine with logging
    //TODO: separate check-pointing from projection handling

    public class CoreProjection : IDisposable,
                                  ICoreProjection,
                                  ICoreProjectionForProcessingPhase,
                                  IHandle<CoreProjectionManagementMessage.GetState>,
                                  IHandle<CoreProjectionManagementMessage.GetResult>
    {
        [Flags]
        private enum State : uint
        {
            Initial = 0x80000000,
            LoadStateRequested = 0x1,
            StateLoaded = 0x2,
            Subscribed = 0x4,
            Running = 0x08,
            Stopping = 0x40,
            Stopped = 0x80,
            FaultedStopping = 0x100,
            Faulted = 0x200,
        }

        private readonly string _name;
        private readonly ProjectionVersion _version;
        private readonly CheckpointTag _zeroCheckpointTag;

        private readonly IPublisher _publisher;

        private readonly Guid _projectionCorrelationId;
        private readonly ProjectionConfig _projectionConfig;

        private readonly
            PublishSubscribeDispatcher
                <ReaderSubscriptionManagement.Subscribe,
                    ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage>
            _subscriptionDispatcher;

        private readonly ILogger _logger;

        private State _state;

        private string _faultedReason;

        private readonly PartitionStateCache _partitionStateCache;
        private readonly ICoreProjectionCheckpointManager _checkpointManager;

        private bool _tickPending;
        private long _expectedSubscriptionMessageSequenceNumber = -1;
        private Guid _currentSubscriptionId;

        private bool _subscribed;
        private bool _startOnLoad;
        private bool _completed;

        private CheckpointSuggestedWorkItem _checkpointSuggestedWorkItem;
        private readonly IProjectionProcessingPhase _projectionProcessingPhase;


        public CoreProjection(
            string name, ProjectionVersion version, Guid projectionCorrelationId, IPublisher publisher,
            IProjectionStateHandler projectionStateHandler, ProjectionConfig projectionConfig, IODispatcher ioDispatcher,
            PublishSubscribeDispatcher
                <ReaderSubscriptionManagement.Subscribe,
                    ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage>
                subscriptionDispatcher, ILogger logger, CheckpointStrategy checkpointStrategy,
            ProjectionNamesBuilder namingBuilder, ProjectionProcessingStrategy projectionProcessingStrategy)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (name == "") throw new ArgumentException("name");
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");
            if (subscriptionDispatcher == null) throw new ArgumentNullException("subscriptionDispatcher");
            var coreProjectionCheckpointManager = checkpointStrategy.CreateCheckpointManager(
                projectionCorrelationId, version, publisher, ioDispatcher, projectionConfig, name, namingBuilder);

            _projectionCorrelationId = projectionCorrelationId;
            _name = name;
            _version = version;
            _projectionConfig = projectionConfig;
            _subscriptionDispatcher = subscriptionDispatcher;
            _logger = logger;
            _publisher = publisher;

            var readerStrategy = checkpointStrategy.ReaderStrategy;
            _zeroCheckpointTag = readerStrategy.PositionTagger.MakeZeroCheckpointTag();
            _checkpointManager = coreProjectionCheckpointManager;
            _partitionStateCache = new PartitionStateCache(_zeroCheckpointTag);

            var statePartitionSelector = checkpointStrategy.CreateStatePartitionSelector(projectionStateHandler);
            namingBuilder.GetPartitionCatalogStreamName();
            var projectionProcessingPhase = projectionProcessingStrategy.CreateFirstProcessingPhase(
                checkpointStrategy, name, publisher, projectionStateHandler, projectionConfig, logger,
                projectionCorrelationId, _partitionStateCache, UpdateStatistics, this, namingBuilder, _checkpointManager,
                statePartitionSelector);
            _projectionProcessingPhase = projectionProcessingPhase;

            GoToState(State.Initial);
        }

        internal void UpdateStatistics()
        {
            var info = new ProjectionStatistics();
            GetStatistics(info);
            _publisher.Publish(
                new CoreProjectionManagementMessage.StatisticsReport(_projectionCorrelationId, info));
        }

        public void Start()
        {
            _startOnLoad = true;
            EnsureState(State.Initial);
            GoToState(State.LoadStateRequested);
        }

        public void LoadStopped()
        {
            _startOnLoad = false;
            EnsureState(State.Initial);
            GoToState(State.LoadStateRequested);
        }

        public void Stop()
        {
            EnsureState(State.LoadStateRequested | State.StateLoaded | State.Subscribed | State.Running);
            try
            {
                if (_state == State.LoadStateRequested)
                    GoToState(State.Stopped);
                else
                    GoToState(State.Stopping);
            }
            catch (Exception ex)
            {
                SetFaulted(ex);
            }
        }

        public void Kill()
        {
            SetFaulted("Killed");
        }

        private void GetStatistics(ProjectionStatistics info)
        {
            _checkpointManager.GetStatistics(info);
            info.Status = _state.EnumValueName() + info.Status; 
            info.Name = _name;
            info.EffectiveName = _name;
            info.ProjectionId = _version.ProjectionId;
            info.Epoch = _version.Epoch;
            info.Version = _version.Version;
            info.StateReason = "";
            info.BufferedEvents = 0; 
            info.PartitionsCached = _partitionStateCache.CachedItemCount;

            _projectionProcessingPhase.GetStatistics(info);
        }

        public void Handle(EventReaderSubscriptionMessage.CommittedEventReceived message)
        {
            if (_state != State.StateLoaded)
            {
                if (IsOutOfOrderSubscriptionMessage(message))
                    return;
                RegisterSubscriptionMessage(message);
            }
            EnsureState(
                /* load state restores already ordered events by sending committed events back to the projection */
                State.StateLoaded | State.Running | State.Stopping | State.Stopped | State.FaultedStopping
                | State.Faulted);
            //TODO: should we allow stopped states here? 
            _projectionProcessingPhase.Handle(message);
            if (_state != State.StateLoaded)
                EnsureTickPending();
        }

        public void Handle(EventReaderSubscriptionMessage.ProgressChanged message)
        {
            if (IsOutOfOrderSubscriptionMessage(message))
                return;
            RegisterSubscriptionMessage(message);

            EnsureState(State.Running | State.Stopping | State.Stopped | State.FaultedStopping | State.Faulted);
            _projectionProcessingPhase.Handle(message);
        }

        public void Handle(EventReaderSubscriptionMessage.NotAuthorized message)
        {
            if (IsOutOfOrderSubscriptionMessage(message))
                return;
            RegisterSubscriptionMessage(message);

            EnsureState(State.Running | State.Stopping | State.Stopped | State.FaultedStopping | State.Faulted);

            _projectionProcessingPhase.Handle(message);
        }

        public void Handle(EventReaderSubscriptionMessage.EofReached message)
        {
            if (IsOutOfOrderSubscriptionMessage(message))
                return;
            RegisterSubscriptionMessage(message);

            EnsureState(State.Running | State.Stopping | State.Stopped | State.FaultedStopping | State.Faulted);
            _projectionProcessingPhase.Handle(message);
        }

        public void Handle(EventReaderSubscriptionMessage.CheckpointSuggested message)
        {
            if (IsOutOfOrderSubscriptionMessage(message))
                return;
            RegisterSubscriptionMessage(message);

            EnsureState(State.Running | State.Stopping | State.Stopped | State.FaultedStopping | State.Faulted);
            _projectionProcessingPhase.Handle(message);
        }

        public void Unsubscribed()
        {
            _subscriptionDispatcher.Cancel(_projectionCorrelationId);
            _subscribed = false;
            _projectionProcessingPhase.Unsubscribed();
        }

        public void Complete()
        {
            if (_state != State.Running)
                return;
            if (!_projectionConfig.StopOnEof)
                throw new InvalidOperationException("!_projectionConfig.StopOnEof");
            _completed = true;
            _checkpointManager.Progress(100.0f);
            Unsubscribed(); // NOTE:  stopOnEof subscriptions automatically unsubscribe when handling this message
            Stop();
        }

        public void Handle(CoreProjectionManagementMessage.GetState message)
        {
            if (_state == State.LoadStateRequested || _state == State.StateLoaded)
            {
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.StateReport(
                        message.CorrelationId, _projectionCorrelationId, message.Partition, state: null, position: null,
                        exception: new Exception("Not yet available")));
                return;
            }
            EnsureState(State.Running | State.Stopping | State.Stopped | State.FaultedStopping | State.Faulted);
            _projectionProcessingPhase.Handle(message);
        }

        public void Handle(CoreProjectionManagementMessage.GetResult message)
        {
            if (_state == State.LoadStateRequested || _state == State.StateLoaded)
            {
                message.Envelope.ReplyWith(
                    new CoreProjectionManagementMessage.ResultReport(
                        message.CorrelationId, _projectionCorrelationId, message.Partition, result: null, position: null,
                        exception: new Exception("Not yet available")));
                return;
            }
            EnsureState(State.Running | State.Stopping | State.Stopped | State.FaultedStopping | State.Faulted);
            _projectionProcessingPhase.Handle(message);
        }

        public void Handle(CoreProjectionProcessingMessage.CheckpointCompleted message)
        {
            CheckpointCompleted(message.CheckpointTag);
        }

        public void Handle(CoreProjectionProcessingMessage.CheckpointLoaded message)
        {
            EnsureState(State.LoadStateRequested);
            try
            {
                InitializeProjectionFromCheckpoint(message.CheckpointData, message.CheckpointTag);
            }
            catch (Exception ex)
            {
                SetFaulted(ex);
            }
        }

        public void Handle(CoreProjectionProcessingMessage.PrerecordedEventsLoaded message)
        {
            EnsureState(State.StateLoaded);
            try
            {
                UnsubscribeFromPreRecordedOrderEvents();
                Subscribe(message.CheckpointTag);
            }
            catch (Exception ex)
            {
                SetFaulted(ex);
            }
        }

        private void UnsubscribeFromPreRecordedOrderEvents()
        {
            // projectionCorrelationId is used as a subscription identifier for delivery
            // of pre-recorded order events recovered by checkpoint manager
            _subscriptionDispatcher.Cancel(_projectionCorrelationId);
        }

        public void Handle(CoreProjectionProcessingMessage.RestartRequested message)
        {
            _logger.Info(
                "Projection '{0}'({1}) restart has been requested due to: '{2}'", _name, _projectionCorrelationId,
                message.Reason);
            if (_state != State.Running)
            {
                SetFaulted(
                    string.Format(
                        "A concurrency violation detected, but the projection is not running. Current state is: {0}.  The reason for the restart is: '{1}' ",
                        _state, message.Reason));
                return;
            }

                //
            EnsureUnsubscribed();
            GoToState(State.Initial);
            Start();
            
        }

        public void Handle(CoreProjectionProcessingMessage.Failed message)
        {
            SetFaulted(message.Reason);
        }

        public void EnsureUnsubscribed()
        {
            if (_subscribed)
            {
                Unsubscribed();
                // this was we distinguish pre-recorded events subscription
                if (_currentSubscriptionId != _projectionCorrelationId) 
                    _publisher.Publish(new ReaderSubscriptionManagement.Unsubscribe(_currentSubscriptionId));
            }
        }

        private void GoToState(State state)
        {
            var wasStopped = _state == State.Stopped || _state == State.Faulted;
            var wasStopping = _state == State.Stopping || _state == State.FaultedStopping;
            var wasStarted = _state == State.Subscribed 
                             || _state == State.Running || _state == State.Stopping || _state == State.FaultedStopping;
            var wasRunning = _state == State.Running;
            var wasFaulted = _state == State.Faulted || _state == State.FaultedStopping;

            _state = state; // set state before transition to allow further state change
            switch (state)
            {
                case State.Stopped:
                case State.Faulted:
                    if (wasStarted && !wasStopped)
                        _checkpointManager.Stopped();
                    break;
                case State.Stopping:
                case State.FaultedStopping:
                    if (wasStarted && !wasStopping)
                        _checkpointManager.Stopping();
                    break;
            }

            switch (state)
            {
                case State.Running:
                    if (!wasRunning)
                        _projectionProcessingPhase.SetRunning();
                    break;
                case State.Faulted:
                case State.FaultedStopping:
                    if (!wasFaulted)
                        _projectionProcessingPhase.SetFaulted();
                    if (wasRunning)
                        _projectionProcessingPhase.SetStopped();
                    break;
                case State.Stopped:
                case State.Stopping:
                    if (wasRunning)
                        _projectionProcessingPhase.SetStopped();
                    break;
                default:
                    _projectionProcessingPhase.SetUnknownState();
                    break;

            }
            switch (state)
            {
                case State.Initial:
                    EnterInitial();
                    break;
                case State.LoadStateRequested:
                    EnterLoadStateRequested();
                    break;
                case State.StateLoaded:
                    EnterStateLoaded();
                    break;
                case State.Subscribed:
                    EnterSubscribed();
                    break;
                case State.Running:
                    EnterRunning();
                    break;
                case State.Stopping:
                    EnterStopping();
                    break;
                case State.Stopped:
                    EnterStopped();
                    break;
                case State.FaultedStopping:
                    EnterFaultedStopping();
                    break;
                case State.Faulted:
                    EnterFaulted();
                    break;
                default:
                    throw new Exception();
            }
        }

        private void EnterInitial()
        {
            _completed = false;
            _subscribed = false;
            _partitionStateCache.Initialize();
            _projectionProcessingPhase.Initialize();
            _checkpointManager.Initialize();
            _tickPending = false;
            _partitionStateCache.CacheAndLockPartitionState("", new PartitionState("", null, _zeroCheckpointTag), null);
            _expectedSubscriptionMessageSequenceNumber = -1; // this is to be overridden when subscribing
            _currentSubscriptionId = Guid.Empty;
            // NOTE: this is to workaround exception in GetState requests submitted by client
        }

        private void EnterLoadStateRequested()
        {
            SubscribeToPreRecordedOrderEvents();
            _checkpointManager.BeginLoadState();
        }

        private void SubscribeToPreRecordedOrderEvents()
        {
            // projectionCorrelationId is used as a subscription identifier for delivery
            // of pre-recorded order events recovered by checkpoint manager
            _currentSubscriptionId = _projectionCorrelationId;
            _subscriptionDispatcher.Subscribed(_projectionCorrelationId, this);
            _subscribed = true; // even if it is not a real subscription we need to unsubscribe 
            
        }

        private void EnterStateLoaded()
        {
        }

        private void EnterSubscribed()
        {
            if (_startOnLoad)
            {
                GoToState(State.Running);
            }
            else
                GoToState(State.Stopped);
        }

        private void EnterRunning()
        {
            try
            {
                _publisher.Publish(new CoreProjectionManagementMessage.Started(_projectionCorrelationId));
                UpdateStatistics();
                _projectionProcessingPhase.ProcessEvent();
            }
            catch (Exception ex)
            {
                SetFaulted(ex);
            }
        }

        private void EnterStopping()
        {
        }

        private void EnterStopped()
        {
            UpdateStatistics();
            _publisher.Publish(new CoreProjectionManagementMessage.Stopped(_projectionCorrelationId, _completed));
        }

        private void EnterFaultedStopping()
        {
        }

        private void EnterFaulted()
        {
            UpdateStatistics();
            _publisher.Publish(
                new CoreProjectionManagementMessage.Faulted(_projectionCorrelationId, _faultedReason));
        }

        private bool IsOutOfOrderSubscriptionMessage(EventReaderSubscriptionMessage message)
        {
            if (_currentSubscriptionId != message.SubscriptionId)
                return true;
            if (_expectedSubscriptionMessageSequenceNumber != message.SubscriptionMessageSequenceNumber)
                throw new InvalidOperationException("Out of order message detected");
            return false;
        }

        private void RegisterSubscriptionMessage(EventReaderSubscriptionMessage message)
        {
            _expectedSubscriptionMessageSequenceNumber = message.SubscriptionMessageSequenceNumber + 1;
        }

        private void EnsureState(State expectedStates)
        {
            if ((_state & expectedStates) == 0)
            {
                throw new Exception(
                    string.Format("Current state is {0}. Expected states are: {1}", _state, expectedStates));
            }
        }

        private void Tick()
        {
            // ignore any ticks received when not pending. this may happen when restart requested
            if (!_tickPending)
                return;
            // process messagesin almost all states as we now ignore work items when processing
            EnsureState(State.Running | State.Stopped | State.Stopping | State.FaultedStopping | State.Faulted);
            try
            {
                _tickPending = false;
                _projectionProcessingPhase.ProcessEvent();
            }
            catch (Exception ex)
            {
                SetFaulted(ex);
            }
        }

        private void InitializeProjectionFromCheckpoint(string state, CheckpointTag checkpointTag)
        {
            //TODO: initialize projection state here (test it)
            //TODO: write test to ensure projection state is correctly loaded from a checkpoint and posted back when enough empty records processed
            _partitionStateCache.CacheAndLockPartitionState("", PartitionState.Deserialize(state, checkpointTag), null);
            _checkpointManager.Start(checkpointTag);
            _projectionProcessingPhase.InitializeFromCheckpoint(checkpointTag);
            GoToState(State.StateLoaded);
        }

        private void Subscribe(CheckpointTag checkpointTag)
        {
            _expectedSubscriptionMessageSequenceNumber = 0;
            _currentSubscriptionId = Guid.NewGuid();
            _projectionProcessingPhase.Subscribed(_currentSubscriptionId);
            var subscriptionOptions = new ReaderSubscriptionOptions(
                _projectionConfig.CheckpointUnhandledBytesThreshold, _projectionConfig.CheckpointHandledThreshold,
                _projectionConfig.StopOnEof, stopAfterNEvents: null);
            _subscriptionDispatcher.PublishSubscribe(
                new ReaderSubscriptionManagement.Subscribe(
                    _currentSubscriptionId, checkpointTag, _projectionProcessingPhase.ReaderStrategy, subscriptionOptions), this);
            _subscribed = true;
            try
            {
                GoToState(State.Subscribed);
            }
            catch (Exception ex)
            {
                SetFaulted(ex);
                return;
            }
        }

        public void Dispose()
        {
            EnsureUnsubscribed();
            if (_projectionProcessingPhase != null)
                _projectionProcessingPhase.Dispose();
        }

        public void EnsureTickPending()
        {
            // ticks are requested when an async operation is completed or when an item is being processed
            // thus, the tick message is removed from the queue when it does not process any work item (and 
            // it is renewed therefore)
            if (_tickPending)
                return;
            _tickPending = true;
            _publisher.Publish(new ProjectionCoreServiceMessage.CoreTick(Tick));
        }

        public void SetFaulted(Exception ex)
        {
            SetFaulted(ex.Message);
        }

        public void SetFaulted(string reason)
        {
            if (_state != State.FaultedStopping && _state != State.Faulted)
                _faultedReason = reason;
            if (_state != State.Faulted)
                GoToState(State.Faulted);
        }

        public void SetFaulting(string reason)
        {
            if (_state != State.FaultedStopping && _state != State.Faulted)
            {
                _faultedReason = reason;
                GoToState(State.FaultedStopping);
            }
        }

        private void CheckpointCompleted(CheckpointTag lastCompletedCheckpointPosition)
        {
            CompleteCheckpointSuggestedWorkItem();
            // all emitted events caused by events before the checkpoint position have been written  
            // unlock states, so the cache can be clean up as they can now be safely reloaded from the ES
            _partitionStateCache.Unlock(lastCompletedCheckpointPosition);

            switch (_state)
            {
                case State.Stopping:
                    GoToState(State.Stopped);
                    break;
                case State.FaultedStopping:
                    GoToState(State.Faulted);
                    break;
            }
        }

        public void SetCurrentCheckpointSuggestedWorkItem(CheckpointSuggestedWorkItem checkpointSuggestedWorkItem)
        {
            if (_checkpointSuggestedWorkItem != null && checkpointSuggestedWorkItem != null)
                throw new InvalidOperationException("Checkpoint in progress");
            if (_checkpointSuggestedWorkItem == null && checkpointSuggestedWorkItem == null)
                throw new InvalidOperationException("No checkpoint in progress");
            _checkpointSuggestedWorkItem = checkpointSuggestedWorkItem;
        }

        private void CompleteCheckpointSuggestedWorkItem()
        {
            var workItem = _checkpointSuggestedWorkItem;
            if (workItem != null)
            {
                _checkpointSuggestedWorkItem = null; 
                workItem.CheckpointCompleted();
                EnsureTickPending();
            }
        }


        public CheckpointTag LastProcessedEventPosition
        {
            get { return _checkpointManager.LastProcessedEventPosition; }
        }

    }
}
