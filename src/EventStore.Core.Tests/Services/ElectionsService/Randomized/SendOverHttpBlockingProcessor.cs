using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Infrastructure;

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized {
	internal class SendOverHttpBlockingProcessor : SendOverHttpProcessor {
		private readonly Dictionary<IPEndPoint, bool> _endpointsToSkip;

		public SendOverHttpBlockingProcessor(Random rnd,
			RandomTestRunner runner,
			double lossProb,
			double dupProb,
			int maxDelay) : base(rnd, runner, lossProb, dupProb, maxDelay) {
			_endpointsToSkip = new Dictionary<IPEndPoint, bool>();
		}

		public void RegisterEndpointToSkip(IPEndPoint endPoint, bool shouldSkip) {
			_endpointsToSkip[endPoint] = shouldSkip;
		}

		protected override bool ShouldSkipMessage(HttpMessage.SendOverHttp message) {
			bool shouldSkip;
			return _endpointsToSkip.TryGetValue(message.EndPoint, out shouldSkip) && shouldSkip;
		}
	}
}
