using System;
using System.Threading.Tasks;
using EventStore.Core.Messaging;
using EventStore.Grpc.Projections;
using EventStore.Projections.Core.Messages;
using Grpc.Core;
using static EventStore.Grpc.Projections.StatisticsReq.Types.Options;

namespace EventStore.Projections.Core.Services.Grpc {
	public partial class ProjectionManagement {
		public override async Task Statistics(StatisticsReq request, IServerStreamWriter<StatisticsResp> responseStream,
			ServerCallContext context) {
			var statsSource = new TaskCompletionSource<ProjectionStatistics[]>();

			var options = request.Options;
			var name = string.IsNullOrEmpty(options.Name) ? null : options.Name;
			var mode = options.ModeCase switch {
				ModeOneofCase.Continuous => ProjectionMode.Continuous,
				ModeOneofCase.Transient => ProjectionMode.Transient,
				ModeOneofCase.OneTime => ProjectionMode.OneTime,
				_ => default(Nullable<ProjectionMode>)
			};

			var envelope = new CallbackEnvelope(OnMessage);

			_queue.Publish(new ProjectionManagementMessage.Command.GetStatistics(envelope, mode, name, true));

			foreach (var stats in Array.ConvertAll(await statsSource.Task, s => new StatisticsResp.Types.Details {
				BufferedEvents = s.BufferedEvents,
				CheckpointStatus = s.CheckpointStatus,
				CoreProcessingTime = s.CoreProcessingTime,
				EffectiveName = s.EffectiveName,
				Epoch = s.Epoch,
				EventsProcessedAfterRestart = s.EventsProcessedAfterRestart,
				LastCheckpoint = s.LastCheckpoint,
				Mode = s.Mode.ToString(),
				Name = s.Name,
				ReadsInProgress = s.ReadsInProgress,
				PartitionsCached = s.PartitionsCached,
				Position = s.Position,
				Progress = s.Progress,
				StateReason = s.StateReason,
				Status = s.Status,
				Version = s.Version,
				WritePendingEventsAfterCheckpoint = s.WritePendingEventsAfterCheckpoint,
				WritePendingEventsBeforeCheckpoint = s.WritePendingEventsBeforeCheckpoint,
				WritesInProgress = s.WritesInProgress
			})) {
				await responseStream.WriteAsync(new StatisticsResp {Details = stats});
			}

			void OnMessage(Message message) {
				if (!(message is ProjectionManagementMessage.Statistics statistics)) {
					statsSource.TrySetException(UnknownMessage<ProjectionManagementMessage.Statistics>(message));
					return;
				}

				statsSource.TrySetResult(statistics.Projections);
			}
		}
	}
}
