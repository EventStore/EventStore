using System;
using Grpc.Core;

namespace EventStore.Client.Projections {
	public partial class EventStoreProjectionManagerClient {
		private readonly Projections.ProjectionsClient _client;

		public EventStoreProjectionManagerClient(CallInvoker callInvoker) {
			if (callInvoker == null) {
				throw new ArgumentNullException(nameof(callInvoker));
			}

			_client = new Projections.ProjectionsClient(callInvoker);
		}
	}
}
