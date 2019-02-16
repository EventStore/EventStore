using System.Net;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Core.Tests.Infrastructure {
	public class RandTestQueueItem {
		public readonly int LogicalTime;
		public readonly int GlobalId;
		public readonly IPEndPoint EndPoint;
		public readonly Message Message;
		public readonly IPublisher Bus;

		public RandTestQueueItem(int logicalTime, int globalId, IPEndPoint endPoint, Message message, IPublisher bus) {
			LogicalTime = logicalTime;
			GlobalId = globalId;
			EndPoint = endPoint;
			Message = message;
			Bus = bus;
		}

		public override string ToString() {
			return string.Format("{0}-{1} :{2} to {3}", LogicalTime, GlobalId, Message, EndPoint.Port);
		}
	}
}
