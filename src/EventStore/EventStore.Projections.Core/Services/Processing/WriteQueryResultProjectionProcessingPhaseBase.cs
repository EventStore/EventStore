using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public abstract class WriteQueryResultProjectionProcessingPhaseBase : IProjectionProcessingPhase
    {
        private int _phase;
        protected string _resultStream;
        private ICoreProjectionForProcessingPhase _coreProjection;
        protected PartitionStateCache _stateCache;
        protected ICoreProjectionCheckpointManager _checkpointManager;
        protected readonly IEmittedEventWriter _emittedEventWriter;
        private bool _subscribed;
        private PhaseState _projectionState;

        public WriteQueryResultProjectionProcessingPhaseBase(
            int phase, string resultStream, ICoreProjectionForProcessingPhase coreProjection,
            PartitionStateCache stateCache, ICoreProjectionCheckpointManager checkpointManager,
            IEmittedEventWriter emittedEventWriter)
        {
            if (resultStream == null) throw new ArgumentNullException("resultStream");
            if (coreProjection == null) throw new ArgumentNullException("coreProjection");
            if (stateCache == null) throw new ArgumentNullException("stateCache");
            if (checkpointManager == null) throw new ArgumentNullException("checkpointManager");
            if (emittedEventWriter == null) throw new ArgumentNullException("emittedEventWriter");
            if (string.IsNullOrEmpty(resultStream)) throw new ArgumentException("resultStream");

            _phase = phase;
            _resultStream = resultStream;
            _coreProjection = coreProjection;
            _stateCache = stateCache;
            _checkpointManager = checkpointManager;
            _emittedEventWriter = emittedEventWriter;
        }

        public ICoreProjectionCheckpointManager CheckpointManager
        {
            get { return _checkpointManager; }
        }

        public void Dispose()
        {
        }

        public void Handle(CoreProjectionManagementMessage.GetState message)
        {
            var state = _stateCache.TryGetPartitionState(message.Partition);
            var stateString = state != null ? state.State : null;
            message.Envelope.ReplyWith(
                new CoreProjectionManagementMessage.StateReport(
                    message.CorrelationId, message.CorrelationId, message.Partition, state: stateString, position: null));
        }

        public void Handle(CoreProjectionManagementMessage.GetResult message)
        {
            var state = _stateCache.TryGetPartitionState(message.Partition);
            var resultString = state != null ? state.Result : null;
            message.Envelope.ReplyWith(
                new CoreProjectionManagementMessage.ResultReport(
                    message.CorrelationId, message.CorrelationId, message.Partition, result: resultString,
                    position: null));

        }

        public void Handle(CoreProjectionProcessingMessage.PrerecordedEventsLoaded message)
        {
            throw new NotImplementedException();
        }

        public CheckpointTag AdjustTag(CheckpointTag tag)
        {
            return tag;
        }

        public void InitializeFromCheckpoint(CheckpointTag checkpointTag)
        {
            _subscribed = false;
        }

        public void AssignSlaves(SlaveProjectionCommunicationChannels slaveProjections)
        {
            throw new NotSupportedException();
        }

        public void ProcessEvent()
        {
            if (!_subscribed)
                throw new InvalidOperationException();
            if (_projectionState != PhaseState.Running)
                return;

            var phaseCheckpointTag = CheckpointTag.FromPhase(_phase, completed: true);
            var writeResults = WriteResults(phaseCheckpointTag);

            var writeEofResults = WriteEofEvent(phaseCheckpointTag);

            _emittedEventWriter.EventsEmitted(writeResults.Concat(writeEofResults).ToArray(), Guid.Empty, null);

            _checkpointManager.EventProcessed(phaseCheckpointTag, 100.0f);
            _coreProjection.CompletePhase();
        }

        private IEnumerable<EmittedEventEnvelope> WriteEofEvent(CheckpointTag phaseCheckpointTag)
        {
            EmittedStream.WriterConfiguration.StreamMetadata streamMetadata = null;
            yield return
                new EmittedEventEnvelope(
                    new EmittedDataEvent(
                        _resultStream, Guid.NewGuid(), "$Eof", true, null, null, phaseCheckpointTag, null),
                    streamMetadata);
        }

        protected abstract IEnumerable<EmittedEventEnvelope> WriteResults(CheckpointTag phaseCheckpointTag);

        public void Subscribe(CheckpointTag from, bool fromCheckpoint)
        {
            _subscribed = true;
            _coreProjection.Subscribed();
        }

        public void SetProjectionState(PhaseState state)
        {
            _projectionState = state;
        }

        public void GetStatistics(ProjectionStatistics info)
        {
            info.Status = info.Status + "/Writing results";
        }

        public CheckpointTag MakeZeroCheckpointTag()
        {
            return CheckpointTag.FromPhase(_phase, completed: false);
        }

        public void EnsureUnsubscribed()
        {
        }
    }
}