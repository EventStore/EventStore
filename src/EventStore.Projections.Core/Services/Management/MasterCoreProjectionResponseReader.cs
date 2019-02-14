using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Messages.Persisted.Responses.Slave;
using EventStore.Projections.Core.Services.Processing;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;
using EventStore.Common.Log;

namespace EventStore.Projections.Core.Services.Management {
	public class MasterCoreProjectionResponseReader {
		private static readonly ILogger Log = LogManager.GetLoggerFor<MasterCoreProjectionResponseReader>();

		private readonly IPublisher _publisher;
		private readonly IODispatcher _ioDispatcher;
		private readonly Guid _workerId;
		private readonly Guid _masterProjectionId;
		private readonly string _streamId;

		private IODispatcherAsync.CancellationScope _cancellationScope;
		private bool _stopped;
		private Guid _lastAwakeCorrelationId;

		public MasterCoreProjectionResponseReader(
			IPublisher publisher,
			IODispatcher ioDispatcher,
			Guid workerId,
			Guid masterProjectionId) {
			if (publisher == null) throw new ArgumentNullException("publisher");
			if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");

			_publisher = publisher;
			_ioDispatcher = ioDispatcher;
			_workerId = workerId;
			_masterProjectionId = masterProjectionId;
			_streamId = "$projections-$" + masterProjectionId.ToString("N");
		}

		public void Start() {
			_cancellationScope = new IODispatcherAsync.CancellationScope();
			StartReaderSteps().Run();
		}

		public void Stop() {
			_cancellationScope.Cancel();
			_stopped = true;
			_ioDispatcher.UnsubscribeAwake(_lastAwakeCorrelationId);
		}

		private IEnumerable<IODispatcherAsync.Step> StartReaderSteps() {
			yield return
				_ioDispatcher.BeginUpdateStreamAcl(
					_cancellationScope,
					_streamId,
					ExpectedVersion.Any,
					SystemAccount.Principal,
					new StreamMetadata(maxAge: ProjectionNamesBuilder.SlaveProjectionControlStreamMaxAge),
					completed => { });

			long @from = 0;

			while (!_stopped) {
				var eof = false;
				var subscribeFrom = default(TFPos);
				do {
					yield return
						_ioDispatcher.BeginReadForward(
							_cancellationScope,
							_streamId,
							@from,
							10,
							false,
							SystemAccount.Principal,
							completed => {
								if (completed.Result == ReadStreamResult.Success
								    || completed.Result == ReadStreamResult.NoStream) {
									@from = completed.NextEventNumber == -1 ? 0 : completed.NextEventNumber;
									eof = completed.IsEndOfStream;
									// subscribeFrom is only used if eof
									subscribeFrom = new TFPos(
										completed.TfLastCommitPosition,
										completed.TfLastCommitPosition);
									if (completed.Result == ReadStreamResult.Success) {
										foreach (var e in completed.Events)
											PublishCommand(e);
									}
								}
							},
							() => Log.Warn("Read forward of stream {stream} timed out. Retrying", _streamId));
				} while (!eof);

				_lastAwakeCorrelationId = Guid.NewGuid();
				yield return
					_ioDispatcher.BeginSubscribeAwake(_cancellationScope, _streamId, subscribeFrom, message => { },
						_lastAwakeCorrelationId)
					;
			}

			// unlikely we can ever get here, but still possible - do nothing
		}

		private void PublishCommand(ResolvedEvent resolvedEvent) {
			var command = resolvedEvent.Event.EventType;
			Log.Debug("Response received: {command}", command);
			switch (command) {
				case "$measured": {
					var body = resolvedEvent.Event.Data.ParseJson<PartitionMeasuredResponse>();
					_publisher.Publish(
						new PartitionMeasured(
							_workerId,
							_masterProjectionId,
							Guid.ParseExact(body.SubscriptionId, "N"),
							body.Partition,
							body.Size));
					break;
				}
				case "$progress": {
					var body = resolvedEvent.Event.Data.ParseJson<PartitionProcessingProgressResponse>();
					_publisher.Publish(
						new PartitionProcessingProgress(
							_workerId,
							_masterProjectionId,
							Guid.ParseExact(body.SubscriptionId, "N"),
							body.Progress));
					break;
				}
				case "$result": {
					var body = resolvedEvent.Event.Data.ParseJson<PartitionProcessingResultResponse>();
					_publisher.Publish(
						new PartitionProcessingResult(
							_workerId,
							_masterProjectionId,
							Guid.ParseExact(body.SubscriptionId, "N"),
							body.Partition,
							Guid.ParseExact(body.CausedBy, "N"),
							body.Position,
							body.Result));
					break;
				}
				default:
					throw new Exception("Unknown response: " + command);
			}
		}
	}
}
