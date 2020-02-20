using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Shared;
using Grpc.Core;

namespace EventStore.Client.Projections {
	public partial class EventStoreProjectionManagerClient {
		public IAsyncEnumerable<ProjectionDetails> ListOneTimeAsync(UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			ListInternalAsync(new StatisticsReq.Types.Options {
				OneTime = new Empty()
			}, userCredentials, cancellationToken);

		public IAsyncEnumerable<ProjectionDetails> ListContinuousAsync(UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			ListInternalAsync(new StatisticsReq.Types.Options {
				Continuous = new Empty()
			}, userCredentials, cancellationToken);

		public Task<ProjectionDetails> GetStatusAsync(string name, UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			ListInternalAsync(new StatisticsReq.Types.Options {
				Name = name
			}, userCredentials, cancellationToken).FirstOrDefaultAsync(cancellationToken).AsTask();

		private async IAsyncEnumerable<ProjectionDetails> ListInternalAsync(StatisticsReq.Types.Options options,
			UserCredentials userCredentials,
			[EnumeratorCancellation] CancellationToken cancellationToken) {
			using var call = _client.Statistics(new StatisticsReq {
				Options = options
			}, RequestMetadata.Create(userCredentials));

			await foreach (var projectionDetails in call.ResponseStream
				.ReadAllAsync(cancellationToken)
				.Select(ConvertToProjectionDetails)
				.WithCancellation(cancellationToken)
				.ConfigureAwait(false)) {
				yield return projectionDetails;
			}
		}

		private static ProjectionDetails ConvertToProjectionDetails(StatisticsResp response) {
			var details = response.Details;

			return new ProjectionDetails(details.CoreProcessingTime, details.Version, details.Epoch,
				details.EffectiveName, details.WritesInProgress, details.ReadsInProgress, details.PartitionsCached,
				details.Status, details.StateReason, details.Name, details.Mode, details.Position, details.Progress,
				details.LastCheckpoint, details.EventsProcessedAfterRestart, details.CheckpointStatus,
				details.BufferedEvents, details.WritePendingEventsBeforeCheckpoint,
				details.WritePendingEventsAfterCheckpoint);
		}

		public IAsyncEnumerable<ProjectionDetails> ListAllAsync(UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			ListInternalAsync(new StatisticsReq.Types.Options {
				All = new Empty()
			}, userCredentials, cancellationToken);
	}
}
