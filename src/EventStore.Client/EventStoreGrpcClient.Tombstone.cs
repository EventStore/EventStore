using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using Google.Protobuf;

namespace EventStore.Client {
	public partial class EventStoreClient {
		public Task<DeleteResult> TombstoneAsync(
			string streamName,
			StreamRevision expectedRevision,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			TombstoneInternal(new TombstoneReq {
				Options = new TombstoneReq.Types.Options {
					StreamName = streamName,
					Revision = expectedRevision
				}
			}, userCredentials, cancellationToken);

		public Task<DeleteResult> TombstoneAsync(
			string streamName,
			AnyStreamRevision expectedRevision,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			TombstoneInternal(new TombstoneReq {
				Options = new TombstoneReq.Types.Options {
					StreamName = streamName
				}
			}.WithAnyStreamRevision(expectedRevision), userCredentials, cancellationToken);

		private async Task<DeleteResult> TombstoneInternal(TombstoneReq request, UserCredentials userCredentials,
			CancellationToken cancellationToken) {
			var result = await _client.TombstoneAsync(request, RequestMetadata.Create(userCredentials),
				cancellationToken: cancellationToken);

			return new DeleteResult(new Position(result.Position.CommitPosition, result.Position.PreparePosition));
		}
	}
}
