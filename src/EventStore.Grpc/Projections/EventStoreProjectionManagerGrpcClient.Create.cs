using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Grpc.Projections {
	public partial class EventStoreProjectionManagerGrpcClient {
		public async Task CreateOneTimeAsync(string query, UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			using var call = _client.CreateAsync(new CreateReq {
				Options = new CreateReq.Types.Options {
					OneTime = new CreateReq.Types.Empty(),
					Query = query
				}
			}, RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);
			await call.ResponseAsync;
		}

		public async Task CreateContinuousAsync(string name, string query, bool trackEmittedStreams = false,
			UserCredentials userCredentials = default, CancellationToken cancellationToken = default) {
			using var call = _client.CreateAsync(new CreateReq {
				Options = new CreateReq.Types.Options {
					Continuous = new CreateReq.Types.Options.Types.Continuous {
						Name = name,
						TrackEmittedStreams = trackEmittedStreams
					},
					Query = query
				}
			}, RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);
			await call.ResponseAsync;
		}

		public async Task CreateTransientAsync(string name, string query, UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			using var call = _client.CreateAsync(new CreateReq {
				Options = new CreateReq.Types.Options {
					Transient = new CreateReq.Types.Options.Types.Transient {
						Name = name
					},
					Query = query
				}
			}, RequestMetadata.Create(userCredentials), cancellationToken: cancellationToken);
			await call.ResponseAsync;
		}
	}
}
