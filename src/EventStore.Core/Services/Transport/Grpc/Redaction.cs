// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Bus;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Services.Transport.Grpc {
	internal partial class Redaction : EventStore.Client.Redaction.Redaction.RedactionBase {
		private readonly IPublisher _bus;
		private readonly IAuthorizationProvider _authorizationProvider;

		public Redaction(IPublisher bus, IAuthorizationProvider authorizationProvider) {
			_bus = bus;
			_authorizationProvider = authorizationProvider;
		}
	}
}
