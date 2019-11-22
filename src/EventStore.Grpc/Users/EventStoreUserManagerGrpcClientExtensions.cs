using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Grpc.Users {
	public static class EventStoreUserManagerGrpcClientExtensions {
		public static Task<UserDetails> GetCurrentUserAsync(this EventStoreUserManagerGrpcClient users,
			UserCredentials userCredentials, CancellationToken cancellationToken = default)
			=> users.GetUserAsync(userCredentials.Username, userCredentials, cancellationToken);
	}
}
