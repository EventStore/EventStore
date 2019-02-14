using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Infrastructure;

namespace EventStore.Core.Tests.Services.ElectionsService.Randomized {
	internal class ElectionsSafetyCondition : IRandTestFinishCondition {
		public bool Done { get; private set; }

		public bool Success {
			get {
				if (_electionsResults.Count == 0)
					return false;
				var master = _electionsResults.First().Value;
				return _electionsResults.Values.All(x => x.Equals(master)); // same master for all
			}
		}

		private readonly int _instancesCount;

		private readonly Dictionary<IPEndPoint, IPEndPoint>
			_electionsResults = new Dictionary<IPEndPoint, IPEndPoint>();

		public ElectionsSafetyCondition(int instancesCount) {
			_instancesCount = instancesCount;
		}

		public void Process(int iteration, RandTestQueueItem item) {
			var electionsMsg = item.Message as ElectionMessage.ElectionsDone;
			if (electionsMsg != null) {
				_electionsResults[item.EndPoint] = electionsMsg.Master.ExternalHttpEndPoint;
				Done = _electionsResults.Count == _instancesCount;
			}
		}

		public void Log() {
		}
	}
}
