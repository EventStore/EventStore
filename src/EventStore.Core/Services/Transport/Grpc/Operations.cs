using System;
using System.Threading.Tasks;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Client.Operations;
using EventStore.Core.Authorization;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Operations
		: EventStore.Client.Operations.Operations.OperationsBase {
		private readonly IPublisher _publisher;
		private readonly IAuthorizationProvider _authorizationProvider;

		public Operations(IPublisher publisher, IAuthorizationProvider authorizationProvider) {
			if (publisher == null) throw new ArgumentNullException(nameof(publisher));
			if (authorizationProvider == null) throw new ArgumentNullException(nameof(authorizationProvider));
			_publisher = publisher;
			_authorizationProvider = authorizationProvider;
		}
	}
}
