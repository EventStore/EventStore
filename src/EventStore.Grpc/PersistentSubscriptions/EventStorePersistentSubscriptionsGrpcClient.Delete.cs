using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Grpc.PersistentSubscriptions {
	partial class EventStorePersistentSubscriptionsGrpcClient {
		public async Task DeleteAsync(string streamName, string groupName,
			UserCredentials userCredentials = default, CancellationToken cancellationToken = default) {
			await _client.DeleteAsync(new DeleteReq {
				Options = new DeleteReq.Types.Options {
					StreamName = streamName,
					GroupName = groupName
				}
			}, RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);
		}
	}
}
