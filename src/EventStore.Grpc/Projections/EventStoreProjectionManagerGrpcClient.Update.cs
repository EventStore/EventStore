using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Grpc.Projections {
	public partial class EventStoreProjectionManagerGrpcClient {
		public async Task UpdateAsync(string name, string query, bool? emitEnabled = null,
			UserCredentials userCredentials = default, CancellationToken cancellationToken = default) {
			var options = new UpdateReq.Types.Options {
				Name = name,
				Query = query
			};
			if (emitEnabled.HasValue) {
				options.EmitEnabled = emitEnabled.Value;
			} else {
				options.NoEmitOptions = new UpdateReq.Types.Empty();
			}

			using var call = _client.UpdateAsync(new UpdateReq {
					Options = options
				},
				RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);

			await call.ResponseAsync;
		}
	}
}
