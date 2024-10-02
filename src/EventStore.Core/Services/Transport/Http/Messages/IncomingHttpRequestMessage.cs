// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Messages {
	[DerivedMessage(CoreMessage.Http)]
	public partial class IncomingHttpRequestMessage : Message {
		public readonly IPublisher NextStagePublisher;
		public readonly IHttpService HttpService;
		public readonly HttpEntity Entity;

		public IncomingHttpRequestMessage(IHttpService httpService, HttpEntity entity, IPublisher nextStagePublisher) {
			HttpService = httpService;
			Entity = entity;
			NextStagePublisher = nextStagePublisher;
		}
	}
}
