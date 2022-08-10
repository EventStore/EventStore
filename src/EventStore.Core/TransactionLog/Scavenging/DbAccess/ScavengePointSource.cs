using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Core.Services.UserManagement;
using Serilog;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class ScavengePointSource : IScavengePointSource {
		protected static readonly ILogger Log = Serilog.Log.ForContext<ScavengePointSource>();

		private readonly IODispatcher _ioDispatcher;

		public ScavengePointSource(IODispatcher ioDispatcher) {
			_ioDispatcher = ioDispatcher;
		}

		public async Task<ScavengePoint> GetLatestScavengePointOrDefaultAsync(
			CancellationToken cancellationToken) {

			Log.Information("SCAVENGING: Getting latest scavenge point...");

			var readTcs = new TaskCompletionSource<ResolvedEvent[]>(
				TaskCreationOptions.RunContinuationsAsynchronously);
			var endStreamPosition = -1;

			_ioDispatcher.ReadBackward(
				streamId: SystemStreams.ScavengePointsStream,
				fromEventNumber: endStreamPosition,
				maxCount: 1,
				resolveLinks: false,
				principal: SystemAccounts.System,
				action: m => {
					if (m.Result == ReadStreamResult.Success)
						readTcs.TrySetResult(m.Events);
					else if (m.Result == ReadStreamResult.NoStream)
						readTcs.TrySetResult(Array.Empty<ResolvedEvent>());
					else {
						readTcs.TrySetException(new Exception(
							$"Failed to get latest scavenge point: {m.Result}. {m.Error}"));
					}
				},
				timeoutAction: () => {
					readTcs.TrySetException(new Exception(
						"Failed to get latest scavenge point: read timed out"));
				},
				corrId: Guid.NewGuid());

			ResolvedEvent[] events;
			using (cancellationToken.Register(() => readTcs.TrySetCanceled())) {
				events = await readTcs.Task.ConfigureAwait(false);
			}

			if (events.Length == 0) {
				Log.Information("SCAVENGING: No scavenge points exist");
				return default;
			} else if (events.Length != 1) {
				throw new Exception($"Expected 1 event but got {events.Length}");
			}

			var scavengePointEvent = events[0].Event;

			if (scavengePointEvent.EventType != SystemEventTypes.ScavengePoint)
				throw new Exception($"Last event in {SystemStreams.ScavengePointsStream} is not a scavenge point.");

			var scavengePointPayload = ScavengePointPayload.FromBytes(scavengePointEvent.Data);

			var scavengePoint = new ScavengePoint(
				position: scavengePointEvent.LogPosition,
				eventNumber: scavengePointEvent.EventNumber,
				effectiveNow: scavengePointEvent.TimeStamp,
				threshold: scavengePointPayload.Threshold);

			Log.Information("SCAVENGING: Latest scavenge point found is {scavengePoint}", scavengePoint);
			return scavengePoint;
		}

		public async Task<ScavengePoint> AddScavengePointAsync(
			long expectedVersion,
			int threshold,
			CancellationToken cancellationToken) {

			Log.Information("SCAVENGING: Adding new scavenge point #{eventNumber} with threshold {threshold}...",
				expectedVersion + 1, threshold);

			var payload = new ScavengePointPayload {
				Threshold = threshold,
			};

			var writeTcs = new TaskCompletionSource<bool>(
				TaskCreationOptions.RunContinuationsAsynchronously);
			_ioDispatcher.WriteEvent(
				streamId: SystemStreams.ScavengePointsStream,
				expectedVersion: expectedVersion,
				@event: new Event(
					eventId: Guid.NewGuid(),
					eventType: SystemEventTypes.ScavengePoint,
					isJson: true,
					data: payload.ToJsonBytes(),
					metadata: null),
				principal: SystemAccounts.System,
				action: m => {
					if (m.Result == OperationResult.Success) {
						writeTcs.TrySetResult(true);
					} else {
						writeTcs.TrySetException(new Exception(
							$"Failed to add new scavenge point: {m.Result}"));
					}
				}
			);

			using (cancellationToken.Register(() => writeTcs.TrySetCanceled())) {
				await writeTcs.Task.ConfigureAwait(false);
			}

			Log.Information("SCAVENGING: Added new scavenge point.");

			// initial chance to replicate (handy if we are follower)
			await Task.Delay(500, cancellationToken).ConfigureAwait(false);

			const int MaxAttempts = 30;
			var attempt = 0;
			while (true) {
				var scavengePoint = await GetLatestScavengePointOrDefaultAsync(cancellationToken)
					.ConfigureAwait(false);

				// success
				if (scavengePoint.EventNumber == expectedVersion + 1)
					return scavengePoint;

				// give up
				if (++attempt > MaxAttempts)
					throw new Exception(
						$"Unable to read back new scavenge point {expectedVersion + 1}. " +
						$"This node is most likely significantly behind the leader. " +
						$"Allow it to catch up and then try again. ");

				// retry
				Log.Information(
					"SCAVENGING: Did not read new scavenge point. " +
					"Found {actual} but expected {expected}. Retrying {attempt}/{maxAttempts}...",
					scavengePoint.EventNumber, expectedVersion + 1, attempt, MaxAttempts);

				await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
