using System;
using System.Threading.Tasks;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Client.Operations;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Operations
		: EventStore.Client.Operations.Operations.OperationsBase {
		private readonly IQueuedHandler _queue;

		public Operations(IQueuedHandler queue) {
			if (queue == null) throw new ArgumentNullException(nameof(queue));
			_queue = queue;
		}
	}
}
