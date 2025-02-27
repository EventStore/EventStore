// Copyright (c) Kurrent, Inc and/or licensed to Kurrent, Inc under one or more agreements.
// Kurrent, Inc licenses this file to you under the Kurrent License v1 (see LICENSE.md).

using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Messages;

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
