using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services.UserManagement;
using System;
using System.Linq;

namespace EventStore.Projections.Core.Services.Processing {
	public interface IEmittedStreamsDeleter {
		void DeleteEmittedStreams(Action onEmittedStreamsDeleted);
	}

	public class EmittedStreamsDeleter : IEmittedStreamsDeleter {
		private static readonly ILogger Log = LogManager.GetLoggerFor<EmittedStreamsDeleter>();
		private readonly IODispatcher _ioDispatcher;
		private readonly int _checkPointThreshold = 4000;
		private int _numberOfEventsProcessed = 0;
		private const int RetryLimit = 3;
		private int _retryCount = RetryLimit;
		private readonly string _emittedStreamsId;
		private readonly string _emittedStreamsCheckpointStreamId;

		public EmittedStreamsDeleter(IODispatcher ioDispatcher, string emittedStreamsId,
			string emittedStreamsCheckpointStreamId) {
			_ioDispatcher = ioDispatcher;
			_emittedStreamsId = emittedStreamsId;
			_emittedStreamsCheckpointStreamId = emittedStreamsCheckpointStreamId;
		}

		public void DeleteEmittedStreams(Action onEmittedStreamsDeleted) {
			_ioDispatcher.ReadBackward(_emittedStreamsCheckpointStreamId, -1, 1, false, SystemAccount.Principal,
				result => {
					var deleteFromPosition = GetPositionToDeleteFrom(result);
					DeleteEmittedStreamsFrom(deleteFromPosition, onEmittedStreamsDeleted);
				},
				() => DeleteEmittedStreams(onEmittedStreamsDeleted),
				Guid.NewGuid());
		}

		private int GetPositionToDeleteFrom(ClientMessage.ReadStreamEventsBackwardCompleted onReadCompleted) {
			int deleteFromPosition = 0;
			if (onReadCompleted.Result == ReadStreamResult.Success) {
				if (onReadCompleted.Events.Length > 0) {
					var checkpoint = onReadCompleted.Events
						.Where(v => v.Event.EventType == ProjectionEventTypes.ProjectionCheckpoint).Select(x => x.Event)
						.FirstOrDefault();
					if (checkpoint != null) {
						deleteFromPosition = checkpoint.Data.ParseJson<int>();
					}
				}
			}

			return deleteFromPosition;
		}

		private void DeleteEmittedStreamsFrom(long fromPosition, Action onEmittedStreamsDeleted) {
			_ioDispatcher.ReadForward(_emittedStreamsId, fromPosition, 1, false, SystemAccount.Principal,
				x => ReadCompleted(x, onEmittedStreamsDeleted),
				() => DeleteEmittedStreamsFrom(fromPosition, onEmittedStreamsDeleted),
				Guid.NewGuid());
		}

		private void ReadCompleted(ClientMessage.ReadStreamEventsForwardCompleted onReadCompleted,
			Action onEmittedStreamsDeleted) {
			if (onReadCompleted.Result == ReadStreamResult.Success ||
			    onReadCompleted.Result == ReadStreamResult.NoStream) {
				if (onReadCompleted.Events.Length == 0 && !onReadCompleted.IsEndOfStream) {
					DeleteEmittedStreamsFrom(onReadCompleted.NextEventNumber, onEmittedStreamsDeleted);
					return;
				}

				if (onReadCompleted.Events.Length == 0) {
					_ioDispatcher.DeleteStream(_emittedStreamsCheckpointStreamId, ExpectedVersion.Any, false,
						SystemAccount.Principal, x => {
							if (x.Result == OperationResult.Success || x.Result == OperationResult.StreamDeleted) {
								Log.Info("PROJECTIONS: Projection Stream '{stream}' deleted",
									_emittedStreamsCheckpointStreamId);
							} else {
								Log.Error("PROJECTIONS: Failed to delete projection stream '{stream}'. Reason: {e}",
									_emittedStreamsCheckpointStreamId, x.Result);
							}

							_ioDispatcher.DeleteStream(_emittedStreamsId, ExpectedVersion.Any, false,
								SystemAccount.Principal, y => {
									if (y.Result == OperationResult.Success ||
									    y.Result == OperationResult.StreamDeleted) {
										Log.Info("PROJECTIONS: Projection Stream '{stream}' deleted",
											_emittedStreamsId);
									} else {
										Log.Error(
											"PROJECTIONS: Failed to delete projection stream '{stream}'. Reason: {e}",
											_emittedStreamsId, y.Result);
									}

									onEmittedStreamsDeleted();
								});
						});
				} else {
					var streamId = Helper.UTF8NoBom.GetString(onReadCompleted.Events[0].Event.Data);
					_ioDispatcher.DeleteStream(streamId, ExpectedVersion.Any, false, SystemAccount.Principal,
						x => DeleteStreamCompleted(x, onEmittedStreamsDeleted, streamId,
							onReadCompleted.Events[0].OriginalEventNumber));
				}
			}
		}

		private void DeleteStreamCompleted(ClientMessage.DeleteStreamCompleted deleteStreamCompleted,
			Action onEmittedStreamsDeleted, string streamId, long eventNumber) {
			if (deleteStreamCompleted.Result == OperationResult.Success ||
			    deleteStreamCompleted.Result == OperationResult.StreamDeleted) {
				_retryCount = RetryLimit;
				_numberOfEventsProcessed++;
				if (_numberOfEventsProcessed >= _checkPointThreshold) {
					_numberOfEventsProcessed = 0;
					TryMarkCheckpoint(eventNumber);
				}

				DeleteEmittedStreamsFrom(eventNumber + 1, onEmittedStreamsDeleted);
			} else {
				if (_retryCount == 0) {
					Log.Error(
						"PROJECTIONS: Retry limit reached, could not delete stream: {stream}. Manual intervention is required and you may need to delete this stream manually",
						streamId);
					_retryCount = RetryLimit;
					DeleteEmittedStreamsFrom(eventNumber + 1, onEmittedStreamsDeleted);
					return;
				}

				Log.Error(
					"PROJECTIONS: Failed to delete emitted stream {stream}, Retrying ({retryCount}/{maxRetryCount}). Reason: {reason}",
					streamId, (RetryLimit - _retryCount) + 1, RetryLimit, deleteStreamCompleted.Result);
				_retryCount--;
				DeleteEmittedStreamsFrom(eventNumber, onEmittedStreamsDeleted);
			}
		}

		private void TryMarkCheckpoint(long eventNumber) {
			_ioDispatcher.WriteEvent(_emittedStreamsCheckpointStreamId, ExpectedVersion.Any,
				new Event(Guid.NewGuid(), ProjectionEventTypes.PartitionCheckpoint, true, eventNumber.ToJson(), null),
				SystemAccount.Principal, x => {
					if (x.Result == OperationResult.Success) {
						Log.Debug("PROJECTIONS: Emitted Stream Deletion Checkpoint written at {eventNumber}",
							eventNumber);
					} else {
						Log.Debug(
							"PROJECTIONS: Emitted Stream Deletion Checkpoint Failed to be written at {eventNumber}",
							eventNumber);
					}
				});
		}
	}
}
