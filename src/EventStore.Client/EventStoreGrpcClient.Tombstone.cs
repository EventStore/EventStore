using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Streams;

namespace EventStore.Client {
	public partial class EventStoreClient {
		public Task<DeleteResult> TombstoneAsync(
			string streamName,
			StreamRevision expectedRevision,
			UserCredentials userCredentials = default,
			TimeSpan? timeoutAfter = default,
			CancellationToken cancellationToken = default) =>
			TombstoneInternal(new TombstoneReq {
				Options = new TombstoneReq.Types.Options {
					StreamName = streamName,
					Revision = expectedRevision
				}
			}, userCredentials, timeoutAfter, cancellationToken);

		public Task<DeleteResult> TombstoneAsync(
			string streamName,
			AnyStreamRevision expectedRevision,
			UserCredentials userCredentials = default,
			TimeSpan? timeoutAfter = default,
			CancellationToken cancellationToken = default) =>
			TombstoneInternal(new TombstoneReq {
				Options = new TombstoneReq.Types.Options {
					StreamName = streamName
				}
			}.WithAnyStreamRevision(expectedRevision), userCredentials, timeoutAfter, cancellationToken);

		private async Task<DeleteResult> TombstoneInternal(TombstoneReq request, UserCredentials userCredentials,
			TimeSpan? timeoutAfter, CancellationToken cancellationToken) {
			var result = await _client.TombstoneAsync(request, RequestMetadata.Create(userCredentials),
				deadline: DeadLine.After(timeoutAfter), cancellationToken: cancellationToken);

			return new DeleteResult(new Position(result.Position.CommitPosition, result.Position.PreparePosition));
		}
	}
}
