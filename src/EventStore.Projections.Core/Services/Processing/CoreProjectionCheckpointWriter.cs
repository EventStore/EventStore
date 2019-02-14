using System;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public class CoreProjectionCheckpointWriter {
		private readonly string _projectionCheckpointStreamId;
		private readonly ILogger _logger;
		private readonly IODispatcher _ioDispatcher;
		private readonly ProjectionVersion _projectionVersion;
		private readonly string _name;

		private Guid _writeRequestId;
		private int _inCheckpointWriteAttempt;
		private long _lastWrittenCheckpointEventNumber;
		private Event _checkpointEventToBePublished;
		private CheckpointTag _requestedCheckpointPosition;
		private IEnvelope _envelope;
		private const int MaxNumberOfRetries = 5;

		public CoreProjectionCheckpointWriter(
			string projectionCheckpointStreamId, IODispatcher ioDispatcher, ProjectionVersion projectionVersion,
			string name) {
			_projectionCheckpointStreamId = projectionCheckpointStreamId;
			_logger = LogManager.GetLoggerFor<CoreProjectionCheckpointWriter>();
			_ioDispatcher = ioDispatcher;
			_projectionVersion = projectionVersion;
			_name = name;
		}

		public void BeginWriteCheckpoint(IEnvelope envelope,
			CheckpointTag requestedCheckpointPosition, string requestedCheckpointState) {
			_envelope = envelope;
			_requestedCheckpointPosition = requestedCheckpointPosition;
			_inCheckpointWriteAttempt = 1;
			//TODO: pass correct expected version
			_checkpointEventToBePublished = new Event(
				Guid.NewGuid(), ProjectionEventTypes.ProjectionCheckpoint, true,
				requestedCheckpointState == null ? null : Helper.UTF8NoBom.GetBytes(requestedCheckpointState),
				requestedCheckpointPosition.ToJsonBytes(projectionVersion: _projectionVersion));
			PublishWriteStreamMetadataAndCheckpointEvent();
		}

		private void WriteCheckpointEventCompleted(
			string eventStreamId, OperationResult operationResult, long firstWrittenEventNumber) {
			if (_inCheckpointWriteAttempt == 0)
				throw new InvalidOperationException();
			if (operationResult == OperationResult.Success) {
				if (_logger != null)
					_logger.Trace(
						"Checkpoint has been written for projection {projection} at sequence number {firstWrittenEventNumber} (current)",
						_name,
						firstWrittenEventNumber);
				_lastWrittenCheckpointEventNumber = firstWrittenEventNumber;

				_inCheckpointWriteAttempt = 0;
				_envelope.ReplyWith(
					new CoreProjectionCheckpointWriterMessage.CheckpointWritten(_requestedCheckpointPosition));
			} else {
				if (_logger != null) {
					_logger.Info(
						"Failed to write projection checkpoint to stream {stream}. Error: {e}", eventStreamId,
						Enum.GetName(typeof(OperationResult), operationResult));
				}

				switch (operationResult) {
					case OperationResult.WrongExpectedVersion:
						_envelope.ReplyWith(
							new CoreProjectionCheckpointWriterMessage.RestartRequested(
								"Checkpoint stream has been written to from the outside"));
						break;
					case OperationResult.PrepareTimeout:
					case OperationResult.ForwardTimeout:
					case OperationResult.CommitTimeout:
						if (_inCheckpointWriteAttempt >= MaxNumberOfRetries) {
							//The first parameter is not needed in this case as the CoreProjectionCheckpointManager takes care of filling in the projection id when it reconstructs the message
							_envelope.ReplyWith(new CoreProjectionProcessingMessage.Failed(Guid.Empty,
								string.Format(
									"After retrying {0} times, we failed to write the checkpoint for {1} to {2} due to a {3}",
									MaxNumberOfRetries, _name, eventStreamId,
									Enum.GetName(typeof(OperationResult), operationResult))));
							_inCheckpointWriteAttempt = 0;
							return;
						}

						_inCheckpointWriteAttempt++;
						PublishWriteStreamMetadataAndCheckpointEvent();
						break;
					default:
						throw new NotSupportedException("Unsupported error code received");
				}
			}
		}

		private void PublishWriteStreamMetadataAndCheckpointEvent() {
			if (_logger != null)
				_logger.Trace(
					"Writing checkpoint for {projection} at {requestedCheckpointPosition} with expected version number {lastWrittenCheckpointEventNumber}",
					_name, _requestedCheckpointPosition, _lastWrittenCheckpointEventNumber);
			if (_lastWrittenCheckpointEventNumber == ExpectedVersion.NoStream)
				PublishWriteStreamMetadata();
			else
				PublishWriteCheckpointEvent();
		}

		private void PublishWriteStreamMetadata() {
			var metaStreamId = SystemStreams.MetastreamOf(_projectionCheckpointStreamId);
			_writeRequestId = _ioDispatcher.WriteEvent(
				metaStreamId, ExpectedVersion.Any, CreateStreamMetadataEvent(), SystemAccount.Principal, msg => {
					switch (msg.Result) {
						case OperationResult.Success:
							PublishWriteCheckpointEvent();
							break;
						default:
							WriteCheckpointEventCompleted(metaStreamId, msg.Result, ExpectedVersion.Invalid);
							break;
					}
				});
		}

		private Event CreateStreamMetadataEvent() {
			var eventId = Guid.NewGuid();
			var acl = new StreamAcl(
				readRole: SystemRoles.Admins, writeRole: SystemRoles.Admins,
				deleteRole: SystemRoles.Admins, metaReadRole: SystemRoles.All,
				metaWriteRole: SystemRoles.Admins);
			var metadata = new StreamMetadata(maxCount: 2, maxAge: null, cacheControl: null, acl: acl);
			var dataBytes = metadata.ToJsonBytes();
			return new Event(eventId, SystemEventTypes.StreamMetadata, isJson: true, data: dataBytes, metadata: null);
		}

		private void PublishWriteCheckpointEvent() {
			_writeRequestId = _ioDispatcher.WriteEvent(
				_projectionCheckpointStreamId, _lastWrittenCheckpointEventNumber, _checkpointEventToBePublished,
				SystemAccount.Principal,
				msg => WriteCheckpointEventCompleted(_projectionCheckpointStreamId, msg.Result, msg.FirstEventNumber));
		}

		public void Initialize() {
			_checkpointEventToBePublished = null;
			_inCheckpointWriteAttempt = 0;
			_ioDispatcher.Writer.Cancel(_writeRequestId);
			_lastWrittenCheckpointEventNumber = ExpectedVersion.Invalid;
		}

		public void GetStatistics(ProjectionStatistics info) {
			info.WritesInProgress = ((_inCheckpointWriteAttempt != 0) ? 1 : 0) + info.WritesInProgress;
			info.CheckpointStatus = _inCheckpointWriteAttempt > 0
				? "Writing (" + _inCheckpointWriteAttempt + ")"
				: info.CheckpointStatus;
		}

		public void StartFrom(CheckpointTag checkpointTag, long checkpointEventNumber) {
			_lastWrittenCheckpointEventNumber = checkpointEventNumber;
		}
	}
}
