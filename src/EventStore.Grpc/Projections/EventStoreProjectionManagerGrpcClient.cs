using System;
using Grpc.Core;

namespace EventStore.Grpc.Projections {
	public partial class EventStoreProjectionManagerGrpcClient {
		private readonly Projections.ProjectionsClient _client;

		public EventStoreProjectionManagerGrpcClient(CallInvoker callInvoker) {
			if (callInvoker == null) {
				throw new ArgumentNullException(nameof(callInvoker));
			}

			_client = new Projections.ProjectionsClient(callInvoker);
		}
	}
}
