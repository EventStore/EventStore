using System;
using System.Threading.Tasks;
using EventStore.Common.Log;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Services;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class ScavengePointSource : IScavengePointSource {
		protected static readonly ILogger Log = LogManager.GetLoggerFor<ScavengePointSource>();

		private readonly IODispatcher _ioDispatcher;

		public ScavengePointSource(IODispatcher ioDispatcher) {
			_ioDispatcher = ioDispatcher;
		}

		public async Task<ScavengePoint> GetLatestScavengePointOrDefaultAsync() {
			Log.Info("SCAVENGING: Getting latest scavenge point...");

			var readTcs = new TaskCompletionSource<ResolvedEvent[]>(
				TaskCreationOptions.RunContinuationsAsynchronously);
			var endStreamPosition = -1;

			_ioDispatcher.ReadBackward(
				streamId: SystemStreams.ScavengePointsStream,
				fromEventNumber: endStreamPosition,
				maxCount: 1,
				resolveLinks: false,
				principal: SystemAccount.Principal,
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

			var events = await readTcs.Task;

			if (events.Length == 0) {
				Log.Info("SCAVENGING: No scavenge points exist");
				return default;
			} else if (events.Length != 1) {
				throw new Exception($"Expected 1 event but got {events.Length}");
			}

			var scavengePointEvent = events[0].Event;
			var scavengePointPayload = ScavengePointPayload.FromBytes(scavengePointEvent.Data);

			var scavengePoint = new ScavengePoint(
				position: scavengePointEvent.LogPosition,
				eventNumber: scavengePointEvent.EventNumber,
				effectiveNow: scavengePointEvent.TimeStamp,
				threshold: scavengePointPayload.Threshold);

			Log.Info("SCAVENGING: Got latest scavenge point {scavengePoint}", scavengePoint);
			return scavengePoint;
		}

		public async Task<ScavengePoint> AddScavengePointAsync(long expectedVersion, int threshold) {
			Log.Info("SCAVENGING: Adding new scavenge point #{eventNumber} with threshold {threshold}...",
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
				principal: SystemAccount.Principal,
				action: m => {
					if (m.Result == OperationResult.Success) {
						writeTcs.TrySetResult(true);
					} else {
						writeTcs.TrySetException(new Exception(
							$"Failed to add new scavenge point: {m.Result}"));
					}
				}
			);

			await writeTcs.Task;

			Log.Info("SCAVENGING: Added new scavenge point");

			var scavengePoint = await GetLatestScavengePointOrDefaultAsync();

			if (scavengePoint.EventNumber != expectedVersion + 1)
				throw new Exception(
					$"Unexpected error: new scavenge point is number {scavengePoint.EventNumber} " +
					$"instead of {expectedVersion + 1}");

			return scavengePoint;
		}
	}
}
