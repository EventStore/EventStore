using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Grpc.Streams;
using Google.Protobuf;

namespace EventStore.Grpc {
	public partial class EventStoreGrpcClient {
		public Task<DeleteResult> SoftDeleteAsync(
			string streamName,
			StreamRevision expectedRevision,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			DeleteInternal(new DeleteReq {
				Options = new DeleteReq.Types.Options {
					StreamName = streamName,
					Revision = expectedRevision
				}
			}, userCredentials, cancellationToken);

		public Task<DeleteResult> SoftDeleteAsync(
			string streamName,
			AnyStreamRevision expectedRevision,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			DeleteInternal(new DeleteReq {
				Options = new DeleteReq.Types.Options {
					StreamName = streamName
				}
			}.WithAnyStreamRevision(expectedRevision), userCredentials, cancellationToken);

		private async Task<DeleteResult> DeleteInternal(DeleteReq request, UserCredentials userCredentials,
			CancellationToken cancellationToken) {
			var result = await _client.DeleteAsync(request, RequestMetadata.Create(userCredentials),
				cancellationToken: cancellationToken);

			return new DeleteResult(new Position(result.Position.CommitPosition, result.Position.PreparePosition));
		}
	}
}
