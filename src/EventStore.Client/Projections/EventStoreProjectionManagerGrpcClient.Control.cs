using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Client.Projections {
	public partial class EventStoreProjectionManagerClient {
		public async Task EnableAsync(string name, UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			using var call = _client.EnableAsync(new EnableReq {
				Options = new EnableReq.Types.Options {
					Name = name
				}
			}, RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);
			await call.ResponseAsync.ConfigureAwait(false);
		}

		public Task AbortAsync(string name, UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			DisableInternalAsync(name, true, userCredentials, cancellationToken);

		public Task DisableAsync(string name, UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) =>
			DisableInternalAsync(name, false, userCredentials, cancellationToken);

		private async Task DisableInternalAsync(string name, bool writeCheckpoint, UserCredentials userCredentials,
			CancellationToken cancellationToken) {
			using var call = _client.DisableAsync(new DisableReq {
				Options = new DisableReq.Types.Options {
					Name = name,
					WriteCheckpoint = writeCheckpoint
				}
			}, RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);
			await call.ResponseAsync.ConfigureAwait(false);
		}
	}
}
