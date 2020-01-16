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
		private readonly IAuthenticationProvider _authenticationProvider;

		public Operations(IQueuedHandler queue, IAuthenticationProvider authenticationProvider) {
			if (queue == null) throw new ArgumentNullException(nameof(queue));
			if (authenticationProvider == null) throw new ArgumentNullException(nameof(authenticationProvider));
			_queue = queue;
			_authenticationProvider = authenticationProvider;
		}
	}
}
