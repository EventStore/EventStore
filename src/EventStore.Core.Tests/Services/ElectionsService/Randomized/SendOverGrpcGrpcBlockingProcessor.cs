using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Infrastructure;

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized {
	internal class SendOverGrpcGrpcBlockingProcessor : SendOverGrpcProcessor {
		private readonly Dictionary<IPEndPoint, bool> _endpointsToSkip;

		public SendOverGrpcGrpcBlockingProcessor(Random rnd,
			RandomTestRunner runner,
			double lossProb,
			double dupProb,
			int maxDelay) : base(rnd, runner, lossProb, dupProb, maxDelay) {
			_endpointsToSkip = new Dictionary<IPEndPoint, bool>();
		}

		public void RegisterEndpointToSkip(IPEndPoint endPoint, bool shouldSkip) {
			_endpointsToSkip[endPoint] = shouldSkip;
		}

		protected override bool ShouldSkipMessage(GrpcMessage.SendOverGrpc message) {
			bool shouldSkip;
			return _endpointsToSkip.TryGetValue(message.DestinationEndpoint, out shouldSkip) && shouldSkip;
		}
	}
}
