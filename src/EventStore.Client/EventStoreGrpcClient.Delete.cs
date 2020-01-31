using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Streams;

namespace EventStore.Client {
	public partial class EventStoreClient {
		public Task<DeleteResult> SoftDeleteAsync(
			string streamName,
			StreamRevision expectedRevision,
			UserCredentials userCredentials = default,
			TimeSpan? timeoutAfter = default,
			CancellationToken cancellationToken = default) =>
			DeleteInternal(new DeleteReq {
				Options = new DeleteReq.Types.Options {
					StreamName = streamName,
					Revision = expectedRevision
				}
			}, userCredentials, timeoutAfter, cancellationToken);

		public Task<DeleteResult> SoftDeleteAsync(
			string streamName,
			AnyStreamRevision expectedRevision,
			UserCredentials userCredentials = default,
			TimeSpan? timeoutAfter = default,
			CancellationToken cancellationToken = default) =>
			DeleteInternal(new DeleteReq {
				Options = new DeleteReq.Types.Options {
					StreamName = streamName
				}
			}.WithAnyStreamRevision(expectedRevision), userCredentials, timeoutAfter, cancellationToken);

		private async Task<DeleteResult> DeleteInternal(DeleteReq request, UserCredentials userCredentials,
			TimeSpan? timeoutAfter, CancellationToken cancellationToken) {
			var result = await _client.DeleteAsync(request, RequestMetadata.Create(userCredentials),
				DeadLine.After(timeoutAfter),
				cancellationToken);

			return new DeleteResult(new Position(result.Position.CommitPosition, result.Position.PreparePosition));
		}
	}
}
