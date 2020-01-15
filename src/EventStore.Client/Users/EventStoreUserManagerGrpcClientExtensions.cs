using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Client.Users {
	public static class EventStoreUserManagerGrpcClientExtensions {
		public static Task<UserDetails> GetCurrentUserAsync(this EventStoreUserManagerClient users,
			UserCredentials userCredentials, CancellationToken cancellationToken = default)
			=> users.GetUserAsync(userCredentials.Username, userCredentials, cancellationToken);
	}
}
